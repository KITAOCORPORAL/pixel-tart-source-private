"""Stage evidence: preserve original references; explicitly configure colour's rounded decode cut.
scikit-image functions are extracted verbatim from the pinned wheel, with only matrix
multiplication set to identity to observe transfer stages, not rewritten formulas.
"""
import argparse, ast, hashlib, importlib, json, pathlib, sys, time
ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'artifacts/v3-corrections/reference-python'))
import numpy as np
import colour, PyOpenColorIO as ocio

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--input',type=pathlib.Path,required=True);ap.add_argument('--output',type=pathlib.Path,required=True);a=ap.parse_args()
 source=ROOT/'artifacts/color-chain-next/skimage-colorconv.py';tree=ast.parse(source.read_text());functions=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['rgb2xyz','xyz2rgb']]
 if len(functions)!=2:raise RuntimeError('Missing exact independent functions')
 for n in functions:n.decorator_list=[]
 ns=dict(np=np,xyz_from_rgb=np.eye(3),rgb_from_xyz=np.eye(3),_prepare_colorarray=lambda x,channel_axis=-1:np.array(x,dtype=np.float64),_convert=lambda m,x:np.array(x,dtype=np.float64)@m.T)
 exec(compile(ast.Module(body=functions,type_ignores=[]),str(source),'exec'),ns)
 # Observe un-clipped encoding by removing only np.clip from upstream xyz2rgb AST;
 # original xyz2rgb still supplies the independent final clipped result.
 enc=ast.parse(source.read_text());fn=next(n for n in enc.body if isinstance(n,ast.FunctionDef) and n.name=='xyz2rgb');fn.decorator_list=[];fn.name='encode_unclipped';fn.body=[n for n in fn.body if not(isinstance(n,ast.Expr) and isinstance(n.value,ast.Call) and isinstance(n.value.func,ast.Attribute) and n.value.func.attr=='clip')];exec(compile(ast.Module(body=[fn],type_ignores=[]),str(source),'exec'),ns)
 raw=a.input.read_text(encoding='utf-8-sig');rows=json.loads(raw[raw.index('['):]);module=importlib.import_module('colour.models.rgb.transfer_functions.srgb');original=module.eotf_inverse_sRGB
 def rounded_threshold(value):
  # Configure ONLY the scalar cutoff requested by eotf_sRGB. Transfer arithmetic
  # and vector encode remain the actual independently-maintained library.
  return np.float64(.04045) if np.ndim(value)==0 and float(value)==.0031308 else original(value)
 maximum={k:0.0 for k in ['colourDecoded','colourExposed','colourEncoded','colourOutput','skimageDecoded','skimageExposed','skimageEncoded','skimageOutput']};records=[];clock=time.perf_counter()
 for row in rows:
  v=lambda name:np.array([row[name][k] for k in ['X','Y','Z']],dtype=np.float64)
  x=v('input');gain=2**row['ev'];old=colour.models.eotf_sRGB(x);module.eotf_inverse_sRGB=rounded_threshold
  try:cd=colour.models.eotf_sRGB(x)
  finally:module.eotf_inverse_sRGB=original
  ce=cd*gain;cc=original(ce);sd=ns['rgb2xyz'](x[None,:])[0];se=sd*gain;sc=ns['encode_unclipped'](se[None,:])[0];so=ns['xyz2rgb'](se[None,:])[0]
  refs=dict(colourDecoded=cd,colourExposed=ce,colourEncoded=cc,colourOutput=np.clip(cc,0,1),skimageDecoded=sd,skimageExposed=se,skimageEncoded=sc,skimageOutput=so)
  measured={}
  for name,expected in refs.items():
   stage=name.removeprefix('colour').removeprefix('skimage').lower();actual=v(stage)
   if not np.isfinite(expected).all() or not np.isfinite(actual).all():raise ValueError('Non-finite stage')
   error=float(abs(actual-expected).max());maximum[name]=max(maximum[name],error);measured[name]=expected.tolist()
  decode=ocio.ExponentWithLinearTransform(gamma=[2.4]*3+[1],offset=[.055]*3+[0],negativeStyle=ocio.NEGATIVE_LINEAR);cpu=ocio.Config.CreateRaw().getProcessor(decode).getOptimizedCPUProcessor(ocio.OPTIMIZATION_NONE)
  od=np.array(cpu.applyRGB(x.tolist()));oc=ocio.ExponentWithLinearTransform(gamma=[2.4]*3+[1],offset=[.055]*3+[0],negativeStyle=ocio.NEGATIVE_LINEAR,direction=ocio.TRANSFORM_DIR_INVERSE);ecpu=ocio.Config.CreateRaw().getProcessor(oc).getOptimizedCPUProcessor(ocio.OPTIMIZATION_NONE);oe=od*gain;encoded=np.array(ecpu.applyRGB(oe.tolist()))
  records.append(dict(input=x.tolist(),ev=row['ev'],project={k:v(k).tolist() for k in ['decoded','exposed','encoded','output']},configuredColourAndScikit=measured,originalColourDecoded=old.tolist(),ocio=dict(inputFloat32=np.asarray(x,dtype=np.float32).tolist(),decoded=od.tolist(),exposed=oe.tolist(),encoded=encoded.tolist(),output=np.clip(encoded,0,1).tolist())))
 result=dict(status='PASS' if max(maximum.values())<=2e-10 else 'FAIL',limit=2e-10,maximum=maximum,rows=records,elapsedSeconds=time.perf_counter()-clock,sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),colourVersion=colour.__version__,ocioVersion=ocio.__version__,configuration='colour only scalar decode branch cut overridden .040449936 -> declared .04045; no formula substituted. scikit matrices identity to isolate transfer; no production code used as reference',scope='Double stage comparison; original colour/OCIO mismatches retained, not acceptance targets')
 a.output.write_text(json.dumps(result,indent=2)+'\n');print(result['status'],maximum);return 0 if result['status']=='PASS' else 1
if __name__=='__main__':raise SystemExit(main())
