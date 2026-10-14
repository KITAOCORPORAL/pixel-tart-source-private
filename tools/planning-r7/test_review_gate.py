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
 def test_missing_required_test_cannot_pass_subset(self):
  p={'tests':[{'name':'Exact.Class.Test','category':'required_automatic','reason':'required'},{'name':'Exact.Class.Other','category':'required_automatic','reason':'required'}]};self.assertEqual('BLOCKED',evaluate([self.summary('Passed')],p)['status'])
 def test_postpublish_requires_actual_identity(self):
  s=self.summary();s['runs'][0]['results'][0]['name']=RELEASE_ID;p={'tests':[{'name':RELEASE_ID,'category':'required_automatic','reason':'actual release required'}]}
  self.assertEqual('BLOCKED',evaluate([s],p,True)['status'])
  release=copy.deepcopy(s);release['runs'][0]['results'][0]['outcome']='Passed';release['runs'][0]['errors']=[]
  self.assertEqual('REVIEW_BUILD_WITH_OPEN_GATES',evaluate([s,release],p,True)['status']);self.assertEqual('NotExecuted',s['runs'][0]['results'][0]['outcome'])
if __name__=='__main__' :unittest.main()
