import pathlib,subprocess,threading,time,json,argparse,os
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);p.add_argument('--wpf',required=True);p.add_argument('--probe',required=True);p.add_argument('--fixture',required=True);p.add_argument('--out',required=True);a=p.parse_args();out=pathlib.Path(a.out);out.mkdir(parents=True,exist_ok=False);stop=threading.Event();events=[];gate=threading.Barrier(5)
classes=['RuntimeCorrectionWpfTests','ReferenceWorkspaceWideRatioTests','PlanningBatch3BusinessTests','PhotoGradePageV1Tests'];env=os.environ.copy()
def worker(index,name):
 gate.wait();round=0
 while not stop.is_set():
  folder=out/f'load-{index}-{round}';folder.mkdir();local=env.copy();local.update({'PIXEL_TART_HUMAN_ACCEPTANCE':'1','PIXEL_TART_ACCEPTANCE_ROOT':str(folder/'runtime'),'PIXEL_TART_ISOLATED_RUNTIME':'1','PIXEL_TART_ISOLATED_RUNTIME_ROOT':str(folder/'runtime'),'KITAO_TEST_EVIDENCE_ROOT':str(folder/'evidence')})
  cmd=[a.dotnet,'vstest',a.wpf,'/TestCaseFilter:FullyQualifiedName~RAWSelectionAssistant.WpfTests.'+name,'/Logger:trx;LogFileName=load.trx','/ResultsDirectory:'+str(folder)]
  start=time.time()
  with (folder/'stdout.log').open('wb') as log:
   proc=subprocess.Popen(cmd,stdout=log,stderr=log,env=local,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));timeout=False
   try:code=proc.wait(timeout=180)
   except subprocess.TimeoutExpired:
    timeout=True;subprocess.run(['taskkill','/PID',str(proc.pid),'/T','/F'],stdout=log,stderr=log,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));code=proc.wait(timeout=20)
  events.append({'worker':index,'round':round,'class':name,'pid':proc.pid,'startEpoch':start,'endEpoch':time.time(),'exit':code,'timeout':timeout});round+=1
threads=[threading.Thread(target=worker,args=(i,n)) for i,n in enumerate(classes)]
for t in threads:t.start()
gate.wait();time.sleep(1.0)
start=time.time();cmd=[a.dotnet,a.probe,str(out/'measurement'),'warm3',a.fixture]
with (out/'measurement.log').open('wb') as log:r=subprocess.run(cmd,stdout=log,stderr=log,env=env,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0),timeout=180)
end=time.time();stop.set()
for t in threads:t.join()
(out/'load.json').write_text(json.dumps({'classes':classes,'measurementCommand':cmd,'measurementStartEpoch':start,'measurementEndEpoch':end,'measurementExit':r.returncode,'events':events,'note':'Fixed four WPF workers, identical plan before and after. No unrelated data. Individual TRX retained; load is not acceptance result.'},indent=2),encoding='utf-8');print(json.dumps({'exit':r.returncode,'workers':len(threads),'events':len(events)}))
