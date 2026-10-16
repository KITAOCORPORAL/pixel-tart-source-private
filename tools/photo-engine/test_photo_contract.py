import unittest,pathlib,importlib.util,sys,struct
R=pathlib.Path(__file__).resolve().parents[2];spec=importlib.util.spec_from_file_location('photo',R/'tools/photo-engine/photo_srgb_workflow.py');p=importlib.util.module_from_spec(spec);spec.loader.exec_module(p)
class PhotoContractTests(unittest.TestCase):
 def test_exact_profile_and_corrupt_length(self):
  b=(R/'artifacts/photo-engine/standard-srgb.icc').read_bytes();p.profile(b,p.INPUTS);bad=bytearray(b);bad[:4]=bytes(4)
  with self.assertRaisesRegex(ValueError,'ICC_LENGTH'):p.profile(bytes(bad),p.INPUTS)
 def test_no_silent_profile_assignment(self):
  with self.assertRaisesRegex(ValueError,'ICC_MISSING'):p.profile(None,p.INPUTS)
 def test_structural_bounds_before_admission(self):
  b=bytearray((R/'artifacts/photo-engine/standard-srgb.icc').read_bytes());struct.pack_into('>I',b,136,0xffffffff)
  with self.assertRaisesRegex(ValueError,'ICC_TAG_BOUNDS'):p.profile(bytes(b),p.INPUTS)
 def test_old_linear_profile_is_not_new_encoded_input(self):
  b=(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc').read_bytes()
  with self.assertRaisesRegex(ValueError,'ICC_NOT_EVALUATED'):p.profile(b,p.INPUTS)
if __name__=='__main__':unittest.main()
