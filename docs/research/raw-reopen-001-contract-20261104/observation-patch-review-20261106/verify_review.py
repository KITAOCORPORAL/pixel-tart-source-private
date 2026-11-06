"""Read fixed source, hashes and synthetic files only. Never load a native library or RAW."""
from pathlib import Path
import hashlib,json,re,subprocess,sys,tempfile
HERE=Path(__file__).resolve().parent
m=json.loads((HERE/"INPUT_MANIFEST.json").read_text(encoding="utf-8"))
snap=Path(m["snapshotRoot"])
checks=[]
def check(name,condition):
    if not condition:raise AssertionError(name)
    checks.append(name)
def sha(b):return hashlib.sha256(b).hexdigest()
for r in m["files"]:
    data=Path(r["snapshotPath"]).read_bytes()
    check("snapshot:"+r["kind"]+":"+Path(r["snapshotPath"]).name,len(data)==r["bytes"] and sha(data)==r["sha256"])

source=Path(m["librawSourceRoot"])
def original(p):return subprocess.check_output(["git","-C",str(source),"show",m["librawCommit"]+":"+p])
lock=json.loads((snap/"research-baseline/INTERNAL_SOURCE_LOCK.json").read_text(encoding="utf-8"))
locked=[r for r in lock["sourceRecords"] if r["project"]=="libraw"]
for r in locked:check("fixed-libraw:"+r["path"],sha(original(r["path"]))==r["sha256"])
prep=json.loads((snap/"receipts/frozen-source-v3/PREPARATION.json").read_text())
for r in prep["modifiedFiles"]:
    b=(snap/"generated"/r["path"]).read_bytes()
    check("generated-hash:"+r["path"],sha(b)==r["generatedSha256"])
    stripped=re.sub(rb"/\*RR001I_BEGIN\*/[\s\S]*?/\*RR001I_END\*/",b"",b)
    check("original-byte-restoration:"+r["path"],stripped==original(r["path"]))
header=(snap/"patch/rr001_internal.h").read_text(encoding="utf-8")
check("frozen-header",sha((snap/"patch/rr001_internal.h").read_bytes())==prep["observerHeaderSha256"])
cpp=(snap/"generated/src/postprocessing/dcraw_process.cpp").read_text(encoding="utf-8")
p1=cpp.index('stage(imgdata,"P1",true)')
assignment=cpp.index('quality = 2 + !IO.fuji_width')
read=cpp.index('rr001i::g.quality=quality')
check("F1-read-before-original-assignment-reproduced",read<p1<assignment and 'int quality, i;' in cpp[:read])
p2=cpp.index('P2-after-pre-scale-callback-before-scale')
check("P2-after-callback-before-scale",cpp.index('(callbacks.pre_scalecolors_cb)(this);')<p2<cpp.index('if (!O.no_auto_scale)'))
x=cpp.index('rr001i::g.branch="xtrans"')
check("P5-actual-xtrans-entry-four-slots",x<cpp.index('stage(imgdata,"P5",true)',x)<cpp.index('xtrans_interpolate(quality > 2 ? 3 : 1);',x))
check("P6-after-actual-return",cpp.index('xtrans_interpolate(quality > 2 ? 3 : 1);',x)<cpp.index('stage(imgdata,"P6",false)',x)<cpp.index('LIBRAW_PROGRESS_INTERPOLATE',x))
check("layout-rejects-pointer-and-stride-drift",all(t in header for t in ['width==d.sizes.iwidth','height==d.sizes.iheight','g.allocated==(uintptr_t)d.image','g.shrink==0','g.fuji==0']))
# Import only frozen JavaScript file-processing modules, and synthesize tiny U16 buffers.
# No host/DLL, budget runner, build script, image or natural-data path is invoked.
js=r"""
import fs from 'node:fs';import path from 'node:path';import {pathToFileURL} from 'node:url';import crypto from 'node:crypto';import assert from 'node:assert/strict';
const [snapshot,tmp]=process.argv.slice(1);
const {load,compareStage,order}=await import(pathToFileURL(path.join(snapshot,'patch/Compare-Checkpoints.mjs')));
const make=(dir,mismatch,states=true)=>{fs.mkdirSync(dir);let rows=[];for(const id of order){let channels=['P1','P3b','P5'].includes(id)?4:3;const b=Buffer.alloc(2*2*channels*2,0);let suffix=channels===4?'image4':'rgb';fs.writeFileSync(path.join(dir,`${id}.${suffix}.u16`),b);if(states)rows.push({kind:'state',where:id,width:2,height:2,iwidth:2,iheight:2,filters:mismatch?8:9,colors:3,branch:mismatch?'custom-xtrans':'xtrans',passes:mismatch?1:3,pointerGeneration:1});rows.push({kind:'payload',id,channels,valid:true,fullInitializedInput:channels===4,borderExcluded:0,width:2,height:2,sourcePitch:16,packedPitch:2*channels*2,sha256:crypto.createHash('sha256').update(b).digest('hex')});}fs.writeFileSync(path.join(dir,'native-events.jsonl'),rows.map(r=>JSON.stringify(r)).join('\n')+'\n');fs.writeFileSync(path.join(dir,'result.json'),JSON.stringify({status:'completed',synthetic:true}));};
const a=path.join(tmp,'left'),b=path.join(tmp,'right'),absent=path.join(tmp,'missing-state');make(a,false);make(b,true);make(absent,false,false);
let aa=load(a),bb=load(b);let compared=aa.map((s,i)=>compareStage(s,bb[i],path.join(tmp,s.id+'.csv')));assert(compared.every(x=>x.equal));assert.equal(load(absent).length,6);assert(compared.every(x=>x.stateEqual===false));const missing=load(absent);assert(missing.every(x=>x.context.length===0));const missingCompared=compareStage(missing[2],missing[2],path.join(tmp,'missing-equal.csv'));assert(missingCompared.equal&&missingCompared.stateEqual);
let changed=Buffer.from(fs.readFileSync(bb[2].file));changed.writeUInt16LE(321,6);fs.writeFileSync(bb[2].file,changed);bb[2].sha256=crypto.createHash('sha256').update(changed).digest('hex');const negative=compareStage(aa[2],bb[2],path.join(tmp,'changed.csv'));assert.equal(negative.changedChannels,1);assert.equal(negative.firstDifference.channel,3);
fs.writeFileSync(path.join(absent,'result.json'),JSON.stringify({status:'failed'}));assert.throws(()=>load(absent));
console.log(JSON.stringify({syntheticOnly:true,mismatchedBranchPassesCfaAcceptedAsEqualPayloads:true,stateDifferenceDetected:compared.every(x=>x.stateEqual===false),missingTypedStatesAccepted:true,missingStatesCanReportStateEqual:true,negativeControlFourthSlotDetected:true,incompleteRunRejected:true,rawCalls:0,nativeInit:0}));
"""
with tempfile.TemporaryDirectory(prefix="rr001-review-synthetic-") as tmp:
    synthetic=json.loads(subprocess.check_output(["node","--input-type=module","-e",js,str(snap),tmp],text=True,encoding="utf-8"))
