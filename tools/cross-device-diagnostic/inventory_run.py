"""Portable post-build identity inventory. Records existing files, never launches renders.
Accepts the home round10 layout or build_package.ps1's source-/build-{diagnostic,trace}.
"""
import argparse,json,subprocess,sys,os
from pathlib import Path
from generate_inputs import sha
p=argparse.ArgumentParser();p.add_argument('root',type=Path);p.add_argument('run',type=Path);p.add_argument('output',type=Path);a=p.parse_args();r=json.loads((a.run/'run.json').read_text());root=a.root
if a.output.exists():raise ValueError('New provenance output required')
def choose(*paths):
 for value in paths:
  if value.is_dir():return value.resolve()
 raise ValueError('Diagnostic source/build path not found: '+str(paths))
def cache(path):
 result={}
 for line in (path/'CMakeCache.txt').read_text().splitlines():
  if line and not line.startswith(('#','//')) and '=' in line and ':' in line.split('=',1)[0]:
   left,value=line.split('=',1);key,typ=left.split(':',1);result[key]=value
 return result
builds={}
for variant in ['diagnostic','trace']:
 source=choose(root/('round10-source-'+variant+'-v2'),root/('source-'+variant));build=choose(root/('round10-build-'+variant+'-v2'),root/('build-'+variant));c=cache(build);commands=subprocess.check_output([c['CMAKE_MAKE_PROGRAM'],'-C',str(build),'-t','commands'],text=True)
 toolpaths={key:{'path':c[key],'sha256':sha(Path(c[key]))} for key in ['CMAKE_CXX_COMPILER','CMAKE_C_COMPILER','CMAKE_MAKE_PROGRAM','SPEKTRALAB_GLSLANG','CMAKE_COMMAND'] if key in c and Path(c[key]).is_file()}
 sources={p.relative_to(source).as_posix():sha(p) for p in source.rglob('*') if p.is_file() and '.git' not in p.parts and p.suffix in ['.cpp','.hpp','.h','.comp','.glsl','.def']}
 builds[variant]={'sourcePath':str(source),'buildPath':str(build),'gitCommit':subprocess.check_output(['git','-C',str(source),'rev-parse','HEAD'],text=True).strip(),'gitStatus':subprocess.check_output(['git','-C',str(source),'status','--porcelain'],text=True),'sources':sources,'cmakeSettings':{k:v for k,v in c.items() if k.startswith(('CMAKE_CXX_','CMAKE_C_','CMAKE_BUILD_TYPE','SPEKTRALAB_'))},'buildNinjaHash':sha(build/'build.ninja'),'perObjectCommands':commands.splitlines(),'perObjectCommandTextSha256':__import__('hashlib').sha256(commands.encode()).hexdigest(),'toolFiles':toolpaths,'runtimeDlls':{p.name:sha(p) for p in (build/'engine').glob('*.dll')}}
report={'version':'DiagnosticExecutionProvenanceV2','scope':'post-build and post-run existing-artifact inventory; not a pre-invocation freeze','runManifestSha256':sha(a.run/'run.json'),'gpu':r['gpu'],'python':sys.version,'resources':r['resources'],'resourcesSha256':r['resourceSetSha256'],'builds':builds,'diagnosticTools':{p.name:sha(p) for p in Path(__file__).parent.glob('*') if p.is_file() and p.suffix in ['.py','.ps1','.json']},'machineExecution':'HOME_EXECUTED' if '5060 Ti' in r['gpu'] else 'EXECUTED_AS_RECORDED','companyExecution':'NOT_RUN_IN_HOME_SESSION'}
with a.output.open('x') as f:json.dump(report,f,indent=2)
print('DIAGNOSTIC_PORTABLE_PROVENANCE_WRITTEN')
