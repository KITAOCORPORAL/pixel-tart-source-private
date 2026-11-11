"""Launch one frozen, isolated product attempt; all app actions use Computer Use.

Usage: python launch_app.py ARTIFACT_ROOT STEP [--attempt retry-02]
The launcher never opens a RAW file or invokes a product command. Each attempt
has a new output directory; existing logs, plans, and ledger entries are retained.
"""
from pathlib import Path
import argparse, ctypes as C, ctypes.wintypes as W, hashlib, json, os, re
import subprocess, time


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest().upper()


def write(path, value):
    path.write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('artifact_root',type=Path)
    parser.add_argument('step',choices=['A-cold','A-cache','A-rebuild','B-cold','B-cache','B-rebuild','control-A','control-B'])
    parser.add_argument('--attempt',help='New attempt name when resuming a step with no consumed decode')
    args=parser.parse_args(); root=args.artifact_root.resolve(); step=args.step
    if os.name!='nt': raise SystemExit('Windows desktop session required')
    if args.attempt and not re.fullmatch(r'[a-z][a-z0-9-]{0,39}',args.attempt):
        raise SystemExit('Attempt must be a short lowercase name, with no path separators')
    group='control' if step.startswith('control') else step[0]
    plan_path=root/('PLAN-'+group+'.json'); manifest=json.loads(plan_path.read_text('utf-8'))
    locked=json.loads((root/'SOURCE_AND_RUN_LOCK.json').read_text('utf-8'))
    source=locked['featureProductSourceSha']
    assert manifest['ProductSourceSha']==source and step in manifest['Steps']
    assert manifest['Version'] in (1,2) and manifest['MaximumHeavyDecodes']==locked['maximumHeavyDecodes']==6
    if manifest['Version']==2:
        if not manifest.get('AuthorizedForHeavyDecode',False) or locked.get('authorizedHeavyDecodes',0)!=6:
            raise SystemExit('Successor batch is pending explicit authorization; no app process launched')
        approval=json.loads((root/'AUTHORIZATION.json').read_text('utf-8'))
        assert approval.get('status')=='GRANTED_BY_USER' and approval.get('task')==manifest['Task'], 'Missing new-batch authorization receipt'
        assert manifest['Task']=='RAW-XTRANS-MEMORYFIX-PRODUCT-REGRESSION-20261111'
    assert manifest['Task']==locked['rawTask'] and manifest['Steps']==locked['steps']
    assert manifest['CandidateNativeSha256']==locked['candidateNativeSha']
    assert manifest['DefaultNativeSha256']==locked['defaultNativeSha']
    for record in manifest['Files']:
        assert sha(record['Path'])==record['Sha256'], 'Frozen file drift: '+record['Path']
    ledger=Path(manifest['LedgerRoot'])
    assert not (ledger/'STOP.json').exists(), 'Batch is stopped; never clear the stop to retry'
    consumed=[json.loads(p.read_text('utf-8')) for p in ledger.glob('*.consumed')]
    assert len(consumed)<=6
    if manifest['Steps'][step]>0:
        assert not any(record.get('step')==step or record.get('Step')==step for record in consumed), 'This step already consumed its decode'
    attempt_root=root/'ui-steps'/step
    if args.attempt: attempt_root=attempt_root/args.attempt
    if attempt_root.exists(): raise SystemExit('Attempt directory exists; preserve it and use --attempt with a new name')
    # Check earlier launch records before touching any attempt directory. A live
    # process with the recorded PID is treated conservatively as still active.
    kernel=C.WinDLL('kernel32',use_last_error=True)
    kernel.OpenProcess.argtypes=[W.DWORD,W.BOOL,W.DWORD]; kernel.OpenProcess.restype=W.HANDLE
    kernel.GetExitCodeProcess.argtypes=[W.HANDLE,C.POINTER(W.DWORD)]; kernel.GetExitCodeProcess.restype=W.BOOL
    kernel.CloseHandle.argtypes=[W.HANDLE]
    for prior in (root/'ui-steps').glob('**/LAUNCH.json'):
        if (prior.parent/'EXIT.json').exists(): continue
        pid=json.loads(prior.read_text('utf-8'))['pid']; handle=kernel.OpenProcess(0x1000,False,pid)
        if handle:
            code=W.DWORD(); okay=kernel.GetExitCodeProcess(handle,C.byref(code)); kernel.CloseHandle(handle)
            assert okay and code.value!=259, 'An earlier isolated product process may still be running'
        elif C.get_last_error()!=87:
            raise SystemExit('Cannot verify earlier process exit; leave its evidence untouched')
    attempt_root.mkdir(parents=True,exist_ok=False)
    data=root/('ui-data-'+('control-'+step[-1] if group=='control' else group)); data.mkdir(exist_ok=True)
    env=os.environ.copy(); env.update(PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=str(data),
        PIXEL_TART_XTRANS_STABLE_V1='1',PIXEL_TART_XTRANS_PLAN=str(plan_path),PIXEL_TART_XTRANS_STEP=step)
    for name in ['PIXEL_TART_HUMAN_ACCEPTANCE','PIXEL_TART_ACCEPTANCE_ROOT','PIXEL_TART_ACCEPTANCE_EXPORT_DIRECTORY']:
        env.pop(name,None)
    with (attempt_root/'stdout.log').open('w') as stdout, (attempt_root/'stderr.log').open('w') as stderr:
        process=subprocess.Popen([manifest['Dotnet'],str(root/'app/KitaoPhotoSelector.dll')],cwd=root/'app',env=env,
            stdout=stdout,stderr=stderr,creationflags=0x08000000)
        write(attempt_root/'LAUNCH.json',dict(pid=process.pid,step=step,attempt=args.attempt,productSourceSha=source,
            mainDllSha256=sha(root/'app/KitaoPhotoSelector.dll'),planSha256=sha(plan_path),
            isolatedDataRoot=str(data),startedUtc=time.time(),launcherInvokesProductCommands=False))
        kernel.K32GetProcessMemoryInfo.argtypes=[W.HANDLE,C.c_void_p,W.DWORD]; kernel.K32GetProcessMemoryInfo.restype=W.BOOL
        handle=kernel.OpenProcess(0x410,False,process.pid)
        samples=[]; start=time.monotonic(); stopped=False
        try:
            while process.poll() is None:
                buffer=C.create_string_buffer(80); C.c_uint32.from_buffer(buffer).value=80
                if handle and kernel.K32GetProcessMemoryInfo(handle,buffer,80):
                    import struct
                    peak,working=struct.unpack_from('<QQ',buffer.raw,8)
                    private=struct.unpack_from('<Q',buffer.raw,72)[0]
                    samples.append(dict(elapsedMs=(time.monotonic()-start)*1000,workingSetBytes=working,
                        osPeakWorkingSetBytes=peak,privateBytes=private))
                    if peak>2147483648 or private>2147483648:
                        ledger.mkdir(exist_ok=True)
                        # Never replace a prior failure record.
                        with (ledger/'STOP.json').open('x',encoding='utf-8') as stop:
                            json.dump(dict(task=manifest['Task'],step=step,reason='Parent app exceeded 2GiB external resource guard'),stop)
                        subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
                        stopped=True; break
                time.sleep(.05)
        finally:
            if handle: kernel.CloseHandle(handle)
        process.wait()
    write(attempt_root/'RESOURCE_SAMPLES.json',samples)
    write(attempt_root/'EXIT.json',dict(pid=process.pid,exitCode=process.returncode,resourceGuardTriggered=stopped,
        elapsedWallMs=(time.monotonic()-start)*1000,peakWorkingSetObserved=max((s['workingSetBytes'] for s in samples),default=0),
        peakPrivate50ms=max((s['privateBytes'] for s in samples),default=0),
        timingScope='Whole process lifetime including dialogs and idle; use ProductPathEnd for command timings'))
    return process.returncode


if __name__=='__main__': raise SystemExit(main())
