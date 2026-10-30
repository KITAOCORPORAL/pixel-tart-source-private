"""Small rejection cases only: no RAW loading and no large buffers."""
import json,subprocess,tempfile,unittest
from pathlib import Path
DLL=Path(__file__).parent/'bin/Debug/net10.0/ResearchDecoder.dll'
class ContractRejection(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);self.variant=self.root/'variant';self.variant.mkdir();self.recipe={'version':'MatrixDecodeResearchV1','mode':'matrix','defaultEnabled':False,'sourceSha256':'3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5','backendHash':'F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9','width':7378,'height':4924,'domain':'LibRaw ProPhoto D65 linear float32, nominal 0..1, negatives and >1 retained','exposure':-2,'rawRecovery':False,'pixelHash':'0'*64}
 def tearDown(self):self.tmp.cleanup()
 def reject(self,fragment):
  (self.variant/'recipe.json').write_text(json.dumps(self.recipe));(self.variant/'master.f32').write_bytes(b'bad');result=subprocess.run([__import__('os').environ.get('RESEARCH_DOTNET','dotnet'),str(DLL),'reopen',str(self.variant),str(self.root/'new-output')],capture_output=True,text=True);self.assertNotEqual(result.returncode,0);self.assertIn(fragment,result.stderr);self.assertFalse((self.root/'new-output').exists())
 def test_unknown_version(self):self.recipe['version']='future';self.reject('identity mismatch')
 def test_implicit_enable(self):self.recipe['defaultEnabled']=True;self.reject('Unsupported saved research definition')
 def test_wrong_domain(self):self.recipe['domain']='sRGB';self.reject('Unsupported saved research definition')
 def test_wrong_backend(self):self.recipe['backendHash']='0'*64;self.reject('identity mismatch')
 def test_wrong_master_length(self):self.reject('hash/length mismatch')
 def test_mode_version_pair(self):self.recipe['mode']='headroom';self.reject('identity mismatch')
if __name__=='__main__':unittest.main()
