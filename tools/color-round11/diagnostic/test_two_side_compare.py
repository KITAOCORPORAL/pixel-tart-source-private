import unittest,tempfile,json,shutil,hashlib,subprocess,sys
from pathlib import Path
class TwoSideComparisonControls(unittest.TestCase):
 def test_compare_independently_rejects_resealed_stream_in_each_side(self):
  source=Path(__file__).resolve().parents[3]/'docs/research/color-engine-20261029/home-round11/diagnostic-home-v2';tool=Path(__file__).with_name('compare_packages.py')
  for badside in ['left','right']:
   with self.subTest(badside=badside),tempfile.TemporaryDirectory() as tmp:
    p=Path(tmp);bad=p/'bad';shutil.copytree(source,bad);file=bad/'gray-boundaries/stream-r1.rgba16';data=bytearray(file.read_bytes());data[2]^=1;file.write_bytes(data);j=json.loads((bad/'run.json').read_text());rec=next(x for x in j['cases'][0]['stream'] if x['rows']==1);rec['outputHash']=hashlib.sha256(data).hexdigest();rec['comparison']['changedChannels']=0;(bad/'run.json').write_text(json.dumps(j));left,right=(bad,source) if badside=='left' else (source,bad);result=subprocess.run([sys.executable,str(tool),str(left),str(right),str(p/'compare.json'),'--left-role','home','--right-role','home'],capture_output=True,text=True);self.assertNotEqual(result.returncode,0);self.assertIn('whole/stream data mismatch',result.stderr);self.assertFalse((p/'compare.json').exists())
 def test_expected_role_rejected_on_each_side(self):
  source=Path(__file__).resolve().parents[3]/'docs/research/color-engine-20261029/home-round11/diagnostic-home-v2';tool=Path(__file__).with_name('compare_packages.py')
  for wrongside in ['left','right']:
   with self.subTest(wrongside=wrongside),tempfile.TemporaryDirectory() as tmp:
    output=Path(tmp)/'compare.json';roles=['company','home'] if wrongside=='left' else ['home','company'];result=subprocess.run([sys.executable,'-O',str(tool),str(source),str(source),str(output),'--left-role',roles[0],'--right-role',roles[1]],capture_output=True,text=True);self.assertNotEqual(result.returncode,0);self.assertIn('Device role mismatch',result.stderr);self.assertFalse(output.exists())
 def test_valid_home_self_comparison(self):
  source=Path(__file__).resolve().parents[3]/'docs/research/color-engine-20261029/home-round11/diagnostic-home-v2';tool=Path(__file__).with_name('compare_packages.py')
  with tempfile.TemporaryDirectory() as tmp:
   output=Path(tmp)/'compare.json';result=subprocess.run([sys.executable,'-O',str(tool),str(source),str(source),str(output),'--left-role','home','--right-role','home'],capture_output=True,text=True);self.assertEqual(result.returncode,0,result.stderr);report=json.loads(output.read_text());self.assertIsNone(report['firstDivergentStage']);self.assertEqual(report['btDecode']['changedChannels'],0)
   for case in report['cases']:
    self.assertEqual(case['whole']['changedChannels'],0)
    for entry in case['stream']+case['stages']:self.assertEqual(entry['changedChannels'],0)
if __name__=='__main__':unittest.main()
