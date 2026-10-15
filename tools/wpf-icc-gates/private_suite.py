"""Run adapter-discovered WPF on a private station. No physical input/desktop switch."""
import argparse,pathlib,ctypes,json,subprocess,sys
p=argparse.ArgumentParser();p.add_argument('--assembly',required=True);p.add_argument('--output',required=True);p.add_argument('--resource-root',required=True);p.add_argument('--classes',nargs='*');a=p.parse_args();R=pathlib.Path(__file__).resolve().parents[2];out=pathlib.Path(a.output);out.mkdir(parents=True,exist_ok=True)
u=ctypes.WinDLL('user32');u.GetProcessWindowStation.restype=ctypes.c_void_p;h=u.GetProcessWindowStation();b=ctypes.create_unicode_buffer(256);n=ctypes.c_uint();assert u.GetUserObjectInformationW(ctypes.c_void_p(h),2,b,512,ctypes.byref(n));assert b.value.startswith('KitaoTests_'), 'Refuse to run foreground-capable suite on interactive station '+b.value
(out/'station.json').write_text(json.dumps({'station':b.value,'physicalInput':False,'inputDesktopSwitched':False}))
cmd=[sys.executable,str(R/'tools/planning-r7/verified_runner.py'),'--assembly',a.assembly,'--output',str(out/'results'),'--resource-root',a.resource_root,'--timeout','90']
if a.classes:cmd+=['--classes']+a.classes
with (out/'runner.log').open('wb') as log:result=subprocess.run(cmd,stdout=log,stderr=log,timeout=10800)
raise SystemExit(result.returncode)
