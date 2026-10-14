"""Separate review gate; never mutates strict summary or test outcomes."""
import json,collections,argparse,pathlib
RELEASE_ID='RAWSelectionAssistant.Tests.Batch12ReleasedAssemblyTests.ReleasedCoreContainsStrictImporterAndKeepsCreativeFilmEvidence'
def evaluate(summaries,policy,postpublish=False):
 rules={x['name']:x for x in policy['tests']};errors=[];open_gates=[];deferred=[];seen=set()
 identity_passed=any(v['name']==RELEASE_ID and v['outcome']=='Passed' and not r.get('timeout') and r.get('exitCode')==0 and not r.get('errors') for s in summaries for r in s['runs'] for v in r['results'])
 if postpublish and not identity_passed:errors.append('required actual release identity evidence missing')
 if len(rules)!=len(policy['tests']):errors.append('duplicate policy name')
 for summary in summaries:
  for run in summary['runs']:
   if run.get('timeout'):errors.append('timeout: '+run['class'])
   if any(e!='non-passing or not-executed tests' for e in run.get('errors',[])):errors.append({'infrastructure':run['class'],'errors':run['errors']})
   if run.get('exitCode',0)!=0:errors.append('nonzero process: '+run['class'])
   if len(run['results'])!=run['expected']:errors.append('count mismatch: '+run['class'])
   for result in run['results']:
    name=result['name'];seen.add(name);rule=rules.get(name)
    if rule is None:errors.append('unclassified test: '+name);continue
    if result['outcome']=='Passed':continue
    if result['outcome']!='NotExecuted':errors.append({'failed':name,'outcome':result['outcome']});continue
    if name==RELEASE_ID and not postpublish:deferred.append(name);continue
    if name==RELEASE_ID and postpublish and identity_passed:continue # Separate actual-payload result satisfies deferred dependency; raw skip is unchanged.
    if rule['category']=='required_automatic':errors.append('required not executed: '+name)
    else:open_gates.append({'name':name,'classification':rule['category'],'reason':rule['reason'],'outcome':result['outcome']})
  if sum(len(r['results']) for r in summary['runs'])!=summary['selected']:errors.append('selected total mismatch')
 return {'status':'BLOCKED' if errors else 'REVIEW_BUILD_WITH_OPEN_GATES','errors':errors,'openGates':open_gates,'postpublishRequired':deferred,'tested':len(seen),'strictUnchanged':True,'manual':{'Windows':'NOT_RUN','physicalDpi':'NOT_RUN','WPS':'NOT_RUN'}}
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--summary',nargs='+',required=True);a.add_argument('--policy',required=True);a.add_argument('--output',required=True);a.add_argument('--postpublish',action='store_true');args=a.parse_args()
 r=evaluate([json.loads(pathlib.Path(p).read_text(encoding='utf-8-sig')) for p in args.summary],json.loads(pathlib.Path(args.policy).read_text(encoding='utf-8-sig')),args.postpublish);pathlib.Path(args.output).write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8');print(r['status'],len(r['errors']),len(r['openGates']));raise SystemExit(bool(r['errors']))
