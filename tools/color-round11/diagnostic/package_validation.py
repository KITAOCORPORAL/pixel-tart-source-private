"""Round11 shared standalone package validation. Never trust recorded PASS booleans."""
from pathlib import Path
import json,hashlib,re,platform
import numpy as np
from compare_rows import compare
ROOT=Path(__file__).parent
FILM=['film_scale_and_expose','film_boost','film_overscan','film_blurs','film_log_and_curves','film_couplers','film_grain']
PRINT=['print_spectral','print_glare','print_linear','print_scan_finish','print_output']
DOMAINS=dict(zip(FILM+PRINT,['exposed camera spectral-response RGB float','exposure RGB float','exposure RGB float','exposure RGB float','CMY negative density float','CMY negative density float','CMY negative density float','print scan tristimulus intermediate float, see fixed print_spectral function','print scan tristimulus intermediate float','output sRGB D65 linear float','output sRGB D65 linear float after scanner filters','output sRGB encoded float before final RGBA16 clamp/round']))
def need(c,m):
 if not c:raise ValueError(m)
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def read(path):
 def pairs(items):
  out={}
  for k,v in items:
   need(k not in out,'Duplicate JSON key '+k);out[k]=v
  return out
 def invalid(v):raise ValueError('Nonfinite JSON '+v)
 return json.loads(Path(path).read_text(encoding='utf-8-sig'),object_pairs_hook=pairs,parse_constant=invalid)
def safe(root,name):
 p=(root/name).resolve();need(p.is_relative_to(root.resolve()) and p.is_file(),'Missing or escaped artifact '+name);return p
def finite_file(path,shape,dtype):
 need(isinstance(shape,list) and len(shape)==3 and all(type(x) is int and x>0 for x in shape),'Invalid artifact shape')
 need(path.stat().st_size==int(np.prod(shape))*np.dtype(dtype).itemsize,'Length/shape mismatch '+str(path))
 if dtype=='<f4':
  data=np.fromfile(path,dtype=dtype);need(np.isfinite(data).all(),'Nonfinite stage '+str(path));return data.reshape(shape)
def stage_pattern(h,w,stream):
 if not stream:return [(name,[h,w,3]) for name in FILM+PRINT]
 result=[]
 for y in range(0,h,7):result += [(name,[min(7,h-y),w,3]) for name in FILM]
 for y in range(0,h,7):result += [(name,[min(h,y+min(7,h-y)+2)-max(0,y-2),w,3]) for name in PRINT]
 return result
def execution_identity(r,legacy,expected_role):
 if legacy:
  need(r.get('version')=='SyntheticCrossDeviceRunV1','Unsupported legacy package')
  pin=ROOT.parents[2]/'docs/research/color-engine-20261029/home-round10/diagnostic-home/run.json'
  # Legacy identity allowed only for exact sealed round10 run.json hash, not attacker-edited clones.
  need(sha(pin)=='f1dccda478588f5e994e2b46cf06d903d0193fa95a8ec3d404aa0049e0be92ae' and sha(pin)==r.pop('_runSha'),'Legacy run identity is not exact sealed home record')
  role='home'
 else:
  need(r.get('version')=='SyntheticCrossDeviceRunV2','Unsupported run version');e=r.get('execution',{});role=e.get('deviceRole');need(role in ['home','company'],'Invalid device role');need(e.get('version')=='DiagnosticMachineExecutionV1' and e.get('machine') and e.get('system') and e.get('gpu') and e.get('driver'),'Missing real machine identity');need(e['gpu'] in r.get('gpu','') and e['driver'] in r.get('gpu',''),'Role GPU/driver identity inconsistent');need(e.get('status')=='EXECUTED','Role not executed');need(e.get('harnessSha256')==r.get('harnessSha256'),'Harness identity inconsistent')
 if expected_role:need(role==expected_role,'Device role mismatch: expected '+expected_role)
 return role

