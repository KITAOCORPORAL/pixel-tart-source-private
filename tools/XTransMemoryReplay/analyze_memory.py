"""Seal paired no-RAW memory replay evidence. All original artifacts are read-only."""
from pathlib import Path
import argparse,hashlib,json,xml.etree.ElementTree as ET
parser=argparse.ArgumentParser();parser.add_argument('artifact_root',type=Path);parser.add_argument('original_root',type=Path);parser.add_argument('output',type=Path);args=parser.parse_args()
r=args.artifact_root;old=args.original_root
load=lambda p:json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
 with Path(p).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest().upper()
def save(name,value):
 with (args.output/name).open('x',encoding='utf-8',newline='\n') as f:json.dump(value,f,indent=2,ensure_ascii=False);f.write('\n')
case_names=['paired-baseline-shell-auto-01','paired-fixed-shell-auto-01','paired-fixed-complete-shell-auto-01','paired-baseline-window-gc-01','paired-fixed-window-gc-01','soak-baseline-01','soak-fixed-01','main-baseline-01','main-fixed-01','final-baseline-auto-01','final-fixed-auto-01','final-fixed-soak-01','final-fixed-full-pixels-01']
cases={}
for name in case_names:
 folder=r/name;guard=r/(name+'-guard');exit=load(guard/'EXIT.json')
 events=[json.loads(line) for line in (folder/'events.jsonl').read_text('utf-8').splitlines()]
 snapshots=[e for e in events if e['kind']=='MemorySnapshot'];assert all(not e['value']['nativeRawLoaded'] for e in snapshots)
 if name.startswith('paired-fixed-complete') or name.startswith('paired-fixed-window') or name.startswith('soak-fixed') or name.startswith('main-fixed') or name.startswith('final-fixed'):
  assert exit['exitCode']==0 and exit['stop'] is None
 elif exit['exitCode']!=0:assert exit['stop']=='2GiB resource guard'
 case=dict(exit=exit,mode=load(guard/'LAUNCH.json')['mode'],memorySnapshots=snapshots,detailRequests=[e for e in events if e['kind']=='DetailRequestChanged'],nativeRawLoaded=False)
 if (folder/'PIXEL_RESULT.json').exists():case['pixelResult']=load(folder/'PIXEL_RESULT.json')
 if (folder/'RESULT.json').exists():assert load(folder/'RESULT.json')['rawDecodeCalls']==0
 cases[name]=case
before=cases['final-baseline-auto-01'];after=cases['final-fixed-auto-01']
def snap(case,name):return next(e['value'] for e in case['memorySnapshots'] if e['value']['name']==name)
assert snap(before,'import-idle')['holders']['Editor._source']['width']==7752
assert snap(after,'import-idle')['holders']['Editor._source']['width']==1600
assert any(e['value']['requested'] and 'ReferenceColorWorkspaceView.RequestImageDetail' in e['value']['stack'] for e in before['detailRequests'])
assert not any(e['value']['requested'] for e in after['detailRequests'])
a=cases['paired-baseline-window-gc-01']['pixelResult'];b=cases['final-fixed-full-pixels-01']['pixelResult'];assert a==b and a['PixelWidth']==7752 and a['PixelHeight']==5184
assert sha(r/'final-baseline-app/XTransMemoryReplay.dll')==sha(r/'final-fixed-app/XTransMemoryReplay.dll')
comparison=dict(status='PAIRED_CACHE_REPLAY_PASS',baseline='820b4f0da7f8b741f0547dbfa72d49c0ae3a1105',fix='b86d02455be77e4ed0f44ddd5b9816681979746e',initialMemoryFix='a42cb7cd9633ad065f632172ee137614af151324',rawDecodeCalls=0,newNativeBuilds=0,guardBytes=2**31,
 sameReplayHostSha256=sha(r/'final-fixed-app/XTransMemoryReplay.dll'),
 automaticHiddenView=dict(beforePeakPrivateBytes=before['exit']['peakPrivateBytes50ms'],afterPeakPrivateBytes=after['exit']['peakPrivateBytes50ms'],
  peakReductionBytes=before['exit']['peakPrivateBytes50ms']-after['exit']['peakPrivateBytes50ms'],
  peakReductionPercent=100*(1-after['exit']['peakPrivateBytes50ms']/before['exit']['peakPrivateBytes50ms']),
  beforeIdlePrivateBytes=snap(before,'detail-idle')['privateBytes'],afterIdlePrivateBytes=snap(after,'detail-idle')['privateBytes'],
  beforeSourceWidth=7752,afterSourceWidth=1600),
 explicitActualSize=dict(rgb24FullFrameHashEqual=True,result=a,beforePeakPrivateBytes=cases['paired-baseline-window-gc-01']['exit']['peakPrivateBytes50ms'],afterPeakPrivateBytes=cases['final-fixed-full-pixels-01']['exit']['peakPrivateBytes50ms']),
 soak=dict(secondsMinimized=210,baseline=cases['soak-baseline-01']['exit'],fixed=cases['soak-fixed-01']['exit'],pairBinding=load(r/'SOAK_PAIR_BINDING.json')),
 scope='Diagnostic cached-RGB16 viewmodel/WPF host replay, not a new actual RAW or TIFF product acceptance')
