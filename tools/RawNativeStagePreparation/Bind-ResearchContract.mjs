import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
export const REVIEW='ef95df88e5ca7b44a6adbcd0f7278941c46db58f';
export const SOURCE='cccb97647fcee56801fa68231fa8a38aa8b52ef7';
export const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const base='docs/research/raw-reopen-001-contract-20261104/process-internal-review/';
const git=(root,...args)=>execFileSync('git',['-C',root,...args],{maxBuffer:32*1024*1024});
export function verifySourceRecords(lock,load){
  assert.equal(lock.librawCommit,SOURCE,'Wrong source commit');
  const selected=lock.sourceRecords.filter(x=>x.project==='libraw');
  assert(selected.length>0,'No LibRaw records');
  const paths=new Set();
  return selected.map(f=>{
    assert(!paths.has(f.path),'Duplicate source record');paths.add(f.path);
    assert.equal(f.commit,SOURCE,'Mixed commits');
    const bytes=load(f.path);assert.equal(bytes.length,f.bytes,'Source length: '+f.path);
    assert.equal(sha(bytes),f.sha256,'Source hash: '+f.path);
    return {path:f.path,bytes:bytes.length,sha256:f.sha256,verified:true};
  });
}
export function verifyPrepared(preparation,load,original){
  assert.equal(preparation.commit,SOURCE,'Wrong prepared commit');
  const cpp=load('src/postprocessing/dcraw_process.cpp');
  assert.equal(sha(cpp),preparation.instrumentedSha256,'Prepared source drift');
  assert.equal(sha(original),preparation.originalSha256,'Original source drift');
  const restored=cpp.toString('utf8').replace(/\/\*RR001_BEGIN\*\/[\s\S]*?\/\*RR001_END\*\//g,'');
  assert.equal(restored,original.toString('utf8'),'Original statements changed');
  assert.equal(sha(load('src/postprocessing/rr001_observer.h')),preparation.headerSha256,'Observer drift');
  return cpp.toString('utf8');
}
// Presence means a static callsite, never a runtime observation or full spec compliance.
export function coverage(spec,cpp){
  assert.equal(spec.sourceCommit,SOURCE);
  const mapping={P1:'raw2image-after',P3b:'scale-after',P5:'interpolation-enter',P6:'interpolation-return',P7:'convert-rgb-before',P9:'convert-rgb-after',P4:'pre-interpolate-after'};
  const incomplete={
    P0:['saved/restored small-state snapshot'],
    P1:['allocation extent/generation','full CFA origin/grid and raw sizes/margins'],
    P2:['after pre_scalecolors callback point (existing black-after is earlier)','callback execution events'],
    P3a:['actual scale_mul float bits and WB branch'],
    P3b:['full layout/state header'],P4:['image generation and mix_green state'],
    P5:['complete initialized input-state contract beyond sparse CFA hash','callback execution/state snapshots'],
    P6:['complete layout/state header'],P7:['post-demosaic branch events'],
    P8:['actual effective out_cam valid columns and raw_color'],
    P9:['complete layout/state header and histogram validity'],P10:['postconvert callback/stretch event summary']
  };
  return spec.checkpoints.map(c=>({id:c.id,file:c.file,sourceAnchor:c.afterLine??c.beforeLine??null,primaryPayload:['P1','P3b','P5','P6','P7','P9'].includes(c.id),observerStage:mapping[c.id]??null,staticCallsitePresent:!!mapping[c.id]&&cpp.includes('"'+mapping[c.id]+'"'),runtimeCaptured:false,missingOrUnverified:incomplete[c.id]??['Unreviewed checkpoint']}));
}
function run(args){
  const [research,source,prepared,out]=args;assert(out&&args.length===4,'Usage: Bind-ResearchContract.mjs <research-tree> <fixed-source> <prepared-tree> <NEW-output-dir>');
  assert(!fs.existsSync(out),'Preserve all earlier outputs');
  assert.equal(git(research,'rev-parse',REVIEW).toString().trim(),REVIEW,'Missing pinned research commit');
  assert.equal(git(source,'rev-parse','HEAD').toString().trim(),SOURCE);
  assert.equal(git(source,'status','--porcelain','--untracked-files=all').toString(),'','Shared source must be clean');
  const names=['INTERNAL_SOURCE_LOCK.json','CHECKPOINT_SPEC.json','CHECKPOINTS.md','REPORT.md','HYPOTHESIS_H1.json'];
  const docs=Object.fromEntries(names.map(n=>[n,git(research,'show',`${REVIEW}:${base}${n}`)]));
  const lock=JSON.parse(docs['INTERNAL_SOURCE_LOCK.json']),spec=JSON.parse(docs['CHECKPOINT_SPEC.json']);
  const records=verifySourceRecords(lock,p=>git(source,'show',`HEAD:${p}`));
  const preparation=JSON.parse(fs.readFileSync(path.join(prepared,'PREPARATION.json')));
  const cpp=verifyPrepared(preparation,p=>fs.readFileSync(path.join(prepared,p)),git(source,'show','HEAD:src/postprocessing/dcraw_process.cpp'));
  const checkpoints=coverage(spec,cpp);
  assert.equal(checkpoints.filter(x=>x.primaryPayload&&x.staticCallsitePresent).length,6,'Missing primary payload callsite');
  fs.mkdirSync(out,{recursive:true});
  const receipt={status:'SOURCE_BINDING_VERIFIED_CHECKPOINT_COVERAGE_PARTIAL',researchHead:REVIEW,sourceCommit:SOURCE,documents:names.map(n=>({path:base+n,sha256:sha(docs[n])})),sourceRecords:records,preparedIdentity:{source:preparation.instrumentedSha256,observer:preparation.headerSha256},checkpoints,rawCalls:0,nativeBuild:'NOT_EXECUTED_BY_BINDER',nativeAbi:'NOT_EXECUTED_BY_BINDER',executionReady:false,limitations:['Static callsite presence is not full contract conformance','No new native build or runtime branch proof','Sparse input hash equality alone cannot establish all interpolator input-state equality for H1','H1 remains conditional; no actual consumer-load or parallel-region trace']};
  fs.writeFileSync(path.join(out,'RESEARCH_CHECKPOINT_BINDING.json'),JSON.stringify(receipt,null,2)+'\n');
  console.log(JSON.stringify({status:receipt.status,verifiedLibRawFiles:records.length,primaryPayloadCallsites:6,researchHead:REVIEW,rawCalls:0,executionReady:false},null,2));
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))run(process.argv.slice(2));
