import unittest,json,pathlib,csv,importlib.util
spec=importlib.util.spec_from_file_location('dual',pathlib.Path(__file__).with_name('audit_dual_intake.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class DualTests(unittest.TestCase):
 def test_canonical_source_not_host_pair(self):
  self.assertEqual(m.canonical('https://www.example.com/post/?utm_source=a'),m.canonical('http://example.com/post'))
  self.assertNotEqual(m.canonical('https://example.com/a'),m.canonical('https://example.com/b'))
 def test_unknown_is_not_observed(self):
  for value in ['',None,'UNKNOWN','未知']:self.assertFalse(m.known(value))
if __name__=='__main__':unittest.main()
