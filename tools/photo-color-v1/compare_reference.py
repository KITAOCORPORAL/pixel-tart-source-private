import pathlib,sys,json,hashlib,zipfile
sys.path.insert(0,str(pathlib.Path('artifacts/v3-corrections/reference-python').resolve()))
import numpy as np,colour,PyOpenColorIO as ocio
raw=pathlib.Path('artifacts/v3-corrections/04-formula-output.json').read_text(encoding='utf-8-sig');raw=raw[raw.index('{'):];d=json.loads(raw);errors=[];comparisons=[]
for name,fn,key in [('DaVinciIntermediate',colour.models.oetf_DaVinciIntermediate,'di'),('ACEScct',colour.models.log_encoding_ACEScct,'acescct'),('sRGB',colour.models.eotf_inverse_sRGB,'srgb'),('BT709',colour.models.oetf_BT709,'bt709'),('BT2020',lambda x:colour.models.oetf_BT2020(x,is_12_bits_system=True),'bt2020')]:
 x=np.array([r['x'] for r in d['rows']]);actual=np.array([r[key] for r in d['rows']]);expected=fn(x);maximum=float(abs(actual-expected).max());comparisons.append(dict(name=name,maxAbs=maximum,limit=2e-10));
 if maximum>2e-10:errors.append(name)
for name,fn,key in [('DI decode',colour.models.oetf_inverse_DaVinciIntermediate,'di'),('ACEScct decode',colour.models.log_decoding_ACEScct,'acescct')]:
 x=np.array([r['x'] for r in d['inverseSamples']]);actual=np.array([r[key] for r in d['inverseSamples']]);expected=fn(x);maximum=float(abs(actual-expected).max());relative=float((abs(actual-expected)/np.maximum(1,abs(expected))).max());comparisons.append(dict(name=name,maxAbs=maximum,maxRelative=relative,relativeLimit=2e-10))
 if relative>2e-10:errors.append(name)
xyz=np.array(d['dwg']);matrix=colour.RGB_COLOURSPACES['DaVinci Wide Gamut'].matrix_RGB_to_XYZ;maximum=float(abs(xyz-matrix).max());comparisons.append(dict(name='DWG matrix',maxAbs=maximum,limit=6e-9))
if maximum>6e-9:errors.append('DWG matrix')
x=np.array([r['x'] for r in d['pq']]);got=np.array([r['encoded'] for r in d['pq']]);expected=colour.models.eotf_inverse_ST2084(x);maximum=float(abs(got-expected).max());comparisons.append(dict(name='PQ cd/m2',maxAbs=maximum,limit=1e-12))
if maximum>1e-12:errors.append('PQ')
# Independent OCIO ASC-CDL CPU implementation, explicit style selection. Not self-roundtrip.
for mode,key,style in [('Clamp','clamp',ocio.CDLStyle.CDL_ASC),('NoClamp','noClamp',ocio.CDLStyle.CDL_NO_CLAMP)]:
 transform=ocio.CDLTransform(slope=[1.1,.9,1.2],offset=[.01,-.02,.03],power=[.9,1.1,1.2],sat=.8);transform.setStyle(style);cpu=ocio.Config.CreateRaw().getProcessor(transform).getOptimizedCPUProcessor(ocio.OPTIMIZATION_NONE);maximum=0
 for row in d['cdl']:
  v=[row['input'][k] for k in ['X','Y','Z']];expected=cpu.applyRGB(v);actual=[row[key][k] for k in ['X','Y','Z']];maximum=max(maximum,max(abs(a-b) for a,b in zip(actual,expected)))
 comparisons.append(dict(name='CDL '+mode,maxAbs=maximum,limit=2e-6))
 if maximum>2e-6:errors.append('CDL '+mode)
result=dict(status='PASSED_INDEPENDENT_REFERENCE' if not errors else 'FAILED',errors=errors,colourVersion=colour.__version__,ocioVersion=ocio.__version__,colourModuleSha256=hashlib.sha256(pathlib.Path(colour.__file__).read_bytes()).hexdigest(),ocioModule=str(ocio.__file__),comparisons=comparisons,scope='Colour0.4.7 actual encoding/matrix and OCIO2.4.2 actual CDL; not self roundtrip or full Resolve output')
p=pathlib.Path('docs/research/v3-corrections-colorpage-20261017/04_color/INDEPENDENT_REFERENCE.json');p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2));raise SystemExit(bool(errors))
