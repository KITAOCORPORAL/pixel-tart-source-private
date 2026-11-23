"""Resolve tagged weak handles and query roots from one private dump. No live process mutation."""
from pathlib import Path
import argparse,json,re,subprocess,hashlib
p=argparse.ArgumentParser();p.add_argument('--run',required=True,type=Path);p.add_argument('--dump-tool',required=True,type=Path);a=p.parse_args();run=a.run.resolve();tool=a.dump_tool.resolve();dump=run/'before-second-ushort.dmp';mapping=json.loads((run/'WEAK_HANDLE_MAP.json').read_text('utf-8-sig'))
def command(path,commands):
 if not path.exists():
  args=[str(tool),'analyze',str(dump)]
  for c in commands:args+=['-c',c]
  args+=['-c','exit']
  with path.open('x',encoding='utf-8') as output:code=subprocess.run(args,stdout=output,stderr=subprocess.STDOUT,timeout=120).returncode
  if code:raise RuntimeError('SOS failed: '+path.name)
 return path.read_text('utf-8-sig')
def sha(path):
 with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
handles=command(run/'gchandles.log',['gchandles']);rows={}
for line in handles.splitlines():
 parts=line.split()
 if len(parts)>3 and re.fullmatch('[0-9a-fA-F]{8,16}',parts[0]) and re.fullmatch('[0-9a-fA-F]{8,16}',parts[2]):rows[int(parts[0],16)]=(parts,line)
results=[]
for subject in mapping['subjects']:
 role=subject['Role'];slot=int(subject['WeakHandle'],16);row=rows.get(slot)
 if row and int(row[0][2],16):
  parts,line=row;assert parts[1]=='WeakShort','Observer must not introduce a strong/pinned handle';address='0x'+parts[2];commands=['gcroot '+address]
  if role in ['first_workspace','first_editor','first_master_wrapper','first_processed_bitmap']:commands+=['dumpobj '+address]
  log=run/(role+'-roots.log');text=command(log,commands);counts=re.findall(r'Found (\d+) unique roots\.',text)
  if len(counts)!=1:raise RuntimeError('No complete root result; preserve raw log '+log.name)
  result={'role':role,'weakHandle':subject['WeakHandle'],'objectAddress':address,'type':subject['Type'],'payloadBytes':subject['PayloadBytes'],'strongRootCount':int(counts[0]),'weakTargetCleared':False,'rootLog':log.name,'rootLogSha256':sha(log)}
  if role=='first_editor':
   fields={}
   for line in text.splitlines():
    fieldsplit=line.split()
    if len(fieldsplit)>2 and fieldsplit[-1] in ['_source','_matchedImage','_frozenRawMaster','_fullRawDisplay']:
     fields[fieldsplit[-1]]=int(fieldsplit[-2],16)==0
   result['oldEditorFieldsNull']=fields
 else:
  existing=run/'processed-root-and-reader-slot.log'
  log=existing if existing.exists() else run/(role+'-slot.log')
  text=log.read_text('utf-8-sig') if log.exists() else command(log,['dp '+subject['WeakHandle']]);value=None
  for line in text.splitlines():
   match=re.match(r'^\s*([0-9a-fA-F]{8,16}):\s+([0-9a-fA-F]{8,16})',line)
   if match and int(match[1],16)==slot:value=int(match[2],16);break
  if value is None:raise RuntimeError('Cannot resolve missing weak slot; no liveness conclusion: '+role)
  result={'role':role,'weakHandle':subject['WeakHandle'],'type':subject['Type'],'weakSlotValue':hex(value),'weakTargetCleared':value==0,'strongRootCount':None,'slotLog':log.name,'slotLogSha256':sha(log)}
 results.append(result)
summary={'classification':'DIAGNOSTIC_ONLY_CAPTURED_BOUNDARY_NOT_UNPAUSED_CAPACITY_PROOF','subjects':results,'capture':json.loads((run/'CAPTURE_SUMMARY.json').read_text('utf-8-sig')),'scopeLimit':'Only tagged principal first-round objects at the captured household/company scope. A pause can allow other threads to progress; zero managed roots does not measure native WIC commitment, all retired images, or another machine/input.','knownTaskEnumerationGap':'Inherited KnownTasks asks GetField(Completion), but ThumbnailJob.Completion is a property. Existing four completed task names do not enumerate every thumbnail task. The diagnostic deliberately does not change waiting behavior.'}
(run/'ANALYSIS_SUMMARY.json').write_text(json.dumps(summary,indent=2),encoding='utf-8');print(json.dumps({'roles':len(results),'roots':{x['role']:x['strongRootCount'] for x in results},'cleared':[x['role'] for x in results if x['weakTargetCleared']]}))
