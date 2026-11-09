import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
// Validate member uniqueness before JSON.parse can replace an earlier member.
// Keys are decoded before comparison, so "quality" and "qual\u0069ty" collide.
export function parseUniqueJson(text,label='JSON'){
 let at=0;const fail=message=>assert.fail(`${label}: ${message} at offset ${at}`);
 const whitespace=()=>{while(at<text.length&&/[ \t\r\n]/.test(text[at]))at++;};
 function string(){const start=at;if(text[at++]!=='"')fail('Expected string');while(at<text.length){const c=text[at++];if(c==='"')return JSON.parse(text.slice(start,at));if(c==='\\')at++;}fail('Unterminated string');}
 function value(depth){if(depth>256)fail('JSON nesting limit');whitespace();const c=text[at];
  if(c==='{'){at++;whitespace();const keys=new Set();if(text[at]==='}'){at++;return;}for(;;){whitespace();const key=string();if(keys.has(key))fail('Duplicate object key '+JSON.stringify(key));keys.add(key);whitespace();if(text[at++]!==':')fail('Expected colon');value(depth+1);whitespace();const separator=text[at++];if(separator==='}')return;if(separator!==',')fail('Expected comma or object end');}}
  if(c==='['){at++;whitespace();if(text[at]===']'){at++;return;}for(;;){value(depth+1);whitespace();const separator=text[at++];if(separator===']')return;if(separator!==',')fail('Expected comma or array end');}}
  if(c==='"'){string();return;}const token=/^(?:true|false|null|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?)/.exec(text.slice(at));if(!token)fail('Expected JSON value');at+=token[0].length;
 }
 value(0);whitespace();if(at!==text.length)fail('Trailing JSON input');return JSON.parse(text);
}
export const primaryOrder=['P1','P3b','P5','P6','P7','P9'];
export const order=['P1','P3b','P5','XTRANS-serial-ready','P6','P7','P9'];
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
export function semantic(v){if(Array.isArray(v))return v.map(semantic);if(v&&typeof v==='object')return Object.fromEntries(Object.keys(v).sort().filter(k=>!['imagePointerDiagnosticOnly','scratchPointerDiagnosticOnly','pointerGeneration','sequence'].includes(k)).map(k=>[k,semantic(v[k])]));return v;}
const one=(rows,predicate,why)=>{const found=rows.filter(predicate);assert.equal(found.length,1,why+' missing/duplicate');return found[0];};
const named=(rows,kind,name)=>one(rows,r=>r.kind===kind&&r.name===name,kind+':'+name);
const isHash=s=>typeof s==='string'&&/^[0-9a-f]{64}$/i.test(s);
const hasNumbers=(a,n)=>Array.isArray(a)&&a.length===n&&a.every(Number.isFinite);
const callbacks=['pre_subtractblack','pre_scalecolors','pre_preinterpolate','pre_interpolate','post_interpolate','pre_converttorgb','post_converttorgb','interpolate_bayer','interpolate_xtrans'];

