from pathlib import Path
import json, os, re, subprocess, sys, time
root = Path.cwd()
out = Path(os.environ.get("KITAO_CAPTURE_DELIVERY", "E:/Kitao-Deliveries/chrome-capture-v1-20261110"))
out.mkdir(exist_ok=True, parents=True)
dotnet = re.search(r'dotnet="([^"]+)"', (root/"tools/whole-app-followup/build_current.py").read_text(encoding="utf-8-sig")).group(1)
label, *args = sys.argv[1:]
assert not (out/(label+".json")).exists(), "Preserve previous build receipt"
env = os.environ.copy(); env["DOTNET_ROOT"] = str(Path(dotnet).parent); env["PATH"] = str(Path(dotnet).parent)+os.pathsep+env.get("PATH", "")
start = time.time()
with (out/(label+".log")).open("wb") as log:
    process = subprocess.Popen([dotnet, *args, "--disable-build-servers", "-m:1", "-nr:false"], env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
    code = process.wait()
record = dict(sourceSha=subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip(), dirty=subprocess.check_output(["git", "status", "--porcelain"], text=True).splitlines(), args=args, pid=process.pid, exitCode=code, seconds=time.time()-start)
(out/(label+".json")).write_text(json.dumps(record, indent=2)+"\n",encoding="utf-8")
print(label, code, flush=True)
sys.exit(code)
