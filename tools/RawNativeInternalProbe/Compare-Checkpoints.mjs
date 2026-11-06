import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
export const primaryOrder=['P1','P3b','P5','P6','P7','P9'];
export const order=['P1','P3b','P5','XTRANS-serial-ready','P6','P7','P9'];
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
export function semantic(v){if(Array.isArray(v))return v.map(semantic);if(v&&typeof v==='object')return Object.fromEntries(Object.keys(v).sort().filter(k=>!['imagePointerDiagnosticOnly','scratchPointerDiagnosticOnly','pointerGeneration','sequence'].includes(k)).map(k=>[k,semantic(v[k])]));return v;}
const one=(rows,predicate,why)=>{const found=rows.filter(predicate);assert.equal(found.length,1,why+' missing/duplicate');return found[0];};
const named=(rows,kind,name)=>one(rows,r=>r.kind===kind&&r.name===name,kind+':'+name);
const isHash=s=>typeof s==='string'&&/^[0-9a-f]{64}$/i.test(s);
const hasNumbers=(a,n)=>Array.isArray(a)&&a.length===n&&a.every(Number.isFinite);
const callbacks=['pre_subtractblack','pre_scalecolors','pre_preinterpolate','pre_interpolate','post_interpolate','pre_converttorgb','post_converttorgb','interpolate_bayer','interpolate_xtrans'];
export function validateTyped(rows,states){
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
 const p8=one(rows,r=>r.kind==='state'&&r.where==='P8-effective-out-cam','P8 state');const out=rows.filter(r=>r.kind==='float32-bits'&&r.name==='effective-out_cam-row');assert(p8.colors===3&&p8.rawColor===0&&out.length===3&&out.every(r=>hasNumbers(r.bits,3)),'Effective 3x3 out_cam required for supported default output');assert(out.every(r=>rows.indexOf(r)>rows.indexOf(p8)&&rows.indexOf(r)<rows.indexOf(one(rows,r=>r.kind==='state'&&r.where==='P9','P9'))),'out_cam phase binding');
 states.P9.push(semantic(p8),...out.map(semantic));const histogram=named(rows,'defined-byte-span-hash','P9-initialized-histogram');assert(histogram.bytes===4*8192*4&&isHash(histogram.sha256),'Initialized histogram missing');states.P9.push(semantic(histogram));
 return true;
}
function verifiedAux(root,p){assert(p.valid&&p.borderExcluded===0&&Number.isSafeInteger(p.width)&&Number.isSafeInteger(p.height)&&p.width>32&&p.height>32&&p.sourcePitch===p.width*8&&p.packedPitch===p.width*p.channels*2&&isHash(p.sha256),'Invalid auxiliary payload layout/hash');const file=path.join(root,`${p.id}.${p.channels===4?'image4':p.channels===3?'rgb':'cfa'}.u16`);const expected=p.width*p.height*p.channels*2;assert.equal(fs.statSync(file).size,expected,'Auxiliary file truncated');const h=crypto.createHash('sha256'),fd=fs.openSync(file,'r'),buf=Buffer.alloc(65536);try{let n;while((n=fs.readSync(fd,buf,0,buf.length,null)))h.update(buf.subarray(0,n));}finally{fs.closeSync(fd);}assert.equal(h.digest('hex'),p.sha256,'Auxiliary file hash mismatch');}
export function load(root){
 const rows=fs.readFileSync(path.join(root,'native-events.jsonl'),'utf8').trim().split('\n').map(x=>JSON.parse(x));
 const result=JSON.parse(fs.readFileSync(path.join(root,'result.json'))),identity=JSON.parse(fs.readFileSync(path.join(root,'run-identity.json'))),loaded=JSON.parse(fs.readFileSync(path.join(root,'loaded-native.json')));
 assert.equal(result.status,'completed');assert(result.zeroRaw===false&&result.error===null,'Only a successful actual RAW host capture is comparable');
 for(const k of ['nativeSha256','bindingSha256','inputSha256','preparationSha256','contractSha256','nativeSelfTestSha256','manifestSha256','hostSha256'])assert(isHash(identity[k]),'Required run identity missing: '+k);
 assert.equal(identity.sourceCommit,'cccb97647fcee56801fa68231fa8a38aa8b52ef7');assert.equal(identity.inputSha256.toUpperCase(),'52D054ED9A90306DB0B7EB5D6C408A2E006532B976AC88ED792692F9F744E865');assert.equal(identity.bindingSha256.toUpperCase(),'6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3');assert.equal(identity.nativeSha256.toLowerCase(),loaded.sha256.toLowerCase());assert(identity.processId===result.runtime.ProcessId&&identity.reservationId===result.reservationId&&identity.reservationId,'Run identity/owner mismatch');assert.equal(result.runtime.environment.OMP_NUM_THREADS,'6','Original effective thread environment required');for(const name of ['OMP_DYNAMIC','OMP_THREAD_LIMIT','OMP_SCHEDULE','OMP_PROC_BIND','OMP_PLACES','KMP_AFFINITY','KMP_BLOCKTIME','OPENBLAS_NUM_THREADS','DOTNET_PROCESSOR_COUNT','COMPlus_ThreadPool_ForceMinWorkerThreads'])assert.equal(result.runtime.environment[name],null,'Missing/drifted effective environment');for(const name of ['OpenFile','Unpack','DcrawProcess','MakeDcrawMemoryImage'])assert.equal(result.counts[name],1,'Actual once-only calls required');
 const states={};validateTyped(rows,states);let last=-1;const captures=[];
 for(const id of order){const p=one(rows,r=>r.kind==='payload'&&r.id===id,id);const at=rows.indexOf(p);assert(at>last,'Checkpoint order mismatch');last=at;assert(p.valid&&p.borderExcluded===0,'Invalid/truncated domain');if(['P1','P3b','P5','XTRANS-serial-ready'].includes(id))assert(p.channels===4&&p.fullInitializedInput,'Sparse state cannot establish full input equality');else assert.equal(p.channels,3);
  assert(p.sourcePitch===p.width*8&&p.packedPitch===p.width*p.channels*2,'Invalid pitch algebra');const file=path.join(root,`${id}.${p.channels===4?'image4':'rgb'}.u16`);assert.equal(fs.statSync(file).size,p.width*p.height*p.channels*2);assert(isHash(p.sha256));
  // Fourth-slot audit is separately mandatory, never labeled RGB or alpha.
  if(['P6','P7','P9'].includes(id)){const audit=one(rows,r=>r.kind==='payload'&&r.id===id+'-audit4',id+' audit4');assert(audit.valid&&audit.channels===4&&audit.width===p.width&&audit.height===p.height&&audit.fullInitializedInput,'Missing fourth-slot state audit');verifiedAux(root,audit);states[id].push(semantic(audit));}
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