allocations={}
for name in ['final-baseline-auto-01','final-fixed-auto-01']:
 data=load(r/(name+'-guard')/'ALLOCATION_STACKS.json')
 allocations[name]=dict(missingStacks=data['missingStacks'],authority=data['authority'],largeSamples=[a for a in data['largeAllocations'] if a['objectSize']>20*2**20],totals=data['totals'][:12])
assert any(a['objectSize']>100*2**20 and any('RawDisplayBitmapAdapter.ToBitmap' in s for s in a['stack']) for a in allocations['final-baseline-auto-01']['largeSamples'])
assert not any(a['objectSize']>100*2**20 and any('SetPreviewZoomAsync' in s for s in a['stack']) for a in allocations['final-fixed-auto-01']['largeSamples'])
binding=load(r/'ORIGINAL_FAILURE_BINDING.json')
for f in binding['files']:
 p=old/f['path'];assert p.stat().st_size==f['bytes'] and sha(p)==f['sha256']
for f in load(old/'OLD_LEDGER_BINDING.json'):assert sha(f['Path'])==f['Sha256']
assert len(list((old/'ledger').glob('*.consumed')))==1 and (old/'ledger/STOP.json').exists()
cache=load(old/'cache-A/e1fbf5aa5d1ef1f8da6b9f52544bbce61c089fa0234b06e8cca4cd9cc7cd43e7.json')
assert sha(r/'frozen-cache-copy.rgb48')==cache['PixelSha']
identity=dict(originalStopSha256=sha(old/'ledger/STOP.json'),originalFailureFilesVerified=len(binding['files']),oldLedgersUnchanged=True,oldConsumed=1,oldExecutable=0,sourceSha256=cache['SourceSha'],decoderIdentity=cache['DecoderIdentity'],optionsSha256=cache['OptionsSha'],key=cache['Key'],pixelSha256=cache['PixelSha'],newRawDecodes=0)
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};tests=[]
for name in ['tests-memory-final/memory-final.trx','tests-detail/detail.trx','tests-product/raw-product.trx','tests-admission02/task-admission02.trx']:
 p=r/name;doc=ET.parse(p);counts=doc.find('.//t:Counters',ns).attrib;assert int(counts['failed'])==0 and int(counts['notExecuted'])==0
 tests.append(dict(file=name,sha256=sha(p),counts=counts,results=[dict(test=x.attrib['testName'],outcome=x.attrib['outcome']) for x in doc.findall('.//t:UnitTestResult',ns)]))
assert sum(int(t['counts']['passed']) for t in tests)==17
assert load(r/'pending-worker-denial/RECEIPT.json')['status']=='PASS_PENDING_AUTHORIZATION_REJECTED_BEFORE_INPUT'
args.output.mkdir(exist_ok=False)
comparison['mainWindowContextControl']=dict(pairBinding=load(r/'MAIN_PAIR_BINDING.json'),baseline=cases['main-baseline-01']['exit'],fixed=cases['main-fixed-01']['exit'],limitation='Inert alternate shell context; both remained at proxy size. This control does not reproduce the original full app or its guard failure.')
comparison['successorProductSourceSha']='b86d02455be77e4ed0f44ddd5b9816681979746e'
comparison['finalSourceSoak']=cases['final-fixed-soak-01']['exit']
comparison['finalPairBinding']=load(r/'FINAL_PAIR_BINDING.json')
save('COMPARISON.json',comparison);save('ALLOCATION_EVIDENCE.json',allocations);save('ORIGINAL_AND_CACHE_BINDING.json',identity);save('TESTS.json',dict(passed=17,failed=0,notExecuted=0,suites=tests));save('CASE_SNAPSHOTS.json',cases);save('EXISTING_TIMELINE.json',load(r/'EXISTING_TIMELINE.json'))
gpu=[]
for p in sorted(r.glob('GPU_COUNTER*.json')):
 data=load(p);normalized={k:v for k,v in data.items() if k not in ('samples','reason')}
 if data.get('status')=='OBSERVED':normalized['metrics']=[dict(metric=s.get('Path','').split('\\')[-1],bytes=s.get('CookedValue'),status=s.get('Status')) for s in data.get('samples',[])]
 else:normalized['reason']=data.get('reason')
 gpu.append(normalized)
save('GRAPHICS_COUNTERS.json',gpu)
files=[]
for name in case_names:
 for folder in [r/name,r/(name+'-guard')]:
  for p in sorted(folder.glob('*')):
   if p.is_file():files.append(dict(path=p.relative_to(r).as_posix(),bytes=p.stat().st_size,sha256=sha(p)))
save('LOCAL_EVIDENCE_INDEX.json',dict(files=files,rawImagesAndTraceBinariesRemainLocal=True))
print(json.dumps(dict(status=comparison['status'],privatePeakReductionPercent=comparison['automaticHiddenView']['peakReductionPercent'],testsPassed=17,newRawDecodes=0,originalScenePreserved=True)))
