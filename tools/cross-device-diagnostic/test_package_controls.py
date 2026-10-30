"""Targeted package tamper controls, public synthetic artifacts only."""
import tempfile,shutil,subprocess,json,sys,unittest
from pathlib import Path
BASE=Path(__file__).resolve().parents[2]/'docs/research/color-engine-20261029/home-round10/diagnostic-home'
class PackageControls(unittest.TestCase):
 def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);self.copy=self.root/'run';shutil.copytree(BASE,self.copy)
 def tearDown(self):self.tmp.cleanup()
 def reject(self,fragment):
  p=subprocess.run([sys.executable,str(Path(__file__).with_name('compare_packages.py')),str(BASE),str(self.copy),str(self.root/'comparison.json')],capture_output=True,text=True);self.assertNotEqual(p.returncode,0);self.assertIn(fragment,p.stderr);self.assertFalse((self.root/'comparison.json').exists())
 def test_changed_whole(self):
  p=self.copy/'gray-boundaries/whole.rgba16';b=bytearray(p.read_bytes());b[0]^=1;p.write_bytes(b);self.reject('Whole result hash changed')
 def test_missing_case(self):
  p=self.copy/'run.json';j=json.loads(p.read_text());j['cases'].pop();p.write_text(json.dumps(j));self.reject('Missing/extra diagnostic case')
 def test_missing_stage(self):
  p=self.copy/'run.json';j=json.loads(p.read_text());j['cases'][0]['stageWhole'].pop();p.write_text(json.dumps(j));self.reject('Missing stage trace')
 def test_bt_domain_mismatch(self):
  p=self.copy/'run.json';j=json.loads(p.read_text());j['btDiagnostic']['mode']=0;p.write_text(json.dumps(j));self.reject('BT domain/input mismatch')
 def test_changed_resource_identity(self):
  p=self.copy/'run.json';j=json.loads(p.read_text());k=next(iter(j['resources']));j['resources'][k]='0'*64;p.write_text(json.dumps(j));self.reject('resource confound')
if __name__=='__main__':unittest.main()
