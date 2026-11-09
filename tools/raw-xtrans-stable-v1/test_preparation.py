# Fail-closed source preparation tests, not further pixel or RAW experiments.
from pathlib import Path
import sys,json,tempfile,unittest,subprocess,hashlib
sys.path.insert(0,str(Path(__file__).resolve().parent))
from prepare_candidate import change,BASE,VERSION
W=Path(__file__).resolve().parents[3];ROOT=W/'raw-reopen001-shared-research/stable-input-v1-20261109';original=(W/'raw-reopen001-shared-research/libraw-0.21.1/src/demosaic/xtrans_demosaic.cpp').read_text()
class Preparation(unittest.TestCase):
 def test_pristine_reproduces_executed_candidate(self):
  out,proof=change(original);self.assertEqual(out,(ROOT/'candidate-source/xtrans_demosaic.cpp').read_text());self.assertTrue(proof['workerInverseNormalizedExact']);self.assertEqual(proof['remainingMutableImageWorkerUses'],0)
 def test_reviewed_observer_reproduces_candidate(self):
  source=(ROOT/'frozen-observer-xtrans.cpp').read_text();out,p=change(source);self.assertEqual(out,(ROOT/'observer-candidate-source/xtrans_demosaic.cpp').read_text());self.assertTrue(p['workerInverseNormalizedExact'])
 def test_formula_drift_rejected(self):
  with self.assertRaises(ValueError):change(original.replace('14269','14270').replace('174 *','175 *',1))
 def test_already_patched_source_rejected(self):
  with self.assertRaises(ValueError):change((ROOT/'candidate-source/xtrans_demosaic.cpp').read_text())
 def test_explicit_candidate_flag_required_before_output(self):
  with tempfile.TemporaryDirectory() as tmp:
   out=Path(tmp)/'new';source=Path(tmp)/'input.cpp';source.write_text(original);r=subprocess.run([sys.executable,'-B',str(Path(__file__).with_name('prepare_candidate.py')),'--source',str(source),'--output',str(out)],capture_output=True,text=True);self.assertNotEqual(r.returncode,0);self.assertFalse(out.exists())
 def test_existing_output_not_overwritten(self):
  with tempfile.TemporaryDirectory() as tmp:
   out=Path(tmp)/'existing';out.mkdir();keep=out/'keep';keep.write_text('keep');source=Path(tmp)/'input.cpp';source.write_text(original);r=subprocess.run([sys.executable,'-B',str(Path(__file__).with_name('prepare_candidate.py')),'--source',str(source),'--output',str(out),'--enable-candidate',VERSION],capture_output=True,text=True);self.assertNotEqual(r.returncode,0);self.assertEqual(keep.read_text(),'keep')
 def test_output_not_read_before_initialization_or_before_join(self):
  out,_=change(original);self.assertLess(out.index('memcpy(output.pixels[p], image[p]'),out.index('# pragma omp parallel for'));self.assertGreater(out.index('memcpy(image[p], output.pixels[p]'),out.index('free_omp_buffers(buffers, buffer_count)'));self.assertGreater(out.index('border_interpolate(8)'),out.index('output.pixels = 0'))
 def test_nested_source_output_refused(self):
  with tempfile.TemporaryDirectory() as tmp:
   source=Path(tmp)/'input.cpp';source.write_text(original);out=Path(tmp)/'new';r=subprocess.run([sys.executable,'-B',str(Path(__file__).with_name('prepare_candidate.py')),'--source',str(source),'--output',str(out),'--enable-candidate',VERSION],capture_output=True,text=True);self.assertNotEqual(r.returncode,0);self.assertIn('outside',r.stderr);self.assertFalse(out.exists())
if __name__=='__main__':unittest.main()