for k in ["mismatchedBranchPassesCfaAcceptedAsEqualPayloads","stateDifferenceDetected","missingTypedStatesAccepted","missingStatesCanReportStateEqual","negativeControlFourthSlotDetected","incompleteRunRejected"]:check("synthetic:"+k,synthetic[k])
build=json.loads((snap/"receipts/native-build-preflight/BUILD_STATUS.json").read_text(encoding="utf-8-sig"))
budget=json.loads((snap/"receipts/bounded-runner-denied/EXECUTION_RECEIPT.json").read_text(encoding="utf-8-sig"))
check("existing-build-not-native-complete",build["status"]=="NOT_BUILT_REQUIRED_MSVC_TOOLCHAIN_UNAVAILABLE" and build["nativeCompilerInvocations"]==0)
check("existing-budget-denies-new-process",budget["rawCalls"]==0 and budget["consumed"]==2 and budget["remaining"]==0)
result=dict(version="RAW-REOPEN-001-ActualPatchReviewChecksV1",status="SOURCE_AND_SYNTHETIC_REVIEW_REPRODUCED_FINDINGS_OPEN",inputManifestSha256=sha((HERE/"INPUT_MANIFEST.json").read_bytes()),checks=checks,checkCount=len(checks),lockedSourceFiles=len(locked),restoredVendorFiles=len(prep["modifiedFiles"]),syntheticComparator=synthetic,admission="NOT_READY",runtimeP5P6="UNKNOWN",H1="UNKNOWN_CONDITIONAL",newCalls=dict(RAW=0,nativeInit=0,nativeCompiler=0,managedBuild=0,GPU=0,numericalReference=0),sourceSnapshotsUploaded=False)
if len(sys.argv)==2:
    target=Path(sys.argv[1])
    with target.open("x",encoding="utf-8",newline="\n") as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write("\n")
print(json.dumps({k:v for k,v in result.items() if k not in ["checks"]},ensure_ascii=False,indent=2))
