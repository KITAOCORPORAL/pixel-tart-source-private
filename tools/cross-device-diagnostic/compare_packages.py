"""Compare two machine packages, align whole passive stages and small final rows."""
import argparse,json
from pathlib import Path
from compare_rows import compare
from generate_inputs import sha
p=argparse.ArgumentParser();p.add_argument('left',type=Path);p.add_argument('right',type=Path);p.add_argument('output',type=Path);a=p.parse_args();l=json.loads((a.left/'run.json').read_text(encoding='utf-8-sig'));r=json.loads((a.right/'run.json').read_text(encoding='utf-8-sig'))
if a.output.exists():raise ValueError('Comparison exists')
if l.get('version')!='SyntheticCrossDeviceRunV1' or r.get('version')!='SyntheticCrossDeviceRunV1' or l.get('privatePhotosIncluded') is not False or r.get('privatePhotosIncluded') is not False:raise ValueError('Unsupported synthetic run identity')
if l['btDiagnostic']['shape']!=[1,13,1] or r['btDiagnostic']['shape']!=[1,13,1] or l['btDiagnostic']['mode']!=3 or r['btDiagnostic']['mode']!=3 or l['btDiagnostic']['inputBits']!=r['btDiagnostic']['inputBits']:raise ValueError('BT domain/input mismatch')
if len(l.get('cases',[]))!=3 or len(r.get('cases',[]))!=3:raise ValueError('Missing/extra diagnostic case')
if len({c['id'] for c in l['cases']})!=3 or len({c['id'] for c in r['cases']})!=3:raise ValueError('Duplicate diagnostic case')
if l['syntheticManifestHash']!=r['syntheticManifestHash'] or l['resolved']!=r['resolved'] or l['resources']!=r['resources']:raise ValueError('Input/params/resource confound: align before numeric comparison')
if sha(a.left/'bt-decode.f32')!=l['btDiagnostic']['outputHash'] or sha(a.right/'bt-decode.f32')!=r['btDiagnostic']['outputHash']:raise ValueError('BT stage artifact changed')
report={'version':'CrossMachineSyntheticComparisonV1','purpose':'quantify and localize; NO cross-device pass threshold','leftGpu':l['gpu'],'rightGpu':r['gpu'],'sameInputParamsResources':True,'libraryHashes':[l['library'],r['library']],'cases':[],'firstDivergentStage':None,'btDecode':compare(a.left/'bt-decode.f32',a.right/'bt-decode.f32',l['btDiagnostic']['shape'],'<f4')}
for lc,rc in zip(l['cases'],r['cases']):
 if lc['id']!=rc['id'] or lc['shape']!=rc['shape']:raise ValueError('Case alignment mismatch')
 folder=lc['id']
 for case in [lc,rc]:
  if not case.get('wholeTraceUnchanged') or not case.get('streamTraceUnchanged') or not case.get('naturalEntrySyntheticRejected') or len(case['stageWhole'])!=12 or len(case['stageStream'])!=36:raise ValueError('Missing stage trace, natural guard or passive trace gate')
 if sha(a.left/folder/'whole.rgba16')!=lc['wholeInfo']['sha256'] or sha(a.right/folder/'whole.rgba16')!=rc['wholeInfo']['sha256']:raise ValueError('Whole result hash changed')
 for c,root in [(lc,a.left),(rc,a.right)]:
  if {x['rows'] for x in c['stream']}!={1,7,17,64} or len(c['stream'])!=4:raise ValueError('Missing/duplicate stream result')
  for record in c['stream']:
   file='stream-r'+str(record['rows'])+'.rgba16'
   if sha(root/folder/file)!=record['outputHash']:raise ValueError('Stream result hash changed')
 out={'id':folder,'whole':compare(a.left/folder/'whole.rgba16',a.right/folder/'whole.rgba16',lc['shape'],'<u2'),'stream':[],'stages':[],'streamStages':[]};report['cases'].append(out)
 for rows in [1,7,17,64]:out['stream'].append({'rows':rows,**compare(a.left/folder/f'stream-r{rows}.rgba16',a.right/folder/f'stream-r{rows}.rgba16',lc['shape'],'<u2')})
 if len(lc['stageWhole'])!=len(rc['stageWhole']):raise ValueError('Stage count differs')
 for x,y in zip(lc['stageWhole'],rc['stageWhole']):
  if x['stage']!=y['stage'] or x['shape']!=y['shape'] or x.get('stageDomain')!=y.get('stageDomain'):raise ValueError('Whole stage alignment/domain differs')
  lp=a.left/x['file'];rp=a.right/y['file']
  if sha(lp)!=x['sha256'] or sha(rp)!=y['sha256']:raise ValueError('Stage output altered')
  row={'stage':x['stage'],**compare(lp,rp,x['shape'],'<f4')};out['stages'].append(row)
  if row['changedChannels'] and report['firstDivergentStage'] is None:report['firstDivergentStage']={'case':folder,**row}
 if len(lc['stageStream'])!=len(rc['stageStream']):raise ValueError('Stream stage count differs')
 for index,(x,y) in enumerate(zip(lc['stageStream'],rc['stageStream'])):
  if x['stage']!=y['stage'] or x['shape']!=y['shape'] or x.get('stageDomain')!=y.get('stageDomain'):raise ValueError('Stream stage execution/band domain alignment differs')
  lp=a.left/x['file'];rp=a.right/y['file']
  if sha(lp)!=x['sha256'] or sha(rp)!=y['sha256']:raise ValueError('Stream stage artifact altered')
  out['streamStages'].append({'executionIndex':index,'stage':x['stage'],**compare(lp,rp,x['shape'],'<f4')})
with a.output.open('x') as f:json.dump(report,f,indent=2,allow_nan=False)
print(json.dumps({'cases':len(report['cases']),'firstDivergentStage':report['firstDivergentStage'],'report':str(a.output)}))
