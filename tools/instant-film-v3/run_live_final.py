import pathlib,json,time,subprocess,os,sys,shutil,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/v3-live';start=time.monotonic()
for suite in ['06-source-frozen-wpf','06-published-wpf']:
 while True:
  paths=list((A/suite).glob('run-*/summary.json'))
  if len(paths)==1:
   s=json.loads(paths[0].read_text())
   if s['status']!='RUNNING':print(suite,s['status'],s['counts'],flush=True);break
  if time.monotonic()-start>3600:raise TimeoutError('Still running, no fabricated Core results')
  time.sleep(5)
while not (R/'artifacts/current-run-visual/current-run-visual-evidence.json').exists():
 if time.monotonic()-start>4000:raise TimeoutError('Logical producer did not complete')
 time.sleep(5)
cmd=['C:/Program Files/dotnet/dotnet.exe','build','tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj','-c','Release','-p:Platform=x64','--artifacts-path','artifacts/v3-live/06-core-frozen-build']
with (A/'06-core-frozen-build.log').open('w') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,timeout=180)
assert r.returncode==0
source=A/'06-core-frozen-build/bin/RAWSelectionAssistant.Tests/release';host=A/'06-published-tests/core';shutil.copytree(source,host);release=A/'06-release/win-x64';shutil.copyfile(release/'RAWSelectionAssistant.Core.dll',host/'RAWSelectionAssistant.Core.dll')
for name,assembly in [('06-source-core',source/'RAWSelectionAssistant.Tests.dll'),('06-published-core',host/'RAWSelectionAssistant.Tests.dll')]:
 env=os.environ.copy();env['KITAO_P3_TRACE']=str(A/(name+'-trace'));env.pop('PIXEL_TART_BATCH12_RELEASE_CORE',None)
 if name=='06-published-core':env['PIXEL_TART_BATCH12_RELEASE_CORE']=str(host/'RAWSelectionAssistant.Core.dll')
 cmd=[sys.executable,'tools/planning-r7/verified_runner.py','--assembly',str(assembly),'--output',str(A/name),'--dotnet','C:/Program Files/dotnet/dotnet.exe','--sdk','C:/Program Files/dotnet/sdk/10.0.401','--single-process','--resource-root',str(R)]
 with (A/(name+'.log')).open('w') as f:r=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=600)
 s=json.loads(next((A/name).glob('run-*/summary.json')).read_text());print(name,s['status'],s['counts'],flush=True)
cmd=['C:/Program Files/dotnet/dotnet.exe','test','tests/RAWSelectionAssistant.DpiTests/RAWSelectionAssistant.DpiTests.csproj','-c','Release','--logger','trx;LogFileName=DpiLive.trx','--results-directory',str(A/'06-dpi')]
with (A/'06-dpi.log').open('w') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,timeout=180)
print('DPI',r.returncode,flush=True)
