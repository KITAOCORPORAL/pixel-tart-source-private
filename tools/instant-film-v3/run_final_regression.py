"""Wait for real evidence production / released WPF; then measure Core without concurrent suites."""
import pathlib,json,time,subprocess,os,sys
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/v3-product';started=time.monotonic()
while True:
 manifest=R/'artifacts/current-run-visual/current-run-visual-evidence.json';paths=list((A/'07-published-wpf').glob('run-*/summary.json'))
 finished=bool(paths) and json.loads(paths[0].read_text())['status']!='RUNNING'
 if manifest.exists() and finished:break
 if time.monotonic()-started>1800:raise TimeoutError('No completed evidence/suite, do not fabricate results')
 time.sleep(5)
env=os.environ.copy();env['PIXEL_TART_BATCH12_RELEASE_CORE']=str(A/'07-published-tests/core/RAWSelectionAssistant.Core.dll');env['KITAO_P3_TRACE']=str(A/'07-published-core-trace');cmd=[sys.executable,'tools/planning-r7/verified_runner.py','--assembly',str(A/'07-published-tests/core/RAWSelectionAssistant.Tests.dll'),'--output',str(A/'07-published-core'),'--dotnet','C:/Program Files/dotnet/dotnet.exe','--sdk','C:/Program Files/dotnet/sdk/10.0.401','--single-process','--resource-root',str(R)]
with (A/'07-published-core.log').open('w') as f:r=subprocess.run(cmd,env=env,stdout=f,stderr=subprocess.STDOUT,timeout=400)
print('published Core exit',r.returncode,flush=True)
cmd=['C:/Program Files/dotnet/dotnet.exe','test','tests/RAWSelectionAssistant.DpiTests/RAWSelectionAssistant.DpiTests.csproj','-c','Release','--no-build','--logger','trx;LogFileName=Dpi-current-producer.trx','--results-directory',str(A/'07-dpi-current')]
with (A/'07-dpi-current.log').open('w') as f:r=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,timeout=120)
print('DPI current producer exit',r.returncode,flush=True)
