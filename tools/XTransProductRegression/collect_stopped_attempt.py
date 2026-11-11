"""Read and seal the stopped desktop-02 product attempt. Never loads RAW/native code.

Usage: python collect_stopped_attempt.py ARTIFACT_ROOT NEW_OUTPUT_DIR
Original plans, ledgers, pixels and logs remain read-only. This collector expects
exactly one completed A-cold decode followed by the parent 2 GiB resource stop.
"""
from pathlib import Path
import argparse, hashlib, json, statistics
from datetime import datetime, timezone


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream,'sha256').hexdigest().upper()


def load(path):
    return json.loads(Path(path).read_text('utf-8'))


def save(path, value):
    with Path(path).open('x',encoding='utf-8',newline='\n') as stream:
        json.dump(value,stream,indent=2,ensure_ascii=False);stream.write('\n')


def same_bytes(left,right):
    with Path(left).open('rb') as a,Path(right).open('rb') as b:
        while True:
            first=a.read(1048576);second=b.read(1048576)
            if first!=second:return False
            if not first:return True


def collect(root,out):
    assert not out.exists(),'Preserve an existing output directory'
    lock=load(root/'SOURCE_AND_RUN_LOCK.json');plan=load(root/'PLAN-A.json')
    assert lock['featureProductSourceSha']=='a24fac65ae91f3fdef7ff16a58dd462a5b7cbe77'
    assert plan['ProductSourceSha']==lock['featureProductSourceSha']
    assert plan['Task']==lock['rawTask'] and plan['Steps']==lock['steps']
    assert plan['MaximumHeavyDecodes']==lock['maximumHeavyDecodes']==6
    frozen={}
    for name in ('A','B','control'):
        p=load(root/f'PLAN-{name}.json')
        assert p['ProductSourceSha']==plan['ProductSourceSha'] and p['Steps']==plan['Steps']
        for f in p['Files']:
            previous=frozen.setdefault(f['Path'],f['Sha256'])
            assert previous==f['Sha256']
    for path,expected in frozen.items():assert sha(path)==expected.upper(),path
    for f in load(root/'OLD_LEDGER_BINDING.json'):assert sha(f['Path'])==f['Sha256']
    for f in plan['Inputs']:assert sha(f['Path'])==f['Sha256']
    attempt=root/'ui-steps/A-cold/desktop-02'
    launch=load(attempt/'LAUNCH.json');end=load(attempt/'EXIT.json');samples=load(attempt/'RESOURCE_SAMPLES.json')
    assert launch['attempt']=='desktop-02' and launch['step']=='A-cold' and launch['pid']==2408
    assert launch['productSourceSha']==plan['ProductSourceSha'] and launch['planSha256']==sha(root/'PLAN-A.json')
    assert launch['mainDllSha256']==sha(root/'app/KitaoPhotoSelector.dll')
    stop_path=Path(plan['LedgerRoot'])/'STOP.json';stop=load(stop_path)
    assert stop['task']==plan['Task'] and stop['step']=='A-cold'
    assert stop['reason']=='Parent app exceeded 2GiB external resource guard'
    assert end['pid']==launch['pid'] and end['exitCode']==1 and end['resourceGuardTriggered']
    reservations=list(Path(plan['LedgerRoot']).glob('*.consumed'));assert len(reservations)==1
    reservation=load(reservations[0]);assert reservation['task']==plan['Task'] and reservation['step']=='A-cold'
    assert reservation['planSha']==sha(root/'PLAN-A.json') and reservation['failedAttemptsConsume']
    worker=Path(plan['EvidenceRoot'])/('worker-'+reservation['reservationId'])
    assert len(list(Path(plan['EvidenceRoot']).glob('worker-*')))==1
    assert Path(str(reservations[0])+'.worker-entered').is_file()
    result=load(worker/'result.json');worker_resources=load(worker/'resources.json');request=load(worker/'request.json')
    assert result['status']=='completed' and result['heavyDecodeCalls']==1 and not result['debuggerAttached']
    assert result['nativeSha256']==plan['CandidateNativeSha256'] and result['bindingSha256']==plan['BindingSha256']
    assert request['step']=='A-cold' and request['planSha']==sha(root/'PLAN-A.json')
    assert (result['width'],result['height'])==(7752,5184)
    decoded=worker/'decoded.rgb48';assert decoded.stat().st_size==7752*5184*6
    assert sha(decoded)==result['pixelSha256']
    traces=list(Path(plan['EvidenceRoot']).glob('*.jsonl'));assert len(traces)==1
    trace=[json.loads(line) for line in traces[0].read_text('utf-8').splitlines()]
    assert all(t['task']==plan['Task'] and t['step']=='A-cold' and t['pid']==launch['pid'] for t in trace)
    assert [t['kind'] for t in trace]==['ProductPathBegin','HeaderOnlyProbe','CacheMiss','DecodeReserved','CandidateWorkerExited','CacheStored','ProductPathEnd']
    begin,finish=trace[0],trace[-1]
    assert begin['value']['name']==finish['value']['name']=='DarkroomImport' and finish['value']['completed']
    worker_end=next(t['value'] for t in trace if t['kind']=='CandidateWorkerExited');assert worker_end['exitCode']==0
    cache_event=next(t['value'] for t in trace if t['kind']=='CacheStored')
    cache_index=Path(cache_event['index']);cache=load(cache_index)
    payload=cache_index.parent/cache['PixelFile']
    assert cache['Key']==cache_event['key'] and cache['PixelSha']==result['pixelSha256']==sha(payload)
    assert cache['DecoderIdentity']==cache_event['identity']==reservation['identity']
    assert cache['SourceSha']==reservation['inputSha']
    assert same_bytes(decoded,payload)
    old_crop_plan=load(root/'CROP_PLAN_FINAL.json')
    research=Path(next(i['path'] for i in old_crop_plan['inputs'] if i['label']=='candidate'))
    assert sha(research)==result['pixelSha256'] and same_bytes(decoded,research)
    threshold=2**31;exceeded=[(i,s) for i,s in enumerate(samples) if s['privateBytes']>threshold or s['osPeakWorkingSetBytes']>threshold]
    assert len(exceeded)==1 and exceeded[0][0]==len(samples)-1
    index,crossing=exceeded[0]
    assert crossing['privateBytes']>threshold and crossing['osPeakWorkingSetBytes']<threshold
    assert max(s['privateBytes'] for s in samples)==end['peakPrivate50ms']
    assert max(s['workingSetBytes'] for s in samples)==end['peakWorkingSetObserved']
    gaps=[b['elapsedMs']-a['elapsedMs'] for a,b in zip(samples,samples[1:])]
    # This comparison only orders the events. Launch wall time and the sample
    # monotonic clock were not captured atomically, so it is not an exact interval.
    import_finish_epoch=datetime.fromisoformat(finish['atUtc']).timestamp()
    relative_import_finish=(import_finish_epoch-launch['startedUtc'])*1000
    assert crossing['elapsedMs']>relative_import_finish+10000
    out.mkdir(parents=True)
    summary=dict(date='2026-11-11',status='STOPPED_RESOURCE_GUARD',productSourceSha=plan['ProductSourceSha'],
        task=plan['Task'],attempt='desktop-02',step='A-cold',pid=launch['pid'],frozenFilesVerified=len(frozen),
        oldLedgersUnchanged=True,consumedHeavyDecodes=1,unusedAllowance=5,remainingExecutableDecodes=0,
        stopPreserved=True,stopReason=stop['reason'],nativeWorkerCompleted=True,importCommandCompleted=True,
        importUtc=dict(begin=begin['atUtc'],end=finish['atUtc']),coldImport=finish['value'],workerParentObservation=worker_end,
        workerSelfObservation=worker_resources,workerImage=dict(width=result['width'],height=result['height'],
            bytes=decoded.stat().st_size,pixelSha256=result['pixelSha256'],nativeSha256=result['nativeSha256'],
            bindingSha256=result['bindingSha256'],decodedOrientation=result['metadata']['Orientation'],
            declaredColorSpace=result['metadata']['ColorSpace']),
        fullByteComparison=dict(productWorkerVersusCache=True,productWorkerVersusStoredResearchCandidate=True,
            authority='one actual product decode compared with its stored cache and an existing research output; not two product runs'),
        parentLifetime=end,thresholdBytes=threshold,sampleCount=len(samples),sampleGapMedianMs=statistics.median(gaps),
        firstThresholdCrossing=crossing,previousSample=samples[index-1],
        firstThresholdCrossingMetric='PrivateBytes',
        eventOrdering='Guard crossing occurred after completed DarkroomImport; exact root cause unobserved',
        clockAlignment='Launch UTC and monotonic sampling start were not atomic; no exact phase time inferred',
        tiff=dict(exports=0,decodedPixelRepeatability='NOT_RUN',bitDepth='NOT_MEASURED',orientation='NOT_MEASURED',icc='NOT_MEASURED'),
        screenshot='Actual full darkroom screenshot unavailable before process guard termination',
        remainingPaths='NOT_RUN_BATCH_STOPPED',newNativeBuilds=0,defaultProductDecoderChanged=False)
    def sanitize(value):
        if isinstance(value,dict):return {k:sanitize(v) for k,v in value.items()}
        if isinstance(value,list):return [sanitize(v) for v in value]
        if isinstance(value,str):return value.replace(str(root),'ARTIFACT_ROOT').replace(str(root).replace('\\','/'),'ARTIFACT_ROOT')
        return value
    save(out/'ATTEMPT_RESULT.json',sanitize(summary))
    save(out/'PRODUCT_PATH_TRACE.json',sanitize(trace))
    paths=[root/'SOURCE_AND_RUN_LOCK.json',root/'OLD_LEDGER_BINDING.json',root/'PLAN-A.json',root/'PLAN-B.json',root/'PLAN-control.json',
        attempt/'LAUNCH.json',attempt/'EXIT.json',attempt/'RESOURCE_SAMPLES.json',attempt/'stdout.log',attempt/'stderr.log',
        traces[0],stop_path,*reservations,Path(str(reservations[0])+'.worker-entered'),cache_index,payload,*worker.iterdir()]
    manifest=[]
    for p in sorted(set(paths)):
        manifest.append(dict(path=p.relative_to(root).as_posix(),bytes=p.stat().st_size,sha256=sha(p)))
    save(out/'FAILURE_SCENE_MANIFEST.json',dict(readOnlyCapture=True,originalFilesNotModified=True,files=manifest))
    # Binned maxima preserve short peaks for a readable resource plot without
    # requiring native or graphics libraries in the collector itself.
    bins={}
    for s in samples:
        key=int(s['elapsedMs']//1000)
        previous=bins.setdefault(key,dict(elapsedSecond=key,workingSetBytes=0,privateBytes=0))
        for metric in ('workingSetBytes','privateBytes'):previous[metric]=max(previous[metric],s[metric])
    save(out/'PARENT_MEMORY_MAXIMA_1S.json',dict(scope='Per-second sample maxima; full unmodified samples retained locally',
        thresholdBytes=threshold,approxImportStartSecond=(datetime.fromisoformat(begin['atUtc']).timestamp()-launch['startedUtc']),
        approxImportEndSecond=relative_import_finish/1000,bins=list(bins.values())))
    print(json.dumps(dict(status=summary['status'],frozenFiles=len(frozen),consumed=1,unused=5,executable=0,
        workerRgb48BitExactToCacheAndStoredResearch=True,parentPrivatePeakBytes=end['peakPrivate50ms'],samples=len(samples))))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('artifact_root',type=Path);parser.add_argument('output_dir',type=Path)
    args=parser.parse_args();collect(args.artifact_root.resolve(),args.output_dir.resolve())
