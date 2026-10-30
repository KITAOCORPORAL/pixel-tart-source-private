"""Targeted no-RAW process tests; synthetic fixtures are never D800 evidence."""
import copy,json,os,struct,subprocess,tempfile,unittest,hashlib
from pathlib import Path
DOTNET=os.environ.get('RESEARCH_DOTNET','dotnet')
DLL=Path(__file__).parent/'bin/Debug/net10.0/ResearchDecoder.dll'
class HardenedContract(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.saved=self.root/'variant'
  p=self.runcli('fixture',str(self.saved));self.assertEqual(p.returncode,0,p.stderr)
  self.recipe=json.loads((self.saved/'recipe.json').read_text(encoding='utf-8-sig'))
 def tearDown(self):self.temp.cleanup()
 def runcli(self,*args):return subprocess.run([DOTNET,str(DLL),*args],capture_output=True,text=True)
 def reopen(self):
  (self.saved/'recipe.json').write_text(json.dumps(self.recipe));return self.runcli('reopen',str(self.saved),str(self.root/'reopened'))
 def reject(self,text):
  p=self.reopen();self.assertNotEqual(p.returncode,0,p.stdout);self.assertIn(text,p.stderr);self.assertFalse((self.root/'reopened').exists())
 def test_fresh_process_full_domain(self):
  p=self.reopen();self.assertEqual(p.returncode,0,p.stderr);r=json.loads((self.root/'reopened/reopen.json').read_text(encoding='utf-8-sig'));self.assertTrue(r['synthetic'] and r['freshProcess'] and r['fullDomain'] and r['previewExact'] and r['exportExact'])
 def test_missing_identity(self):del self.recipe['algorithmIdentity'];self.reject('identity')
 def test_unknown_parameter(self):self.recipe['algorithmIdentity']['decodeSnapshot']['newKnob']=0;self.reject('identity')
 def test_wb_change(self):self.recipe['algorithmIdentity']['decodeSnapshot']['appliedWb'][0]=504;self.reject('identity')
 def test_matrix_change(self):self.recipe['algorithmIdentity']['decodeSnapshot']['floatMatrix'][0]=1;self.reject('identity')
 def test_source_implementation_change(self):self.recipe['algorithmIdentity']['implementationSourceSha256']['tools/color-research-decoder/Program.cs']='0'*64;self.reject('identity')
 def test_dependency_change(self):self.recipe['algorithmIdentity']['dependencies']['Sdcb.LibRaw.dll']='0'*64;self.reject('identity')
 def test_wrong_dimension(self):self.recipe['width']=9;self.reject('identity')
 def test_wrong_domain(self):self.recipe['domain']='ProPhoto D50';self.reject('identity')
 def test_bad_master_length(self):(self.saved/'master.f32').write_bytes(b'bad');self.reject('hash/length')
 def test_bad_master_hash(self):self.recipe['pixelHash']='0'*64;self.reject('hash/length')
 def test_nonfinite_with_valid_hash(self):
  p=self.saved/'master.f32';b=bytearray(p.read_bytes());b[:4]=struct.pack('<f',float('nan'));p.write_bytes(b);self.recipe['pixelHash']=hashlib.sha256(b).hexdigest().upper();self.reject('Nonfinite')
 def test_unknown_version(self):self.recipe['version']='MatrixDecodeResearchV99';self.reject('Unknown')
 def test_lifecycle(self):p=self.runcli('lifecycle-test');self.assertEqual(p.returncode,0,p.stderr);self.assertIn('callback-exception-cleanup PASS',p.stdout)
if __name__=='__main__':unittest.main()
