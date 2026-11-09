from pathlib import Path
import re,os,subprocess,time,json,sys
root=Path.cwd();out=Path('E:/Kitao-Deliveries/r55-collage-favorites-20261109');out.mkdir(exist_ok=True,parents=True)
dotnet=re.search(r'dotnet="([^"]+)"',Path('tools/whole-app-followup/build_current.py').read_text()).group(1)
env=os.environ.copy();env['DOTNET_ROOT']=str(Path(dotnet).parent);env['PATH']=str(Path(dotnet).parent)+os.pathsep+env.get('PATH','')
label=sys.argv[1];args=sys.argv[2:];start=time.time()
if (out/(label+".log")).exists(): raise RuntimeError("Build log already exists; choose a new label")
with (out/(label+'.log')).open('wb') as log:
    p=subprocess.Popen([dotnet]+args+['--disable-build-servers','-m:1','-nr:false'],stdout=log,stderr=subprocess.STDOUT,env=env,creationflags=subprocess.CREATE_NO_WINDOW);code=p.wait()
(out/(label+'.json')).write_text(json.dumps(dict(sourceSha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),dirty=subprocess.check_output(['git','status','--porcelain'],text=True).splitlines(),cwd=str(root),dotnet=dotnet,args=args,pid=p.pid,exitCode=code,seconds=time.time()-start),indent=2));print(label,code,flush=True);sys.exit(code)