// Metadata-only: never opens a pixel file or loads native code.
export function validateCaptureMetadata(rows){
 const at=r=>rows.indexOf(r),state=w=>one(rows,r=>r.kind==='state'&&r.where===w,w+' state'),payload=id=>one(rows,r=>r.kind==='payload'&&r.id===id,id+' payload');
 const uint=(v,name,min=0,max=Number.MAX_SAFE_INTEGER)=>assert(Number.isSafeInteger(v)&&v>=min&&v<=max,'Invalid integer '+name);
 const between=(r,a,b,name)=>assert(at(a)<at(r)&&at(r)<at(b),'Wrong phase: '+name);
 const bound=(s,p)=>{
  for(const k of ['width','height','iwidth','iheight','allocWidth','allocHeight','declaredAllocationBytes','rawWidth','rawHeight','rawPitch','left','top'])uint(s[k],s.where+'.'+k);
  assert(s.width===s.iwidth&&s.height===s.iheight&&s.width>32&&s.height>32&&s.width<=20000&&s.height<=20000,'Unsupported state layout');
  assert(s.allocWidth>=s.iwidth&&s.allocHeight>=s.iheight&&s.declaredAllocationBytes===s.allocWidth*s.allocHeight*8&&s.declaredAllocationBytes>=s.iwidth*s.iheight*8&&s.iwidth*s.iheight*8<=1073741824,'Invalid initialized allocation extent');
  assert(s.rawPitch>=s.rawWidth*2&&s.rawPitch%2===0&&s.left+s.width<=s.rawWidth&&s.top+s.height<=s.rawHeight,'Invalid raw layout/margins');
  uint(s.quality,s.where+'.quality',-1,12);uint(s.passes,s.where+'.passes',-1,3);
  if(!p)return;
  for(const k of ['width','height','channels','sourcePitch','packedPitch'])uint(p[k],p.id+'.'+k,1);
  assert(p.valid===true&&p.borderExcluded===0&&isHash(p.sha256),'Invalid payload validity/hash');
  assert(p.width===s.width&&p.height===s.height&&p.sourcePitch===s.iwidth*8&&p.packedPitch===s.width*p.channels*2,'State/payload layout mismatch');
  assert(p.width*p.height*p.channels*2<=s.declaredAllocationBytes&&p.fullInitializedInput===(p.channels===4),'Payload extent/initialized-domain mismatch');
 };
 const records={};let previous=-1;
 for(const id of order){const s=state(id==='XTRANS-serial-ready'?'xtrans-serial-green-bounds-complete':id),p=payload(id);assert(previous<at(s)&&at(s)<at(p),'Checkpoint state/payload order');bound(s,p);records[id]={s,p};previous=at(p);}
 for(let i=0;i<order.length;i++){
  const id=order[i],{s,p}=records[id],next=i+1<order.length?records[order[i+1]].s:state('P10-after-postconvert-stretch-restore');
  const suffix=['P1','P3b'].includes(id)?'-cfa':['P6','P7','P9'].includes(id)?'-audit4':null;
  if(suffix){const aux=payload(id+suffix);between(aux,p,next,id+suffix);bound(s,aux);assert(aux.channels===(suffix==='-cfa'?1:4),'Companion channel mapping');}
 }
 const p5=records.P5,p6=records.P6,serial=records['XTRANS-serial-ready'];
 const lab=named(rows,'defined-byte-span-hash','cielab-initialized-cbrt'),hex=named(rows,'defined-byte-span-hash','xtrans-fully-defined-allhex');
 between(lab,p5.p,serial.s,'CIELab initialization');between(hex,lab,serial.s,'allhex');
 const matrix=rows.filter(r=>r.kind==='float32-bits'&&r.name==='cielab-effective-xyz-cam');assert(matrix.length===3,'CIELab valid rows missing/duplicate');for(const r of matrix)between(r,lab,hex,'CIELab matrix');assert(matrix.every((r,i)=>at(r)===at(lab)+1+i),'CIELab rows must immediately follow their initialized lookup');
 const p8=state('P8-effective-out-cam');bound(p8);between(p8,payload('P7-audit4'),records.P9.s,'P8');
 const out=rows.filter(r=>r.kind==='float32-bits'&&r.name==='effective-out_cam-row'),bypass=rows.filter(r=>r.kind==='event'&&r.name==='out-cam');if(p8.rawColor===0){assert(out.length===3&&bypass.length===0,'out_cam rows missing/duplicate');for(const r of out)between(r,p8,records.P9.s,'out_cam');}else{assert(p8.rawColor===1&&out.length===0&&bypass.length===1&&bypass[0].phase==='not-consumed-raw-color-bypass','Explicit raw-color matrix bypass required');between(bypass[0],p8,records.P9.s,'out_cam bypass');}
 between(named(rows,'defined-byte-span-hash','P9-initialized-histogram'),payload('P9-audit4'),state('P10-after-postconvert-stretch-restore'),'P9 histogram');
 const scalar=name=>named(rows,'scalar',name),passes=scalar('actual-xtrans-passes'),tileSize=scalar('actual-xtrans-T'),ndir=scalar('actual-xtrans-ndir');
 for(const r of [passes,tileSize,ndir])between(r,lab,hex,r.name);
 assert([1,3].includes(passes.value)&&passes.value===p5.s.passes&&passes.value===(p5.s.quality>2?3:1),'Actual XTrans passes/quality mismatch');
 assert(tileSize.value===512&&ndir.value===(passes.value>1?8:4),'Fixed LibRaw T/ndir mismatch');
 for(const {s} of [serial,p6])assert(s.passes===passes.value&&s.quality===p5.s.quality&&s.width===p5.s.width&&s.height===p5.s.height,'XTrans stage geometry/passes drift');
 const compiled=scalar('compile-LIBRAW_USE_OPENMP'),omp=scalar('omp-compiled');assert([0,1].includes(compiled.value),'Invalid OpenMP compile branch');between(omp,ndir,hex,'omp-compiled');uint(omp.value,'omp-compiled');
 let capacity=1;
 if(compiled.value===1){assert(omp.value>0,'OpenMP compiler/runtime branch mismatch');const max=scalar('omp-max-threads');uint(max.value,max.name,1,256);capacity=max.value;for(const r of [max,scalar('omp-dynamic'),scalar('omp-nested')]){between(r,omp,hex,r.name);if(r!==max)assert([0,1].includes(r.value),'Invalid '+r.name);}}
 else {assert(omp.value===0,'Unexpected OpenMP runtime');for(const name of ['omp-max-threads','omp-dynamic','omp-nested'])assert(!rows.some(r=>r.kind==='scalar'&&r.name===name),'Unexpected serial-branch '+name);}
 const joined=one(rows,r=>r.kind==='event'&&r.name==='xtrans-parallel-region'&&r.phase==='joined','XTrans joined');between(joined,serial.p,p6.s,'XTrans joined');
 const borderEnter=one(rows,r=>r.kind==='event'&&r.name==='xtrans-border-interpolate-8'&&r.phase==='enter','XTrans border enter'),borderReturn=one(rows,r=>r.kind==='event'&&r.name==='xtrans-border-interpolate-8'&&r.phase==='return','XTrans border return');between(borderEnter,joined,borderReturn,'XTrans border entry');between(borderReturn,borderEnter,p6.s,'XTrans border return');
 const expected=new Map(),T=tileSize.value,step=T-16;assert(p5.s.width>=T&&p5.s.height>=T,'Image too small for actual XTrans path');
 for(let top=3;top<p5.s.height-19;top+=step)for(let left=3;left<p5.s.width-19;left+=step)expected.set(top+':'+left,{mrow:Math.min(top+T,p5.s.height-3),mcol:Math.min(left+T,p5.s.width-3)});
 const tiles=rows.filter(r=>r.kind==='tile'),workers=new Map(),owners=new Map();let team=null;const scratchBytes=T*T*(ndir.value*11+6);assert(tiles.length===expected.size&&tiles.length>0,'Incomplete/duplicate tile coverage');
 for(const r of tiles){between(r,serial.p,joined,'tile');for(const k of ['worker','team','top','left','mrow','mcol','scratchBytes','T','ndir'])uint(r[k],'tile.'+k);assert(r.team>=1&&r.team<=capacity&&r.worker<r.team,'Invalid worker/team');if(team===null)team=r.team;assert(team===r.team,'Inconsistent actual team');
  assert(r.T===T&&r.ndir===ndir.value&&r.scratchBytes===scratchBytes,'Invalid tile scratch algebra');const key=r.top+':'+r.left,e=expected.get(key);assert(e&&e.mrow===r.mrow&&e.mcol===r.mcol,'Invalid/duplicate tile coordinates');expected.delete(key);
  const pointer=r.scratchPointerDiagnosticOnly;assert(typeof pointer==='string'&&/^[0-9a-f]{1,16}$/i.test(pointer)&&BigInt('0x'+pointer)>0n,'Missing/invalid scratch identity');const address=BigInt('0x'+pointer),prior=workers.get(r.worker);if(prior){assert(prior.address===address,'Worker scratch changed');assert(++prior.count<=1024,'Worker trace overflow');}else workers.set(r.worker,{address,count:1});
  if(owners.has(r.top))assert(owners.get(r.top)===r.worker,'Split outer-loop row ownership');else owners.set(r.top,r.worker);
 }
 assert(expected.size===0,'Missing tile coordinates');const spans=[...workers.values()].map(w=>w.address).sort((a,b)=>a<b?-1:1);for(let i=1;i<spans.length;i++)assert(spans[i]-spans[i-1]>=BigInt(scratchBytes),'Overlapping worker scratch');
 // Scheduling/team/address evidence is validated above, never added to semantic state.
 return {records,parallel:{compiled:compiled.value,team,capacity,tileCount:tiles.length,T,ndir:ndir.value,passes:passes.value,scratchBytes}};
}

