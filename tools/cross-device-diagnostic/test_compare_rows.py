import unittest,tempfile
from pathlib import Path
import numpy as np
from compare_rows import compare
class RowComparison(unittest.TestCase):
 def test_exact(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);a=np.arange(48,dtype='<u2').reshape(3,4,4);a.tofile(p/'a');a.tofile(p/'b');r=compare(p/'a',p/'b',[3,4,4],'<u2');self.assertTrue(r['exact']);self.assertIsNone(r['firstDifference'])
 def test_known_first_count_quantiles(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);a=np.zeros((2,3,4),'<u2');b=a.copy();b[0,1,2]=3;b[1,2,0]=7;a.tofile(p/'a');b.tofile(p/'b');r=compare(p/'a',p/'b',[2,3,4],'<u2');self.assertEqual(r['changedChannels'],2);self.assertEqual(r['firstDifference']['x'],1);self.assertEqual(r['firstDifference']['channel'],2);self.assertEqual(r['maxAbsoluteDelta'],7);self.assertAlmostEqual(r['meanAbsoluteDelta'],10/24);self.assertEqual(r['absoluteDeltaQuantiles']['1'],7)
 def test_nonfinite_refused(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);a=np.array([[[float('nan'),0,1]]],dtype='<f4');a.tofile(p/'a');a.tofile(p/'b');self.assertRaises(ValueError,compare,p/'a',p/'b',[1,1,3],'<f4')
 def test_length_refused(self):
  with tempfile.TemporaryDirectory() as d:
   p=Path(d);(p/'a').write_bytes(b'bad');(p/'b').write_bytes(b'bad');self.assertRaises(ValueError,compare,p/'a',p/'b',[1,1,4],'<u2')
if __name__=='__main__':unittest.main()
