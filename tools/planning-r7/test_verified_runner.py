import importlib.util,pathlib,tempfile,unittest
spec=importlib.util.spec_from_file_location('runner',pathlib.Path(__file__).with_name('verified_runner.py'));m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
class Contracts(unittest.TestCase):
 def test_missing_trx_fails(self):
  with tempfile.TemporaryDirectory() as d:self.assertTrue(m.load_results(pathlib.Path(d)/'missing.trx',[])[1])
 def test_foreign_name_and_skip_fail(self):
  with tempfile.TemporaryDirectory() as d:
   p=pathlib.Path(d)/'test.trx';p.write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><TestDefinitions><UnitTest id="1"><TestMethod className="A.OtherClass" name="Method"/></UnitTest></TestDefinitions><Results><UnitTestResult testId="1" testName="Method" outcome="NotExecuted"/></Results><ResultSummary><Counters total="1"/></ResultSummary></TestRun>''')
   _,errors=m.load_results(p,[{'fullyQualifiedName':'A.Class.Method'}]);self.assertEqual(2,len(errors))
 def test_exact_match_only_pass(self):
  with tempfile.TemporaryDirectory() as d:
   p=pathlib.Path(d)/'test.trx';p.write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><TestDefinitions><UnitTest id="1"><TestMethod className="A.Class" name="Method"/></UnitTest></TestDefinitions><Results><UnitTestResult testId="1" testName="Method" outcome="Passed"/></Results><ResultSummary><Counters total="1"/></ResultSummary></TestRun>''')
   self.assertFalse(m.load_results(p,[{'fullyQualifiedName':'A.Class.Method'}])[1])
if __name__=='__main__':unittest.main()
