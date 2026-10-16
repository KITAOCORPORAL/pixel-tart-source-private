"""Run final Core suites only after both WPF suites finish; preserve every result, no green selection."""
import json,pathlib,time,subprocess,sys,os
root=pathlib.Path.cwd();out=root/'artifacts/followup';started=time.monotonic()
for suite in ['06-source-wpf','06-published-wpf']:
 while True:
  paths=list((out/suite).glob('run-*/summary.json'))
  if len(paths)!=1:raise RuntimeError('ambiguous WPF run')
  s=json.loads(paths[0].read_text(encoding='utf-8-sig'))
  if s['status']!='RUNNING':print(suite,s['status'],s['counts'],flush=True);break
  if time.monotonic()-started>2400:raise TimeoutError('WPF suites incomplete; no Core results fabricated')
  time.sleep(3)
rows=[]
for suite,assembly in [('06-source-core','tests/RAWSelectionAssistant.Tests/bin/x64/Release/net10.0/RAWSelectionAssistant.Tests.dll'),('06-published-core','artifacts/followup/06-published-tests/core/RAWSelectionAssistant.Tests.dll')]:
 env=os.environ.copy();env['KITAO_P3_TRACE']=str(out/(suite+'-trace'));env.pop('PIXEL_TART_BATCH12_RELEASE_CORE',None)
 if suite=='06-published-core':env['PIXEL_TART_BATCH12_RELEASE_CORE']=str(out/'06-published-tests/core/RAWSelectionAssistant.Core.dll')
 cmd=[sys.executable,'tools/planning-r7/verified_runner.py','--assembly',assembly,'--output',str(out/suite),'--dotnet','C:/Program Files/dotnet/dotnet.exe','--sdk','C:/Program Files/dotnet/sdk/10.0.401','--single-process','--resource-root',str(root)]
 with (out/(suite+'.log')).open('w') as log:p=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT,env=env,timeout=600,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
 s=json.loads(next((out/suite).glob('run-*/summary.json')).read_text());rows.append(dict(suite=suite,exitCode=p.returncode,status=s['status'],counts=s['counts'],discovered=s['discovered'],selected=s['selected']));print(rows[-1],flush=True)
(out/'06-final-core-driver.json').write_text(json.dumps(rows,indent=2)+'\n')