def fixed_contract():
 for file,pin in [('fixtures/fixtures.json','bbb0277b828bf15ec1963df81565faad09faf863525063ae1a03b7a79a3515bc'),('resolved-portraA.json','2036432956d149bc265a851c26d0241bd5753a7882a679aaa2c560a9d5c8710b'),('expected-resources.json','ee39138735771e0f3ce29256677ea2b60e810943bb778cb0188d8d5ca14ba7ff')]:need(sha(ROOT/file)==pin,'Fixed validator contract bytes altered: '+file)
 return read(ROOT/'fixtures/fixtures.json'),read(ROOT/'resolved-portraA.json'),read(ROOT/'expected-resources.json')

def validate_package(root,expected_role=None,allow_sealed_round10=False):
 root=Path(root);r=read(root/'run.json');r['_runSha']=sha(root/'run.json');legacy=r.get('version')=='SyntheticCrossDeviceRunV1';need(not legacy or allow_sealed_round10,'Legacy package requires explicit sealed-round10 flag');role=execution_identity(r,legacy,expected_role);r.pop('_runSha',None)
 if not legacy:
  need(read(root/'execution-before-render.json')==r['execution'],'Execution role record mismatch');need(sha(safe(root,'build-provenance.json'))==r['execution'].get('buildProvenanceSha256'),'Build provenance hash mismatch');need(r['execution'].get('buildProvenance')==read(root/'build-provenance.json'),'Embedded provenance mismatch');need(isinstance(r['execution'].get('toolSourceHashes'),dict) and all(k in r['execution']['toolSourceHashes'] for k in ['run_diagnostic.py','package_validation.py','compare_rows.py']) and all(isinstance(v,str) and len(v)==64 and all(c in '0123456789abcdef' for c in v.lower()) for v in r['execution']['toolSourceHashes'].values()),'Missing or malformed tool identity');prov=r['execution']['buildProvenance'];need(prov.get('version')=='Round11BuildIdentityV1' and prov.get('phase')=='RECORDED_BEFORE_RUN' and prov['libraries']['main']['sha256']==r['library']['sha256'] and prov['libraries']['trace']['sha256']==r['library']['traceSha256'] and prov['resources']==r['resources'],'Build/runtime identity mismatch')
 contract,resolved,resources=fixed_contract();need(r.get('privatePhotosIncluded') is False,'Private photo package forbidden');need(r.get('syntheticManifestHash')==sha(ROOT/'fixtures/fixtures.json'),'Input manifest identity mismatch');need(r.get('syntheticSourceIdentity')==hashlib.sha256(b'SyntheticPortraCrossDeviceV1:PUBLIC_SYNTHETIC_NO_CAMERA').hexdigest(),'Synthetic source identity mismatch');need(r.get('resolved')==resolved,'Fixed complete resolved mismatch');need(r.get('resources')==resources,'Fixed resource/SPIRV identity mismatch');need(r.get('resourceSetSha256')==hashlib.sha256(json.dumps(resources,sort_keys=True,separators=(',',':')).encode()).hexdigest(),'Resource manifest root hash mismatch');cases=r.get('cases',[]);need(len(cases)==3,'Missing/extra case');need(len({x['id'] for x in cases})==3,'Duplicate case');need([x['id'] for x in cases]==[x['id'] for x in contract['fixtures']],'Case identity/order mismatch');results=[]
 for case,fixture in zip(cases,contract['fixtures']):
  fixturepath=ROOT/'fixtures'/fixture['path'];need(sha(fixturepath)==fixture['fileSha256'],'Fixed fixture bytes altered');inputdata=np.load(fixturepath,allow_pickle=False);need(inputdata.shape==tuple(fixture['shape']) and inputdata.dtype==np.dtype('<f4') and np.isfinite(inputdata).all() and hashlib.sha256(inputdata.tobytes()).hexdigest()==fixture['pixelSha256'],'Fixed fixture domain/pixels altered')
  if not legacy:need(case.get('inputIdentity')=={'identity':fixture['identity'],'shape':fixture['shape'],'dtype':'<f4','fileSha256':fixture['fileSha256'],'pixelSha256':fixture['pixelSha256'],'domain':contract['inputDomain'],'orientation':1},'Case input identity mismatch')
  if not legacy:
   supplied=safe(root,case.get('inputFile',''));need(sha(supplied)==fixture['fileSha256'],'Package input file changed');supplied_data=np.load(supplied,allow_pickle=False);need(supplied_data.shape==tuple(fixture['shape']) and supplied_data.dtype==np.dtype('<f4') and np.isfinite(supplied_data).all() and hashlib.sha256(supplied_data.tobytes()).hexdigest()==fixture['pixelSha256'],'Package input pixels/domain changed')
  h,w,_=fixture['shape'];need(case.get('shape')==[h,w,4],'Case shape mismatch');folder=case['id'];whole=safe(root,folder+'/whole.rgba16');finite_file(whole,[h,w,4],'<u2');need(sha(whole)==case['wholeInfo']['sha256'],'Whole result hash changed');need(case['wholeInfo']['width']==w and case['wholeInfo']['height']==h and case['wholeInfo']['row_stride_px']>=w,'Whole actual dimensions mismatch');checks=[];stream=case.get('stream',[]);need(len(stream)==4 and {x['rows'] for x in stream}=={1,7,17,64},'Missing/duplicate rows')
  for rec in stream:
   path=safe(root,folder+'/stream-r'+str(rec['rows'])+'.rgba16');need(sha(path)==rec['outputHash'],'Stream artifact hash mismatch');d=compare(whole,path,[h,w,4],'<u2');need(d['changedChannels']==0,'Same-device whole/stream data mismatch');checks.append(d)
  for name in ['whole-traced.rgba16','stream-traced-r7.rgba16']:
   path=safe(root,folder+'/'+name);need(compare(whole,path,[h,w,4],'<u2')['changedChannels']==0,'Traced final data mismatch')
  for key,streaming in [('stageWhole',False),('stageStream',True)]:
   pattern=stage_pattern(h,w,streaming);items=case.get(key,[]);need(len(items)==len(pattern),'Missing/extra stage');need(len({x['file'] for x in items})==len(items),'Duplicate stage artifact')
   for item,(stage,shape) in zip(items,pattern):
    need(item['stage']==stage and item['shape']==shape,'Stage order/shape mismatch');need(item.get('stageDomain')==DOMAINS[stage],'Stage domain mismatch');path=safe(root,item['file']);need(sha(path)==item['sha256'],'Stage hash mismatch');data=finite_file(path,shape,'<f4');need(item.get('min')==float(data.min()) and item.get('max')==float(data.max()),'Stage statistics mismatch');need(item.get('sample')==data.reshape(-1,3)[:8].tolist(),'Stage sample mismatch')
  results.append({'id':folder,'sameDeviceWholeStreamDataExact':True,'traceFinalDataExact':True,'stageFilesValidated':len(case['stageWhole'])+len(case['stageStream'])})
 bt=r.get('btDiagnostic',{});need(bt.get('mode')==3 and bt.get('shape')==[1,13,1] and bt.get('inputBits')==contract['bt']['inputBits'],'BT fixed input/domain mismatch');bti=safe(root,'bt-input.f32');bto=safe(root,'bt-decode.f32');data=finite_file(bti,[1,13,1],'<f4');finite_file(bto,[1,13,1],'<f4');need(sha(bti)==bt['inputHash'] and sha(bto)==bt['outputHash'],'BT hash mismatch');need([format(int(x),'08x') for x in data.reshape(-1).view('<u4')]==bt['inputBits'],'BT actual input altered');need([format(int(x),'08x') for x in np.fromfile(bto,dtype='<u4')]==bt['outputBits'],'BT actual output bits altered')
 return r,{'version':'PackageIndependentValidationV2','deviceRole':role,'dataChecksRecomputed':True,'cases':results,'legacySealedRound10':legacy}
