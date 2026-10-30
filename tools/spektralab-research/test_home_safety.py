import unittest,tempfile,json,threading
from pathlib import Path
from unittest.mock import patch
from types import SimpleNamespace
from host_budget import HostBudget,finalize_group
class Safety(unittest.TestCase):
 def test_os_peak_not_sampling_is_gate(self):
  with patch('host_budget.psutil.Process') as proc:
   proc.return_value.memory_info.return_value=SimpleNamespace(rss=10,peak_wset=101);b=HostBudget(100);self.assertRaises(MemoryError,b.enforce);self.assertFalse(b.finish()['wholeCallerWithinCap'])
 def test_monitor_join_even_on_exception(self):
  b=HostBudget(2**60).start()
  try:raise ValueError('primary')
  except ValueError:pass
  finally:r=b.finish()
  self.assertTrue(r['monitorJoined'])
 def test_second_rename_failure_leaves_no_complete_group(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp);(p/'rgba16.partial').write_bytes(b'a');(p/'display.partial.tif').write_bytes(b'b');budget=HostBudget(2**60);report={'success':False,'cleanupErrors':[]};n=[0]
   def rename(a,b):
    n[0]+=1
    if n[0]==2:raise OSError('TIFF rename failure')
    a.rename(b)
   def write(p,r):p.write_text(json.dumps(r))
   self.assertFalse(finalize_group(p,report,budget,write,rename));self.assertFalse((p/'rgba16.bin').exists());self.assertFalse((p/'display-rgba16.tif').exists());self.assertFalse(json.loads((p/'report.json').read_text())['success']);self.assertIn('TIFF rename failure',report['primaryError'])
 def test_report_preparation_failure_keeps_outputs_provisional(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp);(p/'rgba16.partial').write_bytes(b'a');(p/'display.partial.tif').write_bytes(b'b');report={'success':False,'cleanupErrors':[]};b=HostBudget(2**60)
   def write(path,value):
    if path.name.startswith('report.success'):raise OSError('prepare report disk failure')
    path.write_text(json.dumps(value))
   self.assertFalse(finalize_group(p,report,b,write));self.assertFalse((p/'rgba16.bin').exists());self.assertFalse((p/'display-rgba16.tif').exists());self.assertFalse(json.loads((p/'report.json').read_text())['success'])
 def test_peak_exceeded_by_report_serialization_cannot_commit(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp);(p/'rgba16.partial').write_bytes(b'a');(p/'display.partial.tif').write_bytes(b'b');report={'success':False,'cleanupErrors':[]};b=HostBudget(2**60)
   def write(path,value):
    path.write_text(json.dumps(value))
    if path.name.startswith('report.success'):b.cap=1
   self.assertFalse(finalize_group(p,report,b,write));self.assertFalse((p/'rgba16.bin').exists());self.assertFalse((p/'display-rgba16.tif').exists());self.assertFalse(json.loads((p/'report.json').read_text())['success'])
 def test_success_group_report_is_last(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp);(p/'rgba16.partial').write_bytes(b'a');(p/'display.partial.tif').write_bytes(b'b');report={'success':False,'cleanupErrors':[]};b=HostBudget(2**60);names=[]
   def write(path,value):names.append(path.name);path.write_text(json.dumps(value))
   self.assertTrue(finalize_group(p,report,b,write));self.assertTrue((p/'rgba16.bin').exists() and (p/'display-rgba16.tif').exists());self.assertTrue(json.loads((p/'report.json').read_text())['success']);self.assertNotIn('report.json',names)
 def test_primary_and_cleanup_errors_are_separate(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp);(p/'rgba16.partial').write_bytes(b'a');(p/'display.partial.tif').write_bytes(b'b');report={'success':False,'primaryError':'original source error','cleanupErrors':[{'resource':'mapping','error':'cleanup failed'}]};b=HostBudget(2**60)
   def write(path,value):
    if path.name.startswith('report.success'):raise OSError('report failed')
    path.write_text(json.dumps(value))
   self.assertFalse(finalize_group(p,report,b,write));self.assertEqual(report['primaryError'],'original source error');self.assertEqual(report['cleanupErrors'][0]['error'],'cleanup failed');self.assertTrue((p/'FAILED.json').exists())
 def test_whole_gate_does_not_use_assert(self):
  from stream_full_once import admitted
  self.assertRaises(ValueError,admitted,{'cases':[]},{'cases':[]})
if __name__=='__main__':unittest.main()
