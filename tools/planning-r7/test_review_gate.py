import unittest,copy
from review_gate import evaluate,RELEASE_ID
class Gate(unittest.TestCase):
 def summary(self,outcome='NotExecuted'):
  return {'selected':1,'runs':[{'class':'Exact.Class','expected':1,'exitCode':0,'timeout':False,'errors':['non-passing or not-executed tests'] if outcome!='Passed' else [],'results':[{'name':'Exact.Class.Test','outcome':outcome}]}]}
 def test_only_explicit_reviewed_condition(self):
  s=self.summary();original=copy.deepcopy(s);p={'tests':[{'name':'Exact.Class.Test','category':'conditional_hardware','reason':'GPU absent'}]};self.assertEqual('REVIEW_BUILD_WITH_OPEN_GATES',evaluate([s],p)['status']);self.assertEqual(original,s)
  self.assertEqual('BLOCKED',evaluate([s],{'tests':[]})['status']);p['tests'][0]['category']='required_automatic';self.assertEqual('BLOCKED',evaluate([s],p)['status'])
 def test_failures_infrastructure_never_exempt(self):
  p={'tests':[{'name':'Exact.Class.Test','category':'conditional_hardware','reason':'GPU absent'}]}
  for field,value in [('exitCode',1),('timeout',True),('errors',['missing TRX']),('expected',2)]:
   s=self.summary();s['runs'][0][field]=value;self.assertEqual('BLOCKED',evaluate([s],p)['status'])
  self.assertEqual('BLOCKED',evaluate([self.summary('Failed')],p)['status'])
if __name__=='__main__':unittest.main()
