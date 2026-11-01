"""No-RAW admission rejection tests. Never change the frozen round10 tools."""
from pathlib import Path
import subprocess,tempfile,unittest
DLL=Path(__file__).parent/'photo-preparation/bin/Debug/net10.0/PhotoPrepare.dll'
class PhotoAdmission(unittest.TestCase):
 def test_unknown_photo_id_rejected_before_decode(self):
  with tempfile.TemporaryDirectory() as tmp:
   out=Path(tmp)/'new';p=subprocess.run(['dotnet',str(DLL),'not-in-source-list',str(out)],capture_output=True,text=True);self.assertNotEqual(p.returncode,0);self.assertFalse(out.exists());self.assertIn('Sequence contains no matching element',p.stderr)
 def test_output_outside_authorized_root_rejected(self):
  with tempfile.TemporaryDirectory() as tmp:
   out=Path(tmp)/'new';p=subprocess.run(['dotnet',str(DLL),'canon600d',str(out)],capture_output=True,text=True);self.assertNotEqual(p.returncode,0);self.assertFalse(out.exists());self.assertIn('Output outside authorized research root',p.stderr)
if __name__=='__main__':unittest.main()
