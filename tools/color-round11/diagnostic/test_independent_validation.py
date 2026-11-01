import unittest,tempfile,json,shutil,hashlib
from pathlib import Path
import numpy as np
from package_validation import validate_package,ROOT,read,sha
BASE=Path(__file__).resolve().parents[3]/'docs/research/color-engine-20261029/home-round10/diagnostic-home'
class IndependentValidation(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)/'run';shutil.copytree(BASE,self.root);self.j=json.loads((self.root/'run.json').read_text());self.j['version']='SyntheticCrossDeviceRunV2';self.j.pop('companyRun',None);self.j['execution']={'version':'DiagnosticMachineExecutionV1','deviceRole':'home','machine':'UNIT_TEST_NOT_REAL_EXECUTION','system':'test','gpu':'NVIDIA GeForce RTX 5060 Ti','driver':'610.88','status':'EXECUTED','harnessSha256':self.j['harnessSha256']};self.populate();self.save()
 def populate(self):
  self.j['execution']['toolSourceHashes']={k:'0'*64 for k in ['run_diagnostic.py','package_validation.py','compare_rows.py']};prov={'version':'Round11BuildIdentityV1','phase':'RECORDED_BEFORE_RUN','libraries':{'main':{'sha256':self.j['library']['sha256']},'trace':{'sha256':self.j['library']['traceSha256']}},'resources':__import__('copy').deepcopy(self.j['resources'])};(self.root/'build-provenance.json').write_text(json.dumps(prov));self.j['execution']['buildProvenance']=prov;self.j['execution']['buildProvenanceSha256']=sha(self.root/'build-provenance.json')
  c=read(ROOT/'fixtures/fixtures.json')
  for row,f in zip(self.j['cases'],c['fixtures']):
   row['inputFile']=row['id']+'/input.npy';(self.root/row['inputFile']).write_bytes((ROOT/'fixtures'/f['path']).read_bytes());row['inputIdentity']={'identity':f['identity'],'shape':f['shape'],'dtype':'<f4','fileSha256':f['fileSha256'],'pixelSha256':f['pixelSha256'],'domain':c['inputDomain'],'orientation':1}
 def tearDown(self):self.tmp.cleanup()
 def save(self):
  (self.root/'run.json').write_text(json.dumps(self.j));(self.root/'execution-before-render.json').write_text(json.dumps(self.j['execution']))
 def reject(self,text,role='home'):
  self.save()
  with self.assertRaisesRegex(ValueError,text):validate_package(self.root,role)
 def test_valid(self):self.assertTrue(validate_package(self.root,'home')[1]['dataChecksRecomputed'])
 def test_stream_pixel_and_hash_resealed_still_rejected(self):
  p=self.root/'gray-boundaries/stream-r7.rgba16';b=bytearray(p.read_bytes());b[0]^=1;p.write_bytes(b);row=next(x for x in self.j['cases'][0]['stream'] if x['rows']==7);row['outputHash']=hashlib.sha256(b).hexdigest();row['comparison']['changedChannels']=0;self.reject('whole/stream data mismatch')
 def test_trace_pixel_rejected_even_bool_true(self):
  p=self.root/'edge-small/whole-traced.rgba16';b=bytearray(p.read_bytes());b[-1]^=1;p.write_bytes(b);self.j['cases'][2]['wholeTraceUnchanged']=True;self.reject('Traced final data mismatch')
 def test_stream_trace_altered(self):
  p=self.root/'saturated-colors/stream-traced-r7.rgba16';b=bytearray(p.read_bytes());b[5]^=1;p.write_bytes(b);self.reject('Traced final data mismatch')
 def test_duplicate_case(self):self.j['cases'][2]=self.j['cases'][0];self.reject('Duplicate case')
 def test_missing_case(self):self.j['cases'].pop();self.reject('Missing/extra case')
 def test_wrong_shape(self):self.j['cases'][0]['shape'][0]+=1;self.reject('Case shape mismatch')
 def test_wrong_role(self):self.j['execution']['deviceRole']='company';self.reject('Device role mismatch')
 def test_invalid_role(self):self.j['execution']['deviceRole']='home-ish';self.reject('Invalid device role')
 def test_stage_nonfinite_rehashed(self):
  item=self.j['cases'][0]['stageWhole'][0];p=self.root/item['file'];b=bytearray(p.read_bytes());b[:4]=np.float32(float('nan')).tobytes();p.write_bytes(b);item['sha256']=hashlib.sha256(b).hexdigest();self.reject('Nonfinite stage')
 def test_stage_order(self):self.j['cases'][0]['stageWhole'][0],self.j['cases'][0]['stageWhole'][1]=self.j['cases'][0]['stageWhole'][1],self.j['cases'][0]['stageWhole'][0];self.reject('Stage order/shape')
 def test_stage_domain(self):self.j['cases'][0]['stageWhole'][0]['stageDomain']='sRGB';self.reject('Stage domain')
 def test_resolved_both_sides_cannot_change_contract(self):self.j['resolved']['print_exposure']=2;self.reject('resolved mismatch')
 def test_input_identity(self):self.j['cases'][0]['inputIdentity']['pixelSha256']='0'*64;self.reject('Case input identity')
 def test_stage_truncated_rehashed(self):
  item=self.j['cases'][0]['stageWhole'][0];p=self.root/item['file'];p.write_bytes(p.read_bytes()[:-4]);item['sha256']=sha(p);self.reject('Length/shape mismatch')
 def test_packaged_input_changed(self):
  f=self.root/self.j['cases'][0]['inputFile'];b=bytearray(f.read_bytes());b[-1]^=1;f.write_bytes(b);self.reject('Package input file changed')
 def test_duplicate_stage(self):self.j['cases'][0]['stageWhole'][1]=self.j['cases'][0]['stageWhole'][0];self.reject('Duplicate stage artifact')
 def test_missing_stage(self):self.j['cases'][0]['stageWhole'].pop();self.reject('Missing/extra stage')
 def test_execution_file_role_drift(self):
  self.save();self.j['execution']['deviceRole']='company';(self.root/'run.json').write_text(json.dumps(self.j))
  with self.assertRaisesRegex(ValueError,'Execution role record mismatch'):validate_package(self.root)
 def test_wrong_gpu_record(self):self.j['execution']['gpu']='Imaginary GPU';self.reject('GPU/driver identity inconsistent')
 def test_tool_identity_missing(self):self.j['execution']['toolSourceHashes']={};self.reject('tool identity')
 def test_resources_resealed(self):
  k=next(iter(self.j['resources']));self.j['resources'][k]='0'*64;self.j['execution']['buildProvenance']['resources'][k]='0'*64;(self.root/'build-provenance.json').write_text(json.dumps(self.j['execution']['buildProvenance']));self.j['execution']['buildProvenanceSha256']=sha(self.root/'build-provenance.json');self.reject('resource/SPIRV')
if __name__=='__main__':unittest.main()


