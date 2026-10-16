"""Only exact admitted copies; scoped child OMP settings never change caller/global state."""
import argparse,hashlib,json,os,pathlib,subprocess,time
p=argparse.ArgumentParser();p.add_argument('--root',type=pathlib.Path,required=True);a=p.parse_args();root=a.root.resolve();out=root/'artifacts/followup';audit=root/'tools/PhotoEngineFollowupAudit/bin/Release/net10.0-windows10.0.19041.0/win-x64';dotnet='C:/Program Files/dotnet/dotnet.exe'
runs=[]
for camera,threads,repeats in [('Fuji',None,2),('Fuji','4',2),('Fuji','1',2),('Canon',None,2)]:
 source=out/'authorized'/('Fuji_DSCF0347.RAF' if camera=='Fuji' else 'Canon_TNAN8886.CR3')
 for repeat in range(1,repeats+1):
  label=f'02-{camera.lower()}-{threads or "inherited"}-{repeat}';dest=out/label
  if dest.exists():raise RuntimeError('no overwrite '+label)
  env=os.environ.copy()
  if threads is not None:env['OMP_NUM_THREADS']=threads
  cmd=[dotnet,str(audit/'PhotoEngineFollowupAudit.dll'),'native-stages',str(source),str(audit/'raw_r.dll'),str(dest),'0'];started=time.perf_counter()
  with (out/(label+'.log')).open('w') as log:r=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT,env=env,timeout=180,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
  report=json.loads((dest/'result.json').read_text(encoding='utf-8-sig'));runs.append(dict(label=label,exitCode=r.returncode,elapsedSeconds=time.perf_counter()-started,outputSha=report.get('outputSha'),status=report['status'],threads=report.get('OMP_NUM_THREADS')));print(runs[-1],flush=True)
(out/'02-repeat-runs.json').write_text(json.dumps(runs,indent=2)+'\n')
