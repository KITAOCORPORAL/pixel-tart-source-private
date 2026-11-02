"""Small CPU-only controls for one saved gray observation. No engine imports."""
from pathlib import Path
import sys,json,hashlib,shutil,tempfile,unittest
import numpy as np
repo=Path(__file__).resolve().parents[2];diag=repo/'tools/color-round11/diagnostic';sys.path.insert(0,str(diag));from verify_gray_scale_tap import validate
E=repo/'docs/research/color-engine-20261029/home-scale-localization-20261102';H=Path('N:/pixart/ColorResearchHome-20261030/home-scale-localization-20261102/gray-once')
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
class SavedGrayControls(unittest.TestCase):
 def test_full_home_validates_without_gpu(self):
  r,i,v=validate(H,'home');self.assertTrue(v['fullLocalCaptureValidated']);self.assertTrue(v['sealedSideFinalExact']);self.assertEqual(r['gpuApiCounts'],{'engineCreate':1,'open':1,'wholeRender':1,'streamRender':0,'btProbe':0})
 def test_public_subset_is_not_complete_capture(self):
  self.assertFalse(read(E/'public-subset-index.json')['isCompleteFullCapture'])
  with self.assertRaises((FileNotFoundError,KeyError,ValueError)):validate(E/'public-gray-taps','home')
 def test_wrong_role_rejected(self):
  with self.assertRaisesRegex(ValueError,'Run/role'):validate(H,'company')
 def test_corrupted_observation_refused(self):
  with tempfile.TemporaryDirectory() as temp:
   root=Path(temp)/'copy';shutil.copytree(H,root);p=root/'taps/01-tc.device.f32';b=bytearray(p.read_bytes());b[0]^=1;p.write_bytes(b)
   with self.assertRaisesRegex(ValueError,'Observation bytes'):validate(root,'home')
 def test_home_identity_real_wmi_fallback(self):
  i=read(H/'execution-before.json');p=i['gpuIdentityProbe'];self.assertTrue(p['fallbackUsed']);self.assertNotEqual(p['nvidiaSmi']['exitCode'],0);self.assertTrue(any('RTX 5060 Ti' in x['Name'] for x in p['videoControllers']));self.assertIn('WindowsDriverVersion=',i['gpu']);self.assertNotIn('GTX 1650',i['gpu'])
 def test_actual_narrow_upload_device_bitexact(self):
  for n in ['tc-b-matrix','tc-lut']:
   a=np.fromfile(H/'taps'/(n+'.cpu.f64'),dtype='<f8').astype('<f4');b=np.fromfile(H/'taps'/(n+'.uploaded.f32'),dtype='<f4');c=np.fromfile(H/'taps'/(n+'.device.f32'),dtype='<f4');np.testing.assert_array_equal(a.view('<u4'),b.view('<u4'));np.testing.assert_array_equal(b.view('<u4'),c.view('<u4'))
 def test_hash_only_does_not_claim_array_comparison(self):
  r=read(E/'paired-available-comparison.json');self.assertFalse(r['companyMaterialScope']['companyFullRuntimeArraysPresent']);self.assertEqual(len(r['summaryHashOnly']),4)
  for x in r['summaryHashOnly']:self.assertFalse(x['pairwiseByteComparisonPerformed']);self.assertIsNone(x['changedValues']);self.assertIsNone(x['maxAbs'])
 def test_tc_b_direct_input_matrix_and_two_output_boundary(self):
  r=read(E/'paired-available-comparison.json');rows={v['file']:v for v in r['directPaired']}
  for n in ['tc-b-matrix.cpu.f64','tc-b-matrix.uploaded.f32','tc-b-matrix.device.f32','00-input.device.f32']:self.assertTrue(rows['taps/'+n]['byteEqual'])
  self.assertEqual(rows['taps/01-tc.device.f32']['changedBitPatterns'],487);self.assertEqual(rows['taps/02-brightness.device.f32']['changedBitPatterns'],82);self.assertEqual(r['firstEffectiveGpuOutputDifference']['sameDispatchOutputs'],['01-tc.device.f32','02-brightness.device.f32'])
 def test_exposure_and_sealed_compound_no_new_difference(self):
  r=read(E/'paired-available-comparison.json')
  for name in ['homeExposureIdentity','homeExposureVsOldCompound','companyExposureVsOldCompound','homeFinalVsFrozen']:self.assertTrue(r[name]['byteEqual']);self.assertEqual(r[name]['changedBitPatterns'],0)
 def test_downstream_diff_not_all_propagation(self):
  r=read(E/'propagation-readback.json');self.assertEqual(r['sameTcAndBrightnessPixels'],158);self.assertEqual(r['upsampleChangedPixelsWithSameTcBrightness'],156);self.assertEqual(r['upsampleChangedComponentsWithSameTcBrightness'],429)
 def test_restricted_arrays_excluded_from_public_files(self):
  idx=read(E/'public-subset-index.json');names=[x['path'].removeprefix('public-gray-taps/') for x in idx['publicFiles']]
  for n in idx['notPublishedLocalSetupArrays']:self.assertNotIn(n,names);self.assertFalse((E/'public-gray-taps'/n).exists())
  for rec in idx['publicFiles']:self.assertEqual(sha(E/rec['path']),rec['sha256']);self.assertEqual((E/rec['path']).stat().st_size,rec['bytes'])
if __name__=='__main__':unittest.main()
