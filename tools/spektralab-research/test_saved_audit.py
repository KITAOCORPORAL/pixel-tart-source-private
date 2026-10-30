"""Saved audit failures must terminate their RSS thread. Tiny synthetic files, not camera coverage."""
import unittest,tempfile,threading,hashlib,json,os
from pathlib import Path
import numpy as np,tifffile
from host_environment import environment
from stream_saved_reopen import audit
class SavedAuditFailures(unittest.TestCase):
 def setUp(self):
  self.root=Path(tempfile.mkdtemp(prefix='saved-audit-control-',dir=environment()['researchRoot']));self.saved=self.root/'saved';self.saved.mkdir();self.data=np.arange(48,dtype='<u2').reshape(3,4,4);self.data.tofile(self.saved/'rgba16.bin');tifffile.imwrite(self.saved/'display-rgba16.tif',self.data,photometric='rgb',extrasamples='unassalpha',rowsperstrip=1,compression=None);self.report={'success':True,'status':'COMPLETE','height':3,'width':4,'outputPixelSha256':hashlib.sha256(self.data.tobytes()).hexdigest()};self.save()
 def save(self):(self.saved/'report.json').write_text(json.dumps(self.report))
 def runaudit(self):
  r=audit(self.saved,self.root/'audit.json');self.assertFalse(any(t.name=='host-budget-monitor' and t.is_alive() for t in threading.enumerate()));self.assertTrue(r['monitorJoined']);return r
 def test_valid_saved_byte_audit_not_rerender(self):
  r=self.runaudit();self.assertTrue(r['success']);self.assertEqual(r['renderCalls'],0)
 def test_truncated_raw(self):
  (self.saved/'rgba16.bin').write_bytes(b'bad');r=self.runaudit();self.assertFalse(r['success']);self.assertIn('Short raw row',r['primaryError'])
 def test_wrong_tiff_layout(self):
  (self.saved/'display-rgba16.tif').unlink();tifffile.imwrite(self.saved/'display-rgba16.tif',self.data,photometric='rgb',extrasamples='unassalpha',rowsperstrip=3,compression=None);r=self.runaudit();self.assertFalse(r['success']);self.assertIn('Unexpected TIFF layout',r['primaryError'])
 def test_wrong_pixel_hash(self):
  self.report['outputPixelSha256']='0'*64;self.save();r=self.runaudit();self.assertFalse(r['success']);self.assertIn('pixel hash mismatch',r['primaryError'])
if __name__=='__main__':unittest.main()
