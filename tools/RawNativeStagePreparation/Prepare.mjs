import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));
const inventory=[];
const COMMIT='cccb97647fcee56801fa68231fa8a38aa8b52ef7';
const target='src/postprocessing/dcraw_process.cpp';
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
const git=(root,...args)=>execFileSync('git',['-C',root,...args],{maxBuffer:32*1024*1024});
const mark=s=>'/*RR001_BEGIN*/'+s+'/*RR001_END*/';
export function instrument(source){
  const edits=[];
  const at=(anchor,code,after=false)=>{
    const i=source.indexOf(anchor);assert(i>=0&&source.indexOf(anchor,i+1)<0,'anchor not unique: '+anchor);
    edits.push({at:i+(after?anchor.length:0),code:mark(code)});
  };
  at('#include "../../internal/libraw_cxx_defs.h"','\n#include "rr001_observer.h"',true);
  at('    int rc = raw2image_ex(subtract_inline);',`    const char *rr_branch="not-dispatched";
    int rr_passes=-1;
    auto rr_stage=[&](const char *name,bool dense,int q) {
      try { rr001::stage(imgdata,name,dense,q,rr_passes,rr_branch,
        [&](unsigned y,unsigned x){return fcol(y,x);},IO.shrink,IO.fuji_width,subtract_inline); }
      catch(...) { rr001::errors++; }
    };
`);
  at('    // Adjust sizes','    rr_stage("raw2image-after",false,-1);\n');
  at('    if (!(di.decoder_flags & LIBRAW_DECODER_FIXEDMAXC))','    rr_stage("black-after",false,quality);\n');
  at('    if (callbacks.pre_preinterpolate_cb)','    rr_stage("scale-after",false,quality);\n');
  at('    pre_interpolate();','\n    rr_stage("pre-interpolate-after",false,quality);',true);
  // Every actual dispatcher leaf is wrapped; original leaf calls remain byte-identical.
  const branches=[
    ['(callbacks.interpolate_bayer_cb)(this);','custom-bayer',-1],
    ['(callbacks.interpolate_xtrans_cb)(this);','custom-xtrans',-1],
    ['lin_interpolate();','linear',-1],['vng_interpolate();','vng',-1],
    ['ppg_interpolate();','ppg',-1],['xtrans_interpolate(quality > 2 ? 3 : 1);','xtrans','quality > 2 ? 3 : 1'],
    ['ahd_interpolate(); // really don\'t need it here due to fallback op','ahd',-1],
    ['dcb(iterations, dcb_enhance);','dcb',-1],['dht_interpolate();','dht',-1],
    ['aahd_interpolate();','aahd',-1],['        ahd_interpolate();\n        imgdata.process_warnings','ahd-fallback',-1]
  ];
  for(const [anchor,branch,passes] of branches){
    const call=branch==='ahd-fallback'?'        ahd_interpolate();':branch==='ahd'?'ahd_interpolate();':anchor;
    const i=source.indexOf(anchor);assert(i>=0&&source.indexOf(anchor,i+1)<0,'branch anchor: '+branch);
    edits.push({at:i,code:mark(`{ rr_branch="${branch}"; rr_passes=${passes}; rr_stage("interpolation-enter",false,quality);\n`)});
    edits.push({at:i+call.length,code:mark('\nrr_stage("interpolation-return",rr_branch==std::string("xtrans"),quality); }')});
  }
  at('    convert_to_rgb();','    rr_stage("convert-rgb-before",rr_branch==std::string("xtrans"),quality);\n');
  at('    convert_to_rgb();','\n    rr_stage("convert-rgb-after",rr_branch==std::string("xtrans"),quality);',true);
  // Do not allocate std::string in phase control (observation already perturbs allocations).
  for(const e of edits)e.code=e.code.replaceAll('rr_branch==std::string("xtrans")','!strcmp(rr_branch,"xtrans")');
  edits.sort((a,b)=>b.at-a.at);let result=source;
  for(const e of edits)result=result.slice(0,e.at)+e.code+result.slice(e.at);
  assert.equal(result.replace(/\/\*RR001_BEGIN\*\/[\s\S]*?\/\*RR001_END\*\//g,''),source,'original source must survive exactly');
  return {result,insertionCount:edits.length,branches:branches.map(x=>x[1])};
}
export function selectValues(a,w,h,dense,color){
  assert(w>32&&h>32);const values=[];
  for(let y=16;y<h-16;y++)for(let x=16;x<w-16;x++)for(let k=0;k<(dense?3:1);k++)values.push(a[(y*w+x)*4+(dense?k:color(y,x))]);
  const b=Buffer.alloc(values.length*2);values.forEach((v,i)=>b.writeUInt16LE(v,2*i));return sha(b);
}
function domainSelfTest(){
  const w=40,h=40,a=new Uint16Array(w*h*4).fill(123),c=(y,x)=>(y+x)%3;
  const d=selectValues(a,w,h,true,c),s=selectValues(a,w,h,false,c);
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){
    a[(y*w+x)*4+3]=65535;
    if(y<16||x<16||y>=h-16||x>=w-16)for(let k=0;k<3;k++)a[(y*w+x)*4+k]=999;
  }
  assert.equal(d,selectValues(a,w,h,true,c));assert.equal(s,selectValues(a,w,h,false,c));
  a[(17*w+17)*4+(c(17,17)+1)%3]++;
  assert.equal(s,selectValues(a,w,h,false,c));assert.notEqual(d,selectValues(a,w,h,true,c));
  a[(17*w+17)*4+c(17,17)]++;assert.notEqual(s,selectValues(a,w,h,false,c));
  return {denseSha:d,sparseSha:s,syntheticOnly:true,excludedBorder:16,rawCalls:0};
}
const [mode,root,out]=process.argv.slice(2);
if(!['--prepare','--self-test'].includes(mode)||!root||!out)throw Error('Usage: Prepare.mjs --prepare|--self-test <shared-fixed-source> <NEW-output-dir>');
assert(!fs.existsSync(out),'preserve all existing outputs');
assert.equal(git(root,'rev-parse','HEAD').toString().trim(),COMMIT);
assert.equal(git(root,'status','--porcelain','--untracked-files=all').toString(),'','shared source must be clean');
const blob=git(root,'show',`HEAD:${target}`), original=blob.toString('utf8');
const patch=instrument(original);const test=domainSelfTest();
assert.throws(()=>instrument(original.replace('    pre_interpolate();','    changed_pre_interpolate();')));
assert.throws(()=>instrument(original+'\n    pre_interpolate();'));
fs.mkdirSync(out,{recursive:true});
if(mode==='--prepare'){
  for(const f of git(root,'ls-files','-z').toString().split('\0').filter(Boolean)){
    assert(!path.isAbsolute(f)&&!f.split('/').includes('..'));
    const dest=path.join(out,f);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,git(root,'show',`HEAD:${f}`));
  }
  fs.writeFileSync(path.join(out,target),patch.result);
  fs.copyFileSync(path.join(here,'rr001_observer.h'),path.join(out,path.dirname(target),'rr001_observer.h'));
}
const receipt={status:'SOURCE_PREPARATION_ONLY_NATIVE_NOT_COMPILED',mode,commit:COMMIT,target,originalSha256:sha(blob),instrumentedSha256:sha(patch.result),headerSha256:sha(fs.readFileSync(path.join(here,'rr001_observer.h'))),generatorSha256:sha(fs.readFileSync(fileURLToPath(import.meta.url))),originalByteRestoration:true,anchorRejection:true,insertionCount:patch.insertionCount,branches:patch.branches,domainSelfTest:test,fullRawDevelopments:0};
if(mode==='--prepare'){
 const walk=dir=>{for(const e of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,e.name);if(e.isDirectory())walk(p);else inventory.push({path:path.relative(out,p).split(path.sep).join('/'),sha256:sha(fs.readFileSync(p))});}};walk(out);fs.writeFileSync(path.join(out,'FILES.json'),JSON.stringify(inventory,null,2)+'\n');
}
fs.writeFileSync(path.join(out,'PREPARATION.json'),JSON.stringify(receipt,null,2)+'\n');console.log(JSON.stringify(receipt,null,2));
