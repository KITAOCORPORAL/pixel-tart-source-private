"""Actual Ctrl-Break cancellation in a new Windows process group, explicit D80014 only."""
import json,subprocess,time,signal,sys
from pathlib import Path
source=Path('N:/pixart/RAW/Real/nikon/RAW_NIKON_D800_14bit_FX_LOSSLESS.NEF');sha='593E1B0EE73EF7E699D73465E3BD8222AD0ED79BEB69E9265E440756D4B50A1A';root=Path('N:/pixart/ColorResearchHome-20261030');out=root/'d80014-native-cancel';dll=Path(__file__).parent/'bin/Debug/net10.0/ResearchDecoder.dll';log=root/'evidence/d80014-cancel-console.txt';report={'version':'D80014ActualCancelAttemptV1','sourceSha256':sha,'success':False}
if out.exists():raise ValueError('Cancel output already exists')
with log.open('x',encoding='utf-8') as f:
 p=subprocess.Popen(['dotnet',str(dll),str(source),sha,str(out),'matrix'],stdout=f,stderr=f,creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
 try:
  start=time.monotonic()
  while not out.exists() and p.poll() is None and time.monotonic()-start<10:time.sleep(.01)
  time.sleep(.15);p.send_signal(signal.CTRL_BREAK_EVENT);report['signalSent']=True;rc=p.wait(30);report['exitCode']=rc
 except BaseException as ex:
  report['primaryError']=repr(ex)
  # Do not terminate a possibly GPU/native operation abruptly; wait for completion.
  try:report['exitCode']=p.wait(60)
  except BaseException as cleanup:report['cleanupError']=repr(cleanup)
text=log.read_text();report['operationCanceledObserved']='OperationCanceledException' in text;report['recipeExists']=(out/'recipe.json').exists();report['success']=report.get('signalSent',False) and report.get('exitCode')!=0 and report['operationCanceledObserved'] and not report['recipeExists'];(root/'evidence/d80014-cancel.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2));sys.exit(0 if report['success'] else 3)
