"""Guarded visible-class TIFF cycle, independent reopen, decoded-pixel and metadata checks."""
from pathlib import Path
import argparse, hashlib, json, os, subprocess, sys
p=argparse.ArgumentParser();p.add_argument('--app',type=Path,required=True);p.add_argument('--input-task',type=Path,required=True);p.add_argument('--name',required=True);a=p.parse_args()
repo=Path(__file__).resolve().parents[2];root=repo/'artifacts/tiff-memory-20261115';guard=repo/'tools/ProductMemoryIntegrationReplay/run_guarded.py';app=a.app.resolve();task_path=a.input_task.resolve()
def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))
def save(path,value):path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def sha(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
task=read(task_path);source=Path(task['inputPath']);assert task['maximumNewRawDecodes']==0 and task['guardBytes']==2**31
case=root/a.name;case.mkdir(exist_ok=False)
def run(path,destination,mode,approval,env=None):
    subprocess.run([sys.executable,str(guard),str(app),str(path),str(destination),mode,'--input-task',str(approval)],check=True,env=env)
run(source,case/'first','rendered-tiff-before',task_path)
first=read(case/'first/RENDERED_TIFF_RESULT.json');env=os.environ.copy();env['KITAO_TIFF_REOPEN_SESSION']=first['session']
run(source,case/'reopen','rendered-tiff-reopen',task_path,env)
reopen=read(case/'reopen/RENDERED_TIFF_RESULT.json')
probes={}
for label,path in [('input',source),('first',Path(first['output'])),('reopen',Path(reopen['output']))]:
    approval=case/(label+'-probe-task.json');save(approval,{**task,'inputPath':str(path),'fileSha256':sha(path)})
    run(path,case/(label+'-probe'),'probe-tiff',approval)
    probes[label]=read(case/(label+'-probe')/'TIFF_PROBE.json')
x,y,z=probes['first'],probes['reopen'],probes['input']
checks={key:x[key]==y[key] for key in ['width','height','bitsPerPixel','storedPixelFormat','storedPixelSha256','rgb48PixelSha256','iccProfiles','orientation']}
checks.update(inputNormalizedPixelsMatch=x['rgb48PixelSha256']==z['normalizedRgb48PixelSha256'],inputNormalizedSizeMatch=[x['width'],x['height']]==[z['normalizedWidth'],z['normalizedHeight']],outputOrientationNormalized=x['orientation']==y['orientation']==1,channelBitDepth16=x['bitsPerPixel']==y['bitsPerPixel']==64 and z['bitsPerPixel'] in [48,64],iccMatchesInput=x['iccProfiles']==z['iccProfiles'],independentProcess=first['processId']!=reopen['processId'],newRawDecodesZero=first['rawDecoderRequests']==reopen['rawDecoderRequests']==0,sourceUnchanged=sha(source)==task['fileSha256'])
stages={};guard_results={};viewports={}
for label in ['first','reopen']:
    folder=case/label;g=case/(label+'-guard');result=read(g/'EXIT.json');guard_results[label]=result
    checks[label+'GuardPassed']=result['exitCode']==0 and result['stop'] is None and result['peakPrivateBytes50ms']<=2**31 and result['osPeakWorkingSetBytes']<=2**31
    events=[json.loads(line) for line in (folder/'events.jsonl').read_text().splitlines()]
    snapshots={e['value']['name']:e['value'] for e in events if e['kind']=='MemorySnapshot'}
    viewports[label]={name:snapshots[name]['holders'].get('Viewport.Layout') for name in ['rendered-fit-idle','rendered-100-idle','rendered-20-idle','rendered-100-return-idle']}
    checks[label+'ViewportVisible']=all(v and v['visible'] and v['loaded'] and v['windowVisible'] and v['width']>0 and v['height']>0 for v in viewports[label].values())
    checks[label+'ActualSizeZoom']=viewports[label]['rendered-100-idle']['zoom']==viewports[label]['rendered-100-return-idle']['zoom']==1
    checks[label+'FitZoom']=0<viewports[label]['rendered-fit-idle']['zoom']<1
    checks[label+'SavedClosed']= (folder/'rendered.ptstudio.json').is_file() if label=='first' else first['session']==reopen['session']
    by_phase={}
    for line in (g/'RESOURCE_SAMPLES.jsonl').read_text().splitlines():
        sample=json.loads(line);entry=by_phase.setdefault(sample.get('phase','unknown'),{'peakPrivateBytes':0,'peakWorkingSetBytes':0,'samples':0})
        entry['peakPrivateBytes']=max(entry['peakPrivateBytes'],sample['privateBytes']);entry['peakWorkingSetBytes']=max(entry['peakWorkingSetBytes'],sample['workingSetBytes']);entry['samples']+=1
    stages[label]=by_phase
result={'status':'PASS_CONTROLLED_VISIBLE_TIFF_CYCLE' if all(checks.values()) else 'FAIL','authority':'Actual WPF visible PhotoGradePageView plus collapsed legacy view, production commands and real TIFF files; fixed dialogs, offscreen window; no manual desktop/file-picker acceptance.','inputDifference':task.get('inputDifference'),'checks':checks,'probes':probes,'first':first,'reopen':reopen,'guardBytes':2**31,'guardResults':guard_results,'stagePeaks50ms':stages,'viewports':viewports,'newRawDecodes':0,'highlightResearchChanged':False}
save(case/'COMPARISON.json',result);print(json.dumps({'case':a.name,'status':result['status'],'failed':[k for k,v in checks.items() if not v],'peakMiB':{k:round(v['peakPrivateBytes50ms']/1048576,2) for k,v in guard_results.items()}},ensure_ascii=False))
if not all(checks.values()):raise SystemExit(1)
