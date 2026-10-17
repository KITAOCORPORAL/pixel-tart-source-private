"""Run one bounded audit in a separate process; record native Windows process counters."""
import argparse,ctypes,json,os,pathlib,subprocess,time
from ctypes import wintypes
class Counters(ctypes.Structure):
 _fields_=[("cb",wintypes.DWORD),("PageFaultCount",wintypes.DWORD)]+[(n,ctypes.c_size_t) for n in ["PeakWorkingSetSize","WorkingSetSize","QuotaPeakPagedPoolUsage","QuotaPagedPoolUsage","QuotaPeakNonPagedPoolUsage","QuotaNonPagedPoolUsage","PagefileUsage","PeakPagefileUsage","PrivateUsage"]]
def main():
 a=argparse.ArgumentParser();a.add_argument("--output",type=pathlib.Path,required=True);a.add_argument("--log",type=pathlib.Path,required=True);a.add_argument("command",nargs=argparse.REMAINDER);args=a.parse_args();args.output.parent.mkdir(parents=True,exist_ok=True)
 if args.output.exists():raise FileExistsError(args.output)
 env=os.environ.copy();env["KITAO_TEST_RESOURCE_ROOT"]=str(pathlib.Path.cwd());t=time.perf_counter();samples=[]
 kernel=ctypes.WinDLL("kernel32",use_last_error=True);kernel.OpenProcess.restype=wintypes.HANDLE;kernel.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];kernel.CloseHandle.argtypes=[wintypes.HANDLE];psapi=ctypes.WinDLL("psapi",use_last_error=True);psapi.GetProcessMemoryInfo.argtypes=[wintypes.HANDLE,ctypes.POINTER(Counters),wintypes.DWORD]
 with args.log.open("w",encoding="utf-8") as log:
  p=subprocess.Popen(args.command,stdout=log,stderr=subprocess.STDOUT,env=env);handle=kernel.OpenProcess(0x410,False,p.pid)
  if not handle:raise ctypes.WinError(ctypes.get_last_error())
  try:
   while p.poll() is None:
    m=Counters();m.cb=ctypes.sizeof(m)
    if psapi.GetProcessMemoryInfo(handle,ctypes.byref(m),m.cb):samples.append(dict(seconds=time.perf_counter()-t,private=m.PrivateUsage,workingSet=m.WorkingSetSize,peakWorkingSet=m.PeakWorkingSetSize))
    if time.perf_counter()-t>1200:p.kill();raise TimeoutError("Audit exceeded 1200 seconds")
    time.sleep(.25)
   code=p.wait()
  finally:kernel.CloseHandle(handle)
 args.output.write_text(json.dumps(dict(exitCode=code,seconds=time.perf_counter()-t,sampleSeconds=.25,samples=samples),indent=2));print(code,len(samples));return code
if __name__=="__main__":raise SystemExit(main())
