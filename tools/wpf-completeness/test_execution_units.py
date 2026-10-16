import importlib.util,pathlib,unittest,collections
R=pathlib.Path(__file__).resolve().parents[2];spec=importlib.util.spec_from_file_location('runner',R/'tools/planning-r7/verified_runner.py');runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
class UnitPlanTests(unittest.TestCase):
 def test_data_rows_kept_exactly_once(self):
  c=runner.P1_CLASS;rows=[dict(id=str(i),fullyQualifiedName=c+'.'+('RowMethod' if i<32 else 'Method'+str(i)),displayName='case'+str(i)) for i in range(42)];plan=runner.execution_units({c:rows});self.assertEqual(42,sum(map(len,plan.values())));self.assertEqual(11,len(plan));self.assertEqual(32,max(map(len,plan.values())));self.assertEqual(collections.Counter(x['id'] for x in rows),collections.Counter(x['id'] for a in plan.values() for x in a))
 def test_class_diagnostic_and_other_class_unchanged(self):
  c=runner.P1_CLASS;r=[dict(id='x',fullyQualifiedName=c+'.M',displayName='M')];self.assertEqual({c:r},runner.execution_units({c:r},p1_unit='class'));self.assertEqual({'Other':r},runner.execution_units({'Other':r}))
if __name__=='__main__':unittest.main()
