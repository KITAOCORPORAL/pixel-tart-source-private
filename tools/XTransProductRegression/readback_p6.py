from pathlib import Path
import hashlib,json,sys,struct
root=Path(sys.argv[1]); out=Path(sys.argv[2])
reference=Path(sys.argv[3]); result=json.loads(reference.read_text('utf-8'))
plan=json.loads((root/'CROP_PLAN_FINAL.json').read_text('utf-8'))
def sha(p):
 with Path(p).open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
inputs={r['label']:r for r in plan['inputs']}
first=Path(inputs['candidate']['pairedP6']); second=first.parent.parent/'second'/first.name
assert sha(first)==result['pairMetrics']['firstSha256']
assert sha(second)==result['pairMetrics']['secondSha256']
size=7752*5184*3*2
assert first.stat().st_size==second.stat().st_size==size
with first.open('rb') as a,second.open('rb') as b:
 while True:
  block=a.read(1048576); other=b.read(1048576); assert block==other
  if not block: break
records=[]
for label,key in [('old-first','first'),('old-second','second')]:
 p=Path(inputs[label]['pairedP6'])
 expected=next(r for r in result['versusStoredBaselines'] if r['candidate']=='first' and r['baseline']==key)['metrics']
 assert sha(p)==expected['secondSha256']
 with first.open('rb') as a,p.open('rb') as b:
  at=((792*7752+2501)*3+1)*2; a.seek(at);b.seek(at)
  left=struct.unpack('<H',a.read(2))[0];right=struct.unpack('<H',b.read(2))[0]
 assert left==3053 and right==2183
 records.append(dict(baseline=label,sha256=expected['secondSha256'],maxDifferenceCoordinate=[2501,792,'G'],candidate=left,old=right,absoluteDifference=abs(left-right),previousWholeImageMetrics=expected['regions']['whole'],previousNonSeamMetrics=expected['regions']['interiorNonSeam']))
receipt=dict(status='PASS_STORED_P6_READBACK',newRawDecoderCalls=0,bytesPerP6=size,candidatePairSha256=result['pairMetrics']['firstSha256'],candidateP6BitExact=True,previousMetricsScope='Existing research report; source hashes and selected maximum coordinate independently re-read in this continuation',baselines=records)
with out.open('x',encoding='utf-8') as stream: stream.write(json.dumps(receipt,indent=2)+'\n')
print(json.dumps(dict(status=receipt['status'],candidatePairBitExact=True,newRawDecoderCalls=0)))
