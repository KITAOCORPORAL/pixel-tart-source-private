"""Explicit home research probe of hash-pinned official executables. Never alters product runtime."""
import argparse,pathlib,hashlib,json,subprocess,time,struct
p=argparse.ArgumentParser();p.add_argument('--package',type=pathlib.Path,required=True);p.add_argument('--input-manifest',type=pathlib.Path,required=True);p.add_argument('--output',type=pathlib.Path,required=True);p.add_argument('--evidence',type=pathlib.Path,required=True);a=p.parse_args()
if a.output.exists() or a.evidence.exists():raise RuntimeError('New output/evidence required')
sha=lambda path:hashlib.sha256(path.read_bytes()).hexdigest();exe=a.package/'bin/dcraw_emu.exe';dll=a.package/'bin/libraw.dll'
assert sha(exe)=='8df090a80c784fa03182b91ab1679fd08e5fc139d54b0bfb8b003024ef741129';assert sha(dll)=='6a459c22039abf0eac4d263673337c8ed5f223acbd372fcf77610debf80ac8cd'
a.output.mkdir(parents=True);rows=[]
def ppm(path):
 with path.open('rb') as stream:
  tokens=[]
  while len(tokens)<4:
   line=stream.readline()
   if not line:raise RuntimeError('PPM truncated')
   if not line.startswith(b'#'):tokens+=line.split()
  assert tokens[0]==b'P6' and tokens[3]==b'65535' and len(tokens)==4
  w,h=map(int,tokens[1:3]);offset=stream.tell();assert path.stat().st_size-offset==w*h*6
  digest=hashlib.sha256()
  while block:=stream.read(1024*1024):digest.update(block)
  return dict(width=w,height=h,bits=16,pixelBytes=w*h*6,pixelSha256=digest.hexdigest())
flags=['-w','-6','-o','4','-g','1','1','-W','-q','3','-t','0']
for entry in json.loads(a.input_manifest.read_text(encoding='utf-8-sig')):
 if not entry['authorizedIdentityMatch']:
  rows.append(dict(source=entry['path'],status='NOT_RUN_MISSING_AUTHORIZED_ORIGINAL'));continue
 source=pathlib.Path(entry['path']);assert sha(source)==entry['expectedSha256'].lower();copy=a.output/('copy'+source.suffix);copy.write_bytes(source.read_bytes());output=a.output/(source.stem+'.linear-prophoto.ppm');cmd=[str(exe.resolve()),*flags,'-Z',str(output.resolve()),str(copy.resolve())];clock=time.perf_counter()
 proc=subprocess.run(cmd,capture_output=True,text=True,timeout=180,cwd=a.output,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));(a.output/(source.stem+'.stdout')).write_text(proc.stdout);(a.output/(source.stem+'.stderr')).write_text(proc.stderr)
 row=dict(sourceSha256=sha(source),copySha256=sha(copy),sourceFile=source.name,command=cmd,exitCode=proc.returncode,elapsedSeconds=time.perf_counter()-clock,status='RESEARCH_PREBUILT_DECODED' if proc.returncode==0 else 'FAILED',contract='HomeOfficial0222LinearProPhoto16ResearchV1',domain='LibRaw ProPhoto primaries/D65 convention, linear gamma1/1, camera WB baked; quantized rendered RGB, not sensor or ICC ProPhotoD50',orientation='-t0: not applied; original metadata remains authoritative')
 if proc.returncode==0:row.update(ppm(output))
 rows.append(row)
 if proc.returncode:break
# Process termination probe is NOT callback cancellation/product cancellation proof.
valid=next((v for v in json.loads(a.input_manifest.read_text(encoding='utf-8-sig')) if v['authorizedIdentityMatch']),None)
if valid:
 noresult=a.output/'cancelled.ppm';cmd=[str(exe.resolve()),*flags,'-Z',str(noresult.resolve()),str(pathlib.Path(valid['path']).resolve())]
 proc=subprocess.Popen(cmd,stdout=subprocess.PIPE,stderr=subprocess.PIPE,cwd=a.output,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));time.sleep(.05);cancelled=proc.poll() is None
 if cancelled:proc.terminate()
 stdout,stderr=proc.communicate(timeout=30);rows.append(dict(operation='TerminateOwnedResearchProcessAfter50ms',cancelRequested=cancelled,exitCode=proc.returncode,outputExists=noresult.exists(),productCallbackCancel='NOT_RUN',sourceUnchanged=sha(pathlib.Path(valid['path']))==valid['expectedSha256'].lower()))
a.evidence.write_text(json.dumps(dict(status='RESEARCH_PREBUILT_PROBED_NOT_PRODUCT',executableSha256=sha(exe),nativeSha256=sha(dll),sourceRebuilt=False,cleanVM='NOT_RUN',distributionApproved=False,productEnabled=False,rows=rows),indent=2));print(json.dumps(rows,indent=2))