export function validateTyped(rows,states){
 validateCaptureMetadata(rows);
 for(const name of callbacks)assert.equal(named(rows,'scalar','callback-present-'+name).value,0,'Default supported capture requires no replacement callback');
 for(const name of ['compile-LIBRAW_USE_OPENMP','compile-LIBRAW_NOTHREADS','compile-NO_LCMS'])assert([0,1].includes(named(rows,'scalar',name).value),'Missing compile truth');
 const scale=named(rows,'float32-bits','actual-scale_mul');assert(hasNumbers(scale.bits,4),'Actual scale_mul missing');
 for(const id of order){const where=id==='XTRANS-serial-ready'?'xtrans-serial-green-bounds-complete':id;const st=one(rows,r=>r.kind==='state'&&r.where===where,where+' state');const end=one(rows,r=>r.kind==='payload'&&r.id===id,id+' payload');const a=rows.indexOf(st),b=rows.indexOf(end);assert(a<b,'State must precede its exact payload');const segment=rows.slice(a+1,b);
  for(const k of ['width','height','iwidth','iheight','rawWidth','rawHeight','rawPitch','left','top','flip','declaredAllocationBytes','allocWidth','allocHeight','filters','colors','shrink','fujiWidth','mixGreen','rawColor','zeroIsBad','quality','passes','subtractInline','black','maximum','dataMaximum','noAutoScale','noInterpolation','asShotWbApplied','rawOptions','userQual','cameraWb','autoWb','fourColor','highlight','medPasses','progress','warnings'])assert(Number.isFinite(st[k]),where+' missing '+k);
  assert(st.width===st.iwidth&&st.height===st.iheight&&st.width>32&&st.height>32&&st.colors===3&&st.filters===9&&st.shrink===0&&st.fujiWidth===0,'Unsupported stage geometry/CFA');
  assert(st.declaredAllocationBytes>=st.iwidth*st.iheight*8&&st.allocWidth>=st.iwidth&&st.allocHeight>=st.iheight,'Unverified initialized extent');
  assert(st.cameraWb===1&&st.autoWb===0&&st.userQual===-1&&st.noAutoScale===0&&st.noInterpolation===0,'Default control state drift');
  if(id==='P1')assert.equal(st.quality,-1,'P1 quality must remain UNAVAILABLE, never read uninitialized local');else assert(st.quality>=0,'Effective quality required after original assignment');
  if(['P5','XTRANS-serial-ready','P6','P7','P9'].includes(id))assert(st.branch==='xtrans'&&[1,3].includes(st.passes),'Actual XTrans branch and passes required');
  for(const [name,n]of [['pre_mul',4],['cam_mul',4],['user_mul',4],['rgb_cam-defined',12]])assert(hasNumbers(named(segment,'float32-bits',name).bits,n),where+' missing '+name);
  for(const [name,n]of [['gamma',6],['aber',4]]){const v=named(segment,'float64-bits',name).bits;assert(Array.isArray(v)&&v.length===n&&v.every(x=>/^[0-9a-f]{16}$/i.test(x)),where+' missing '+name);}
  const black=named(segment,'uint32-values','current-cblack-defined').values;assert(Array.isArray(black)&&black.length>=6&&black.length===6+black[4]*black[5]&&black.every(Number.isInteger),'Invalid typed cblack grid');
  assert(hasNumbers(named(segment,'uint32-values','cropbox').values,4),'Missing cropbox');
  for(const [name,n]of [['xtrans-six-by-six',36],['xtrans-absolute-six-by-six',36],['white-defined-8x8',128]]){const v=named(segment,'defined-byte-span-hash',name);assert(v.bytes===n&&isHash(v.sha256),where+' invalid '+name);}
  states[id]=semantic(rows.slice(a,b));
 }
 const wb=one(rows,r=>r.kind==='state'&&r.where==='WB-entry-before-selection','WB selection entry');
 const p3a=one(rows,r=>r.kind==='state'&&r.where==='P3a-after-WB-wavelet-before-scale-loop','P3a');assert(rows.indexOf(p3a)<rows.indexOf(scale)&&rows.indexOf(scale)<rows.indexOf(one(rows,r=>r.kind==='state'&&r.where==='P3b','P3b')),'Actual scale position');assert(rows.indexOf(wb)<rows.indexOf(p3a),'WB selection phase missing');const wbRows=rows.slice(rows.indexOf(wb),rows.indexOf(p3a));for(const name of ['pre_mul','cam_mul','user_mul'])assert(hasNumbers(named(wbRows,'float32-bits',name).bits,4),'WB effective input missing');states.P3b.push(...wbRows.map(semantic),semantic(scale));
 const lab=named(rows,'defined-byte-span-hash','cielab-initialized-cbrt');assert(lab.bytes===65536*4&&isHash(lab.sha256),'CIELab table missing');
 const matrix=rows.filter(r=>r.kind==='float32-bits'&&r.name==='cielab-effective-xyz-cam');assert(matrix.length===3&&matrix.every(r=>hasNumbers(r.bits,3)),'CIELab valid matrix rows missing');
 const hex=named(rows,'defined-byte-span-hash','xtrans-fully-defined-allhex');assert(hex.bytes===3*3*2*8*2&&isHash(hex.sha256),'allhex missing');
 const serialPayload=one(rows,r=>r.kind==='payload'&&r.id==='XTRANS-serial-ready','serial-ready');assert(rows.indexOf(lab)<rows.indexOf(serialPayload)&&rows.indexOf(hex)<rows.indexOf(serialPayload),'Serial tables must precede parallel entry');
 states['XTRANS-serial-ready'].push(semantic(lab),...matrix.map(semantic),semantic(hex));
 const p8=one(rows,r=>r.kind==='state'&&r.where==='P8-effective-out-cam','P8 state');const out=rows.filter(r=>r.kind==='float32-bits'&&r.name==='effective-out_cam-row');assert(p8.colors===3&&([0,1].includes(p8.rawColor))&&(p8.rawColor===1?out.length===0:out.length===3&&out.every(r=>hasNumbers(r.bits,3))),'Effective out_cam or explicit raw-color bypass required');assert(out.every(r=>rows.indexOf(r)>rows.indexOf(p8)&&rows.indexOf(r)<rows.indexOf(one(rows,r=>r.kind==='state'&&r.where==='P9','P9'))),'out_cam phase binding');
 states.P9.push(semantic(p8),...out.map(semantic),...rows.filter(r=>r.kind==='event'&&r.name==='out-cam').map(semantic));const histogram=named(rows,'defined-byte-span-hash','P9-initialized-histogram');assert(histogram.bytes===4*8192*4&&isHash(histogram.sha256),'Initialized histogram missing');states.P9.push(semantic(histogram));
 return true;
}
function verifiedAux(root,p,rgbPair=null){
 assert(p.valid===true&&p.borderExcluded===0&&Number.isSafeInteger(p.width)&&Number.isSafeInteger(p.height)&&p.width>32&&p.height>32&&p.sourcePitch===p.width*8&&p.packedPitch===p.width*p.channels*2&&isHash(p.sha256),'Invalid auxiliary payload layout/hash');
 const file=path.join(root,p.id+'.'+(p.channels===4?'image4':p.channels===3?'rgb':'cfa')+'.u16');
 const expected=p.width*p.height*p.channels*2;assert.equal(fs.statSync(file).size,expected,'Auxiliary file truncated');
 const h=crypto.createHash('sha256'),fd=fs.openSync(file,'r'),buf=Buffer.alloc(65536);let rgbFd=null;
 try{
  let rgbHash,projected,actual;if(rgbPair){assert(p.channels===4&&rgbPair.channels===3&&rgbPair.width===p.width&&rgbPair.height===p.height,'RGB/audit4 geometry mismatch');const rgbFile=path.join(root,rgbPair.id+'.rgb.u16');assert.equal(fs.statSync(rgbFile).size,p.width*p.height*6,'RGB file truncated');rgbFd=fs.openSync(rgbFile,'r');rgbHash=crypto.createHash('sha256');projected=Buffer.alloc(49152);actual=Buffer.alloc(49152);}
  let n;while((n=fs.readSync(fd,buf,0,buf.length,null))){h.update(buf.subarray(0,n));if(rgbPair){assert(n%8===0,'Partial image4 pixel');let used=0;for(let i=0;i<n;i+=8){buf.copy(projected,used,i,i+6);used+=6;}assert.equal(fs.readSync(rgbFd,actual,0,used,null),used,'RGB/audit4 length mismatch');assert(projected.subarray(0,used).equals(actual.subarray(0,used)),'RGB differs from audit4 slots 0/1/2');rgbHash.update(actual.subarray(0,used));}}
  assert.equal(h.digest('hex'),p.sha256,'Auxiliary file hash mismatch');if(rgbPair)assert.equal(rgbHash.digest('hex'),rgbPair.sha256,'RGB file hash mismatch');
 }finally{fs.closeSync(fd);if(rgbFd!==null)fs.closeSync(rgbFd);}
}
export function load(root){
 const rows=fs.readFileSync(path.join(root,'native-events.jsonl'),'utf8').trim().split('\n').map(x=>parseUniqueJson(x,'native-events.jsonl'));
 const result=parseUniqueJson(fs.readFileSync(path.join(root,'result.json'),'utf8'),'result.json'),identity=parseUniqueJson(fs.readFileSync(path.join(root,'run-identity.json'),'utf8'),'run-identity.json'),loaded=parseUniqueJson(fs.readFileSync(path.join(root,'loaded-native.json'),'utf8'),'loaded-native.json');
 assert.equal(result.status,'completed');assert(result.zeroRaw===false&&result.error===null,'Only a successful actual RAW host capture is comparable');
 for(const k of ['nativeSha256','bindingSha256','inputSha256','preparationSha256','contractSha256','nativeSelfTestSha256','manifestSha256','hostSha256'])assert(isHash(identity[k]),'Required run identity missing: '+k);
 assert.equal(identity.sourceCommit,'cccb97647fcee56801fa68231fa8a38aa8b52ef7');assert.equal(identity.inputSha256.toUpperCase(),'52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865');assert.equal(identity.bindingSha256.toUpperCase(),'6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3');assert.equal(identity.nativeSha256.toLowerCase(),loaded.sha256.toLowerCase());assert(identity.processId===result.runtime.ProcessId&&identity.reservationId===result.reservationId&&identity.reservationId,'Run identity/owner mismatch');assert.equal(result.runtime.environment.OMP_NUM_THREADS,'6','Original effective thread environment required');for(const name of ['OMP_DYNAMIC','OMP_THREAD_LIMIT','OMP_SCHEDULE','OMP_PROC_BIND','OMP_PLACES','KMP_AFFINITY','KMP_BLOCKTIME','OPENBLAS_NUM_THREADS','DOTNET_PROCESSOR_COUNT','COMPlus_ThreadPool_ForceMinWorkerThreads'])assert.equal(result.runtime.environment[name],null,'Missing/drifted effective environment');for(const name of ['OpenFile','Unpack','DcrawProcess','MakeDcrawMemoryImage'])assert.equal(result.counts[name],1,'Actual once-only calls required');
 const states={};validateTyped(rows,states);let last=-1;const captures=[];
 for(const id of order){const p=one(rows,r=>r.kind==='payload'&&r.id===id,id);const at=rows.indexOf(p);assert(at>last,'Checkpoint order mismatch');last=at;assert(p.valid&&p.borderExcluded===0,'Invalid/truncated domain');if(['P1','P3b','P5','XTRANS-serial-ready'].includes(id))assert(p.channels===4&&p.fullInitializedInput,'Sparse state cannot establish full input equality');else assert.equal(p.channels,3);
  assert(p.sourcePitch===p.width*8&&p.packedPitch===p.width*p.channels*2,'Invalid pitch algebra');const file=path.join(root,`${id}.${p.channels===4?'image4':'rgb'}.u16`);assert.equal(fs.statSync(file).size,p.width*p.height*p.channels*2);assert(isHash(p.sha256));
  // Fourth-slot audit is separately mandatory, never labeled RGB or alpha.
  if(['P6','P7','P9'].includes(id)){const audit=one(rows,r=>r.kind==='payload'&&r.id===id+'-audit4',id+' audit4');assert(audit.valid&&audit.channels===4&&audit.width===p.width&&audit.height===p.height&&audit.fullInitializedInput,'Missing fourth-slot state audit');verifiedAux(root,audit,p);states[id].push(semantic(audit));}
  if(['P1','P3b'].includes(id)){const cfa=one(rows,r=>r.kind==='payload'&&r.id===id+'-cfa',id+' CFA');assert(cfa.channels===1&&cfa.width===p.width&&cfa.height===p.height,'CFA companion missing');verifiedAux(root,cfa);states[id].push(semantic(cfa));}
  captures.push({...p,file,context:states[id],requiredStateComplete:true,identityVerified:true,identity});
 }
 return captures;
}
export function compareStage(a,b,csv){
 assert(a.requiredStateComplete===true&&b.requiredStateComplete===true&&a.identityVerified===true&&b.identityVerified===true,'Missing state/identity is UNKNOWN, never equal');assert(Array.isArray(a.context)&&a.context.length&&Array.isArray(b.context)&&b.context.length,'Empty context is not evidence');assert.equal(a.id,b.id);
 for(const key of ['nativeSha256','bindingSha256','inputSha256','preparationSha256','contractSha256','hostSha256','manifestSha256','nativeSelfTestSha256'])assert(isHash(a.identity[key])&&isHash(b.identity[key]),'Incomplete identity cannot compare equal: '+key);
 for(const k of ['sourceCommit','nativeSha256','bindingSha256','inputSha256','preparationSha256','contractSha256','hostSha256','manifestSha256','nativeSelfTestSha256'])assert.equal(a.identity[k],b.identity[k],'Different run/software identity: '+k);
 for(const k of ['width','height','channels','sourcePitch','packedPitch'])assert.equal(a[k],b[k],'Same-stage layout mismatch: '+k);
 const total=a.width*a.height*a.channels*2,ha=crypto.createHash('sha256'),hb=crypto.createHash('sha256');const fa=fs.openSync(a.file,'r'),fb=fs.openSync(b.file,'r'),out=fs.openSync(csv,'wx');let changed=0,max=0,sum=0,first=null,bounds=null;const x=Buffer.alloc(65536),y=Buffer.alloc(65536);fs.writeSync(out,'x,y,channel,first,second,delta\n');
 try{for(let p=0;p<total;){const n=Math.min(total-p,x.length);assert.equal(fs.readSync(fa,x,0,n,null),n);assert.equal(fs.readSync(fb,y,0,n,null),n);ha.update(x.subarray(0,n));hb.update(y.subarray(0,n));const lines=[];for(let i=0;i<n;i+=2){let v=x.readUInt16LE(i),w=y.readUInt16LE(i);if(v===w)continue;let ix=(p+i)/2,pixel=Math.floor(ix/a.channels),xx=pixel%a.width,yy=Math.floor(pixel/a.width),c=ix%a.channels,d=Math.abs(v-w);changed++;max=Math.max(max,d);sum+=d;if(!first)first={x:xx,y:yy,channel:c,first:v,second:w};if(!bounds)bounds={left:xx,right:xx,top:yy,bottom:yy};else{bounds.left=Math.min(bounds.left,xx);bounds.right=Math.max(bounds.right,xx);bounds.top=Math.min(bounds.top,yy);bounds.bottom=Math.max(bounds.bottom,yy);}lines.push(`${xx},${yy},${c},${v},${w},${w-v}\n`);}if(lines.length)fs.writeSync(out,lines.join(''));p+=n;}}
 finally{fs.closeSync(fa);fs.closeSync(fb);fs.closeSync(out);}
 const firstHash=ha.digest('hex'),secondHash=hb.digest('hex');assert.equal(firstHash,a.sha256);assert.equal(secondHash,b.sha256);const stateEqual=JSON.stringify(a.context)===JSON.stringify(b.context),imageEqual=firstHash===secondHash;
 return{id:a.id,requiredStateComplete:true,identitiesVerified:true,stateEqual,imageEqual,effectiveInputEqual:imageEqual&&stateEqual,equal:imageEqual,contextSha256First:hash(JSON.stringify(a.context)),contextSha256Second:hash(JSON.stringify(b.context)),firstHash,secondHash,changedChannels:changed,maxU16:max,meanU16:sum/(total/2),firstDifference:first,bounds};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 const[first,second,out]=process.argv.slice(2);assert(out&&!fs.existsSync(out));const a=load(first),b=load(second);assert(a[0].identity.processId!==b[0].identity.processId&&a[0].identity.reservationId!==b[0].identity.reservationId,'Two independent processes/reservations required');fs.mkdirSync(out,{recursive:true});const results=a.map((x,i)=>compareStage(x,b[i],path.join(out,x.id+'.changes.csv')));fs.writeFileSync(path.join(out,'FIRST_DIFFERENCE.json'),JSON.stringify({rawCalls:0,results,earliestDifferentPayload:results.find(x=>!x.imageEqual)?.id??null,earliestDifferentObservedBoundary:results.find(x=>!x.imageEqual||!x.stateEqual)?.id??null,status:results.every(x=>x.effectiveInputEqual)?'NEW_BUILD_THIS_PAIR_NOT_REPRODUCED_NOT_FIXED':'INTERNAL_FIRST_DIFFERENCE_OBSERVED_ROOT_CAUSE_OPEN',H1:'UNKNOWN_NO_ACTUAL_CONSUMER_LOAD_EVIDENCE',limit:'Includes mandatory serial-ready image4 and initialized CIELab/allhex. Never attribute an earlier serial difference to parallel tile loads.'},null,2)+'\n');
}
