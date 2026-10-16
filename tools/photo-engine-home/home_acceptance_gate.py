# Home acceptance gate is stricter than a successful build or legacy test allowlist.
import argparse,json,pathlib

def evaluate(review,products,repeatability,reference):
 errors=[]
 if review.get('status')=='BLOCKED' or review.get('errors'):errors.append({'strictReview':review.get('errors',[])})
 for name,result in products.items():
  if result.get('status')!='COMPLETED_PRODUCT_BACKEND_NOT_DESKTOP':errors.append({'productProbe':name,'status':result.get('status')})
  if not result.get('sourceSha256') or result.get('sourceSha256')!=result.get('isolatedCopySha256'):errors.append({'sourceIdentity':name})
  for row in result.get('rows',[]):
   if row.get('status')=='FAILED':errors.append({'failedProductOperation':name,'detail':row})
 if repeatability.get('changedChannelSamples',1)!=0:errors.append({'requiredProductRepeatability':'Fuji default legacy output differs for identical source','changedChannelSamples':repeatability.get('changedChannelSamples'),'maximum16BitCodeDifference':repeatability.get('maximum16BitCodeDifference')})
 if reference.get('status')!='PASSED_REFERENCE_SUBSET':errors.append({'iccReference':reference.get('status')})
 return {'status':'BLOCKED' if errors else 'REVIEW_BUILD_WITH_OPEN_GATES','errors':errors,'legacyOpenConditionsPreserved':True,'nativeDesktop':'NOT_RUN','physicalDpi':'NOT_RUN','candidateDistributionApproved':False,'monitorPixelConversionImplemented':False}
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--review',type=pathlib.Path,required=True);p.add_argument('--product',type=pathlib.Path,nargs='+',required=True);p.add_argument('--repeatability',type=pathlib.Path,required=True);p.add_argument('--reference',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
 load=lambda path:json.loads(path.read_text(encoding='utf-8-sig'));r=evaluate(load(a.review),{str(x):load(x) for x in a.product},load(a.repeatability),load(a.reference));a.output.write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8');print(r['status'],len(r['errors']));raise SystemExit(bool(r['errors']))
