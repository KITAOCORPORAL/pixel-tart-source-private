"""Company: one matched-binary diagnostic, not a gate rerun or product deployment."""
from pathlib import Path
import argparse,subprocess,json,hashlib,sys
p=argparse.ArgumentParser();p.add_argument('--product-app',type=Path);p.add_argument('--baseline-launch',required=True,type=Path);p.add_argument('--dump-tool',required=True,type=Path);p.add_argument('--output',required=True,type=Path);a=p.parse_args();repo=Path(__file__).resolve().parents[2];tool=Path(__file__).parent;out=a.output.resolve();baseline=json.loads(a.baseline_launch.read_text('utf-8-sig'));app=a.product_app.resolve() if a.product_app else Path(baseline['stage']).resolve()
def sha(path):
 with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
# The actual prior run's App/Core hashes are authoritative; a package binding alone does not establish its stage bytes.
assert sha(app/'KitaoPhotoSelector.dll').lower()==baseline['appSha256'].lower() and sha(app/'RAWSelectionAssistant.Core.dll').lower()==baseline['coreSha256'].lower(),'Refuse a binary that differs from the actual prior legacy run'
quote=lambda value:"'"+str(value).replace("'","''")+"'"
metadata='[Diagnostics.FileVersionInfo]::GetVersionInfo('+quote(app/'KitaoPhotoSelector.dll')+').ProductVersion'
productVersion=subprocess.check_output(['powershell.exe','-NoProfile','-Command',metadata],text=True).strip()
assert 'f3190f4470996232aa63477838b8979d66dd8e45' in productVersion,'Use the actual f319 integration candidate, not research ac1 or stable f84'
assert baseline['scenario']=='points','Use the failing processed40 baseline, not an answered neutral/Sony experiment'
version=subprocess.check_output([str(a.dump_tool.resolve()),'--version'],text=True).strip();assert version.startswith('10.0.750501'), 'Use the same fixed dump tool; no target run started'
assert not out.exists();out.mkdir(parents=True)
cmd=['dotnet','publish',str(tool/'Host.csproj'),'-c','Release','-p:Platform=x64','-p:ProductAppDirectory='+str(app),'-o',str(out/'host'),'-v:q','--nologo']
with (out/'host-build.log').open('x',encoding='utf-8') as log:subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT,check=True)
subprocess.run([sys.executable,str(tool/'prepare_stage.py'),'--product-app',str(app),'--host-build',str(out/'host'),'--resources',str(repo/'src/RAWSelectionAssistant/App.xaml'),'--output',str(out/'stage')],check=True)
subprocess.run([sys.executable,str(tool/'capture.py'),'--stage',str(out/'stage'),'--baseline-launch',str(a.baseline_launch.resolve()),'--out',str(out/'run'),'--dump-tool',str(a.dump_tool.resolve())],check=True)
subprocess.run([sys.executable,str(tool/'analyze.py'),'--run',str(out/'run'),'--dump-tool',str(a.dump_tool.resolve())],check=True)
print('One company observation completed. Do not treat diagnostic exit0 or its pause as a resource PASS. Stable f84 was not changed.')
