import pathlib,sys,json,hashlib,argparse
sys.path.insert(0,str(pathlib.Path('artifacts/v3-corrections/reference-python').resolve()))
import numpy as np,colour,PyOpenColorIO as ocio
parser=argparse.ArgumentParser();parser.add_argument('--input',type=pathlib.Path,default=pathlib.Path('artifacts/v3-corrections/04-formula-output.json'));parser.add_argument('--output',type=pathlib.Path,default=pathlib.Path('docs/research/v3-corrections-colorpage-20261017/04_color/INDEPENDENT_REFERENCE.json'));args=parser.parse_args()
if colour.__version__!='0.4.7' or ocio.__version__!='2.4.2':raise RuntimeError('Pinned reference version mismatch')
raw=args.input.read_text(encoding='utf-8-sig');raw=raw[raw.index('{'):];d=json.loads(raw);errors=[];comparisons=[]
def require_finite(value):
 if isinstance(value,dict):
  for v in value.values():require_finite(v)
 elif isinstance(value,list):
  for v in value:require_finite(v)
 elif isinstance(value,(int,float)) and not np.isfinite(value):raise ValueError('Non-finite probe output')
require_finite(d)
for name,fn,key in [('DaVinciIntermediate',colour.models.oetf_DaVinciIntermediate,'di'),('ACEScct',colour.models.log_encoding_ACEScct,'acescct'),('sRGB',colour.models.eotf_inverse_sRGB,'srgb'),('BT709',colour.models.oetf_BT709,'bt709'),('BT2020',lambda x:colour.models.oetf_BT2020(x,is_12_bits_system=True),'bt2020')]:
 x=np.array([r['x'] for r in d['rows']]);actual=np.array([r[key] for r in d['rows']]);expected=fn(x);maximum=float(abs(actual-expected).max());comparisons.append(dict(name=name,maxAbs=maximum,limit=2e-10));
 if maximum>2e-10:errors.append(name)
for name,fn,key in [('DI decode',colour.models.oetf_inverse_DaVinciIntermediate,'di'),('ACEScct decode',colour.models.log_decoding_ACEScct,'acescct')]:
 x=np.array([r['x'] for r in d['inverseSamples']]);actual=np.array([r[key] for r in d['inverseSamples']]);expected=fn(x);maximum=float(abs(actual-expected).max());relative=float((abs(actual-expected)/np.maximum(1,abs(expected))).max());comparisons.append(dict(name=name,maxAbs=maximum,maxRelative=relative,relativeLimit=2e-10))
 if relative>2e-10:errors.append(name)
def matrix_values(value):
 return np.array([[value[f'M{r}{c}'] for c in range(1,4)] for r in range(1,4)])
# Read actual production fields through the probe, not a copied literal table.
for key,space,inverse in [('dwg','DaVinci Wide Gamut',False),('xyzToDwg','DaVinci Wide Gamut',True),('srgb','sRGB',False),('bt2020','ITU-R BT.2020',False),('ap1D60','ACEScg',False)]:
 reference=colour.RGB_COLOURSPACES[space]
 # Derive both directions from independent primary/whitepoint definitions. sRGB's
 # conventional rounded table must not substitute for the declared precise matrix.
 matrix=colour.normalised_primary_matrix(reference.primaries,reference.whitepoint)
 if inverse:matrix=np.linalg.inv(matrix)
 maximum=float(abs(matrix_values(d['matrices'][key])-matrix).max())
 comparisons.append(dict(name='matrix '+key,maxAbs=maximum,limit=6e-9))
 if maximum>6e-9:errors.append('matrix '+key)
for name,fn,key in [('sRGB decode',colour.models.eotf_sRGB,'srgb'),('BT709 decode',colour.models.oetf_inverse_BT709,'bt709'),('BT2020 decode',lambda x:colour.models.oetf_inverse_BT2020(x,is_12_bits_system=True),'bt2020')]:
 x=np.array([r['x'] for r in d['inverseSamples']]);got=np.array([r[key] for r in d['inverseSamples']]);expected=fn(x);maximum=float(abs(got-expected).max());comparisons.append(dict(name=name,maxAbs=maximum,limit=2e-10))
 if maximum>2e-10:errors.append(name)
x=np.array([r['encoded'] for r in d['pq']]);got=np.array([r['decoded'] for r in d['pq']]);maximum=float(abs(got-colour.models.eotf_ST2084(x)).max());comparisons.append(dict(name='PQ decode cd/m2',maxAbs=maximum,limit=1e-7))
if maximum>1e-7:errors.append('PQ decode')
x=np.array([r['x'] for r in d['pq']]);got=np.array([r['encoded'] for r in d['pq']]);expected=colour.models.eotf_inverse_ST2084(x);maximum=float(abs(got-expected).max());comparisons.append(dict(name='PQ cd/m2',maxAbs=maximum,limit=1e-12))
if maximum>1e-12:errors.append('PQ')
# Independent OCIO ASC-CDL CPU implementation, explicit style selection. Not self-roundtrip.
for mode,key,style in [('Clamp','clamp',ocio.CDLStyle.CDL_ASC),('NoClamp','noClamp',ocio.CDLStyle.CDL_NO_CLAMP)]:
 transform=ocio.CDLTransform(slope=[1.1,.9,1.2],offset=[.01,-.02,.03],power=[.9,1.1,1.2],sat=.8);transform.setStyle(style);cpu=ocio.Config.CreateRaw().getProcessor(transform).getOptimizedCPUProcessor(ocio.OPTIMIZATION_NONE);maximum=0
 for row in d['cdl']:
  v=[row['input'][k] for k in ['X','Y','Z']];expected=cpu.applyRGB(v);actual=[row[key][k] for k in ['X','Y','Z']];maximum=max(maximum,max(abs(a-b) for a,b in zip(actual,expected)))
 comparisons.append(dict(name='CDL '+mode,maxAbs=maximum,limit=2e-6))
 if maximum>2e-6:errors.append('CDL '+mode)
result=dict(status='PASSED_INDEPENDENT_REFERENCE' if not errors else 'FAILED',errors=errors,colourVersion=colour.__version__,ocioVersion=ocio.__version__,colourCommit='a3bfe349685f528100672e5c8ca2dfeeef64a273',ocioCommit='6918fad3f5d22ac3ef2397c754bf4268c2b58dd0',colourModuleSha256=hashlib.sha256(pathlib.Path(colour.__file__).read_bytes()).hexdigest(),ocioModule=str(ocio.__file__),comparisons=comparisons,scope='Colour0.4.7 actual encoding/matrix and OCIO2.4.2 actual CDL; not self roundtrip or full Resolve output')
p=args.output;p.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2));raise SystemExit(bool(errors))
