"""Integration gate for the shipped prepare CMD, including a real extraction path with spaces.
Usage: python tools/chrome-capture/test_prepare_launcher.py <existing-package> <new-evidence-folder>
Uses a private copy; never modifies Chrome registration or browser records.
"""
from pathlib import Path
import json,shutil,subprocess,sys
package=Path(sys.argv[1]).resolve();evidence=Path(sys.argv[2]).resolve();evidence.mkdir(parents=True,exist_ok=False)
template=Path(__file__).resolve().with_name("Prepare-Guangjing-Capture.cmd")
assert '--prepare "%~dp0"' not in template.read_text(encoding='ascii')
target=evidence/'unpacked folder with spaces'/'Guangjing Capture Package';target.mkdir(parents=True)
shutil.copytree(package/'native-host',target/'native-host');(target/'extension').mkdir();shutil.copy2(package/'extension/DEVELOPMENT_ID.json',target/'extension/DEVELOPMENT_ID.json');shutil.copy2(template,target/template.name)
original=json.loads((target/'native-host/capture-host-config.json').read_text(encoding='utf-8-sig'))
def run(label):
    with(evidence/(label+'.log')).open('wb')as log:
        return subprocess.run(['cmd.exe','/d','/c',template.name],cwd=target,input=b'\r\n',stdout=log,stderr=subprocess.STDOUT,timeout=25,creationflags=subprocess.CREATE_NO_WINDOW).returncode
success=run('valid-space-path');assert success==0
manifest=json.loads((target/'native-host/com.guangjing.capture.json').read_text(encoding='utf-8-sig'));config=json.loads((target/'native-host/capture-host-config.json').read_text(encoding='utf-8-sig'))
assert Path(manifest['path']).resolve()==(target/'native-host/Guangjing.Capture.NativeHost.exe').resolve()
assert original['dataRoot']==config['dataRoot'] and original['pipeName']==config['pipeName']
identity=target/'extension/DEVELOPMENT_ID.json';hidden=identity.with_suffix('.regression-unavailable');identity.rename(hidden)
try:failure=run('native-failure-exit');assert failure!=0
finally:hidden.rename(identity)
result=dict(status='PASS_ARGUMENT_SAFE_LAUNCHER',spacePath=str(target),successExit=success,failureExit=failure,existingRuntimePreserved=True,manifestUsesActualCopiedRoot=True,noRegistryWrites=True)
(evidence/'RECEIPT.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
