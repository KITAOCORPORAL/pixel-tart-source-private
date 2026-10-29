import copy,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
from stage_validation import validate_stage,validate_candidate,sha,NATIVE,MANAGED,SOURCES,STAGES,DOMAIN
class Validation(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);self.source=next(iter(SOURCES));w,h=7378,4924
  self.m={'manifestVersion':'StageManifestV2','sourceUnchanged':True,'productBaselineSampleMismatches':0,'nativeHash':NATIVE,'managedHash':MANAGED,'architecture':'X64','sourceSha256':self.source,'Maximum':4095,'make':'Nikon','model':'D800','filters':2492765332,'cblack':[0]*6,'sizes':{'RawWidth':7424,'RawHeight':4924,'IWidth':w,'IHeight':h,'TopMargin':0,'LeftMargin':0,'Flip':0,'RawPitch':14848},'output':{'Width':w,'Height':h,'Bits':16,'Channels':3},'stages':[{'name':n,'width':w,'height':h,'layout':'uint16 RG B G2, interleaved','RgbCam':[1,0,0,0,0,1,0,0,0,0,1,0],'PreMul':[1,1,1,1],'Black':0,'Maximum':4095,'cblack':[0]*6} for n in STAGES]}
  sizes={'cfa.u16':4924*14848,**{n+'.u16':w*h*8 for n in STAGES},'output.u16':w*h*6};self.m['files']=[{'name':n,'length':v,'sha256':'a'*64} for n,v in sizes.items()]
  for n,v in sizes.items():
   with (self.root/n).open('wb') as f:f.truncate(v)
  self.save();self.hashpatch=patch('stage_validation.sha',return_value='a'*64);self.hashpatch.start()
 def tearDown(self):self.hashpatch.stop();self.tmp.cleanup()
 def save(self):(self.root/'metadata.json').write_text(json.dumps(self.m))
 def test_valid(self):self.assertEqual(validate_stage(self.root)['sourceSha256'],self.source)
 def test_missing(self):self.m['files'].pop();self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_duplicate(self):self.m['stages'][-1]=copy.deepcopy(self.m['stages'][0]);self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_domain(self):self.m['stages'][0]['layout']='float';self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_layout(self):self.m['sizes']['LeftMargin']=1;self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_length(self):self.m['files'][0]['length']-=1;self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_tamper(self):self.m['files'][0]['sha256']='b'*64;self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_missing_physical_file(self):
  (self.root/self.m['files'][0]['name']).unlink();self.assertRaises(ValueError,validate_stage,self.root)
 def test_duplicate_file(self):
  self.m['files'][-1]=copy.deepcopy(self.m['files'][0]);self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_binary_identity(self):
  self.m['nativeHash']='0'*64;self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_hash_reads_bytes(self):
  p=self.root/'tiny';p.write_bytes(b'old');a=sha(p);p.write_bytes(b'new');self.assertNotEqual(a,sha(p))
 def test_candidate_missing_sidecar(self):
  self.assertRaises(FileNotFoundError,validate_candidate,self.root/'not-present',self.m)
 def test_duplicate_json_keys(self):
  (self.root/'metadata.json').write_text('{"manifestVersion":"StageManifestV2","manifestVersion":"bad"}');self.assertRaises(ValueError,validate_stage,self.root)
 def test_nonfinite_matrix(self):
  self.m['stages'][0]['RgbCam'][0]=float('nan');self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_physical_length(self):
  p=self.root/self.m['files'][0]['name']
  with p.open('r+b') as f:f.truncate(3)
  self.assertRaises(ValueError,validate_stage,self.root)
 def test_stage_levels(self):
  self.m['stages'][0]['Maximum']=4000;self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_stage_matrix_drift(self):
  self.m['stages'][0]['RgbCam'][0]=1.1;self.save();self.assertRaises(ValueError,validate_stage,self.root)
 def test_candidate_domain(self):
  f=self.root/'candidate';f.touch();side={'schema':'MatrixFloatOutputResearchV1','width':7378,'height':4924,'channels':'RGB','storage':'little-endian float32 interleaved','inputDomain':'post-demosaic, post-WB camera RGB uint16','outputDomain':'sRGB','icc':'none; no ICC embedded'};Path(str(f)+'.json').write_text(json.dumps(side));self.assertRaises(ValueError,validate_candidate,f,self.m)
if __name__=='__main__':unittest.main()
