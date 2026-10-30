"""One admitted20MP invocation; explicit home configuration, no assert gates."""
from pathlib import Path
import argparse,json,subprocess,sys,os
if '--environment' in sys.argv:
 index=sys.argv.index('--environment')
 if index+1>=len(sys.argv):raise ValueError('Explicit environment path required')
 os.environ['COLOR_RESEARCH_ENV']=str(Path(sys.argv[index+1]).resolve())
from natural_admission import sha,load_json,atomic_json,output_path,need,npy_info
from host_environment import environment

def admitted(gate,controls):
 cases=gate.get('cases',[]);need(len(cases)>=10 and all(c.get('success') and c.get('oracle',{}).get('wholeVsStreamExact') and c.get('wholeCallerWithinCap') for c in cases),'Natural small exact/host gates not passed')
 faults=controls.get('cases',[]);need(len(faults)>=10 and all(not c.get('success') and c.get('native',{}).get('deviceLiveAfter')==c.get('native',{}).get('stagingLiveAfter')==0 for c in faults),'Native control gates not passed')
 need(gate.get('syntheticPassed') is True and gate.get('consumerPassed') is True and controls.get('concurrencyPassed') is True,'Additional small gates incomplete')
 return len(cases),len(faults)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--environment',type=Path,required=True);p.add_argument('--gates',type=Path,required=True);p.add_argument('--controls',type=Path,required=True);p.add_argument('--input',type=Path,required=True);p.add_argument('--manifest',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();host=environment(a.environment);n,f=admitted(load_json(a.gates),load_json(a.controls));dest=output_path(a.output);need(not dest.exists(),'Full output exists');need(npy_info(a.input)['shape']==[3670,5496,3],'Single full must use frozen5496x3670 input');script=Path(__file__).with_name('stream_case.py');cmd=[sys.executable,str(script),str(a.input),str(dest),'--manifest',str(a.manifest),'--manifest-sha',sha(a.manifest),'--library',host['streamLibrary'],'--resources',host['resources'],'--rows','127','--consumer-rows','7','--device-mib','256','--staging-mib','64','--host-mib','512'];inv=dest.with_name(dest.name+'-invocation.json');need(not inv.exists(),'This full invocation was already recorded');atomic_json(inv,{'version':'HomeSingle20MPInvocationV2','environment':str(a.environment),'gatesHash':sha(a.gates),'controlsHash':sha(a.controls),'naturalCases':n,'faultCases':f,'argv':cmd,'fullCallsRequested':1});childenv=os.environ.copy();childenv['COLOR_RESEARCH_ENV']=str(a.environment.resolve());proc=subprocess.run(cmd,capture_output=True,text=True,env=childenv);dest.with_name(dest.name+'-console.txt').write_text(proc.stdout+proc.stderr);print(proc.stdout+proc.stderr);r=load_json(dest/'report.json');raise SystemExit(0 if proc.returncode==0 and r.get('success') else 3)
