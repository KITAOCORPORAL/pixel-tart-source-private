import unittest,copy
from home_acceptance_gate import evaluate
class HomeGateTests(unittest.TestCase):
 def test_repeated_output_inconsistency_blocks_even_when_regressions_pass(self):
  review={'status':'REVIEW_BUILD_WITH_OPEN_GATES','errors':[]};product={'canon':{'status':'COMPLETED_PRODUCT_BACKEND_NOT_DESKTOP','sourceSha256':'B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401','isolatedCopySha256':'B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401','rows':[]}};repeat={'changedChannelSamples':38375,'maximum16BitCodeDifference':11713};reference={'status':'PASSED_REFERENCE_SUBSET'};before=copy.deepcopy((review,product,repeat,reference));r=evaluate(review,product,repeat,reference);self.assertEqual('BLOCKED',r['status']);self.assertEqual(before,(review,product,repeat,reference));self.assertFalse(r['candidateDistributionApproved'])
 def test_only_known_product_and_reference_evidence_can_review(self):
  review={'status':'REVIEW_BUILD_WITH_OPEN_GATES','errors':[]};products={'canon':{'status':'COMPLETED_PRODUCT_BACKEND_NOT_DESKTOP','sourceSha256':'B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401','isolatedCopySha256':'B7D358517DB0CD5B5C54215AA089A5A30C79234AEF50E9381931128493E33401','rows':[]}};reference={'status':'PASSED_REFERENCE_SUBSET'};repeat={'changedChannelSamples':0};products['fuji']=copy.deepcopy(products['canon']);products['fuji']['sourceSha256']=products['fuji']['isolatedCopySha256']='52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865'
  self.assertEqual('REVIEW_BUILD_WITH_OPEN_GATES',evaluate(review,products,repeat,reference)['status'])
  for missing in [{},{'status':'FAILED'}]:self.assertEqual('BLOCKED',evaluate(review,products,repeat,missing)['status'])
  self.assertEqual('BLOCKED',evaluate(review,{},repeat,reference)['status'])
  self.assertEqual('BLOCKED',evaluate({},products,repeat,reference)['status'])
  self.assertEqual('BLOCKED',evaluate({'status':'BLOCKED','errors':['required missing']},products,repeat,reference)['status'])
if __name__=='__main__':unittest.main()
