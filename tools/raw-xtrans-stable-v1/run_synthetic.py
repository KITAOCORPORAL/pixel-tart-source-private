# Synthetic algorithm only; no RAW/natively loaded decoder and no automatic retry.
from pathlib import Path
import sys,os,json,subprocess,time,hashlib
build=Path(sys.argv[1]).resolve();compiler_bin=Path(sys.argv[2]).resolve();here=Path(__file__).resolve();workspace=here.parents[3];sys.path.insert(0,str(workspace/'ColorResearchHome-20261030/deps/python-frozen'));import psutil
marker=build/'SYNTHETIC_RUN_RESERVED.json'
with marker.open('x') as f:json.dump({'rawCalls':0,'mode':'noRAW synthetic','maxRssBytes':2147483648,'timeoutSeconds':240,'autoRetry':False},f)
env=os.environ.copy();env['PATH']=str(compiler_bin)+os.pathsep+env['PATH'];cmd=[str(build/'synthetic.exe'),str(build/'results')];start=time.perf_counter();peak=0;failure=None
with (build/'run.stdout').open('x') as out,(build/'run.stderr').open('x') as err:
 child=subprocess.Popen(cmd,env=env,stdout=out,stderr=err);proc=psutil.Process(child.pid)
 while child.poll() is None:
  try:peak=max(peak,proc.memory_info().rss)
  except psutil.Error:pass
  if peak>2147483648 or time.perf_counter()-start>240:
   failure='SYNTHETIC_BUDGET_EXCEEDED';child.kill();break
  time.sleep(.02)
 code=child.wait()
receipt={'version':'SyntheticActualInvocationV1','command':cmd,'exitCode':code,'failure':failure,'elapsedMs':(time.perf_counter()-start)*1000,'sampledRssPeakBytes':peak,'executableSha256':hashlib.sha256((build/'synthetic.exe').read_bytes()).hexdigest(),'rawCalls':0,'productionDecoderCalls':0}
with (build/'RUN_RECEIPT.json').open('x') as f:json.dump(receipt,f,indent=2)
print(json.dumps(receipt));print((build/'run.stdout').read_text());print((build/'run.stderr').read_text());sys.exit(code if code else (1 if failure else 0))
