import os,sys,subprocess,json,hashlib
from pathlib import Path
root=Path(sys.argv[1]);out=Path(sys.argv[2]);repo=Path(__file__).resolve().parents[2];native=root/'round11/source-natural-v1';build=root/'round11/build-natural-v1/engine';compiler=root/'deps/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin/clang++.exe';env=os.environ.copy();env['PATH']=str(compiler.parent)+os.pathsep+str(build)+os.pathsep+env['PATH'];exe=out/'cpu_setup_probe.exe'
identity_path=repo/'docs/research/color-engine-20261029/home-round12/cpu-setup-identity.json'
if exe.exists() or identity_path.exists():raise ValueError('New CPU helper and identity output required')
command=[str(compiler),'-std=c++20','-O2',str(out/'cpu_setup_probe.cpp'),'-I'+str(native/'engine/src/core'),'-I'+str(native/'engine/tests'),str(build/'libspektralab_core.a'),'-o',str(exe)]
r=subprocess.run(command,env=env,capture_output=True,text=True);(out/'cpu-build.txt').write_text(r.stdout+r.stderr);print('BUILD',r.returncode,r.stdout,r.stderr)
if r.returncode:sys.exit(r.returncode)
args=[str(exe),str(root/'resources-patch'),str(repo/'tools/color-round11/diagnostic/resolved-portraA.json'),str(out/'cpu-constants.dat'),str(out/'cpu-constants.idx'),str(out/'cpu-resolved.json')];r=subprocess.run(args,env=env,capture_output=True,text=True);(out/'cpu-run.txt').write_text(r.stdout+r.stderr);print('RUN',r.returncode,r.stdout,r.stderr)
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
j={'version':'Round12CpuSetupOnlyV1','gpuCalls':0,'rawDecodeCalls':0,'compileCommand':command,'runCommand':args,'exitCode':r.returncode,'compilerSha256':sha(compiler),'archiveSha256':sha(build/'libspektralab_core.a'),'wrapperSha256':sha(out/'cpu_setup_probe.cpp'),'exeSha256':sha(exe),'noGpuLink':'Only libspektralab_core.a, no pipeline/GPU/engine library'}
if r.returncode==0:
 j['resolvedMatchesFrozen']=json.loads((out/'cpu-resolved.json').read_text())==json.loads((repo/'tools/color-round11/diagnostic/resolved-portraA.json').read_text());j['dataSha256']=sha(out/'cpu-constants.dat');j['indexSha256']=sha(out/'cpu-constants.idx')
with identity_path.open('x',encoding='utf-8') as f:json.dump(j,f,indent=2)
sys.exit(r.returncode)
