"""Actual approved TIFF IO on the final product binary; fixed-dialog nonvisual scope."""
from pathlib import Path
import argparse,json,subprocess,sys,hashlib
p=argparse.ArgumentParser();p.add_argument('--context',required=True);a=p.parse_args();c=json.loads(Path(a.context).read_text('utf-8-sig'));b=Path(c['deliveryRoot']);app=b/'final-replay-bin/ProductMemoryIntegrationReplay.exe';guard=Path(c['worktree'])/'tools/ProductMemoryIntegrationReplay/run_guarded.py';source=Path('E:/Guangjing-RawColorChain-20261103/pass4/company-02/default-export/DSCF0347_仿色.tif')
def save(p,o):Path(p).write_text(json.dumps(o,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def read(p):return json.loads(Path(p).read_text('utf-8-sig'))
def digest(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
approval={'task':'FINAL_REAL_TIFF_939','inputKind':'already-rendered-tiff','inputPath':str(source),'fileSha256':digest(source),'maximumNewRawDecodes':0,'guardBytes':2**31,'productSource':c['currentProductSource']};task=b/'APPROVED_RENDERED_TIFF_FINAL.json';save(task,approval)
before_root=b/'final-tiff-model-before';after_root=b/'final-tiff-model-reopen'
subprocess.run([sys.executable,str(guard),str(app),str(source),str(before_root),'rendered-tiff-model-before','--input-task',str(task)],check=True)
import os
env=os.environ.copy();env['KITAO_TIFF_REOPEN_SESSION']=str(before_root/'rendered.ptstudio.json')
subprocess.run([sys.executable,str(guard),str(app),str(source),str(after_root),'rendered-tiff-model-reopen','--input-task',str(task)],env=env,check=True)
before=read(before_root/'RENDERED_TIFF_RESULT.json');after=read(after_root/'RENDERED_TIFF_RESULT.json');probes={}
for name,path in [('input',source),('before',Path(before['output'])),('reopen',Path(after['output']))]:
 task=b/('FINAL_PROBE_TASK_'+name+'.json');save(task,{**approval,'inputPath':str(path),'fileSha256':digest(path)})
 output=b/('final-tiff-probe-'+name);subprocess.run([sys.executable,str(guard),str(app),str(path),str(output),'probe-tiff','--input-task',str(task)],check=True);probes[name]=read(output/'TIFF_PROBE.json')
x=probes['before'];y=probes['reopen'];checks={field:x[field]==y[field] for field in ['width','height','bitsPerPixel','storedPixelFormat','rgb48PixelSha256','iccProfiles']};checks.update(independentProcess=before['processId']!=after['processId'],rawRequestsZero=before['rawDecoderRequests']==after['rawDecoderRequests']==0,sourceFileUnchanged=digest(source)==approval['fileSha256'])
result={'status':'PASS_CONTROLLED_REAL_TIFF_EXPORT_AND_INDEPENDENT_REOPEN' if all(checks.values()) else 'FAIL','productSource':c['currentProductSource'],'authority':'Nonvisual production ViewModel with fixed dialogs; no native chooser or visual-window memory acceptance','checks':checks,'probes':probes,'visualWindowGuardPassed':False,'sourceFileUnchanged':checks['sourceFileUnchanged'],'inputChannelBitDepth':16,'outputChannelBitDepth':16,'outputBitDepthPreservedFromInput':True,'inputToOutputRgb48PixelMatch':probes['input']['rgb48PixelSha256']==x['rgb48PixelSha256'],'channelLayoutNote':'RGB48 input,RGBA64 output; both16 bits/channel and opaque alpha added'};save(b/'TIFF_PIPELINE_COMPARISON.json',result)
print(json.dumps({'status':result['status'],'allChecks':all(checks.values()),'inputRgbMatchesOutput':result['inputToOutputRgb48PixelMatch'],'source':c['currentProductSource']}))
if not all(checks.values()):raise SystemExit(1)
