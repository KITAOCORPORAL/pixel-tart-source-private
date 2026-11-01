import argparse,json
from pathlib import Path
from package_validation import validate_package
from compare_rows import compare
p=argparse.ArgumentParser();p.add_argument('left',type=Path);p.add_argument('right',type=Path);p.add_argument('output',type=Path);p.add_argument('--left-role',choices=['home','company'],required=True);p.add_argument('--right-role',choices=['home','company'],required=True);p.add_argument('--allow-sealed-round10',action='store_true');a=p.parse_args()
if a.output.exists():raise ValueError('Output exists')
l,lv=validate_package(a.left,a.left_role,a.allow_sealed_round10);r,rv=validate_package(a.right,a.right_role,a.allow_sealed_round10)
report={'version':'IndependentCrossDeviceComparisonV2','leftValidation':lv,'rightValidation':rv,'purpose':'measure and localize only; no cross-device PASS threshold','btDecode':compare(a.left/'bt-decode.f32',a.right/'bt-decode.f32',[1,13,1],'<f4'),'cases':[],'firstDivergentStage':None}
for x,y in zip(l['cases'],r['cases']):
 folder=x['id'];out={'id':folder,'whole':compare(a.left/folder/'whole.rgba16',a.right/folder/'whole.rgba16',x['shape'],'<u2'),'stream':[],'stages':[]};report['cases'].append(out)
 for rows in [1,7,17,64]:out['stream'].append({'rows':rows,**compare(a.left/folder/f'stream-r{rows}.rgba16',a.right/folder/f'stream-r{rows}.rgba16',x['shape'],'<u2')})
 for key in ['stageWhole','stageStream']:
  for index,(u,v) in enumerate(zip(x[key],y[key])):
   d={'kind':key,'index':index,'stage':u['stage'],**compare(a.left/u['file'],a.right/v['file'],u['shape'],'<f4')};out['stages'].append(d)
   if key=='stageWhole' and d['changedChannels'] and report['firstDivergentStage'] is None:report['firstDivergentStage']={'case':folder,**d}
with a.output.open('x') as f:json.dump(report,f,indent=2,allow_nan=False)
print(json.dumps({'report':str(a.output),'firstDivergentStage':report['firstDivergentStage'],'leftRole':a.left_role,'rightRole':a.right_role}))
