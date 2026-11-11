import assert from 'node:assert/strict';
import {indexedDB,IDBKeyRange} from 'fake-indexeddb';
import {JSDOM} from 'jsdom';
import fs from 'node:fs';
import crypto from 'node:crypto';
globalThis.indexedDB=indexedDB;globalThis.IDBKeyRange=IDBKeyRange;
const store=await import('../../extensions/guangjing-chrome/queue.mjs');
const {BatchEngine}=await import('../../extensions/guangjing-chrome/batch-engine.mjs');
const {normalizePageScan,matchesFilter}=await import('../../extensions/guangjing-chrome/scan.mjs');
const {collectPageImages}=await import('../../extensions/guangjing-chrome/page-images.mjs');
const tests=[];const test=(name,fn)=>tests.push({name,fn});
async function reset(){await new Promise((resolve,reject)=>{const r=indexedDB.deleteDatabase('guangjing-capture-v1');r.onsuccess=resolve;r.onerror=()=>reject(r.error);});}
const target={libraryId:'11111111-1111-1111-1111-111111111111',projectId:'22222222-2222-2222-2222-222222222222',boardId:null};
const item=(name)=>({id:crypto.randomUUID(),imageUrl:'https://example.test/'+name+'.jpg',renderedImageUrl:'https://example.test/'+name+'.jpg',sourceSelection:'direct',knownWidth:640,knownHeight:480,format:'JPEG',occurrences:1,selected:true,state:'candidate',captureId:null,error:''});
const scan=(names)=>({pageUrl:'https://example.test/article',pageTitle:'Synthetic current page',items:names.map(item),totalDomImages:names.length,truncated:false});
function adapters(options={}){
 const committed=new Map(),hashAssets=new Map(),stats={reads:0,sends:0,queries:0,active:0,maxActive:0};
 const api={async fetch(row){stats.reads++;if(row.imageUrl.includes('403'))return store.save({...row,status:'originalUnavailable',error:'HTTP 403, original unavailable'});const bytes=new TextEncoder().encode(row.imageUrl.includes('duplicate')?'same':'data:'+row.imageUrl);return store.save({...row,status:'ready',imageBytes:bytes.buffer,contentSha256:crypto.createHash('sha256').update(bytes).digest('hex')});},async send(id,destination){
  let row=await store.get(id);row=await store.save({...row,status:'pending',destination,attemptedDestination:destination});stats.sends++;stats.active++;stats.maxActive=Math.max(stats.maxActive,stats.active);
  try{if(options.send)return await options.send(row,destination,{stats,committed,hashAssets});
   let asset=hashAssets.get(row.contentSha256),outcome=asset?'Reused':'Imported';if(!asset){asset=crypto.randomUUID();hashAssets.set(row.contentSha256,asset);}
   const receipt={captureId:id,...destination,assetId:asset,contentSha256:row.contentSha256,outcome,pageUrl:row.pageUrl,capturedAtUtc:row.capturedAtUtc,importedAtUtc:new Date().toISOString()};committed.set(id,receipt);return store.save({...row,status:'completed',receipt,imageBytes:null});
  }finally{stats.active--;}
 },async receipt(row){stats.queries++;if(options.receipt)return options.receipt(row,{stats,committed});const receipt=committed.get(row.id);return receipt?{ok:true,code:'Completed',receipt}:{ok:false,code:'CaptureNotFound'};}};
 return{api,stats,committed,hashAssets};
}
async function prepare(engine,names){const job=await engine.create(scan(names),10);return job;}
test('additive IDB v1->v2 migration preserves legacy capture bytes, receipt and identity',async()=>{
 await reset();const legacy={id:'legacy-id',capturedAtUtc:'2026-11-01T00:00:00Z',status:'pending',imageBytes:new Uint8Array([7,8,9]).buffer,destination:target};
 const db=await new Promise((resolve,reject)=>{const r=indexedDB.open('guangjing-capture-v1',1);r.onupgradeneeded=()=>r.result.createObjectStore('captures',{keyPath:'id'});r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);});
 await new Promise(resolve=>{const tx=db.transaction('captures','readwrite');tx.objectStore('captures').put(legacy);tx.oncomplete=resolve;});db.close();
 const loaded=await store.get('legacy-id');assert.deepEqual([...new Uint8Array(loaded.imageBytes)],[7,8,9]);assert.equal(loaded.status,'pending');
 const headers=await store.list();assert.equal(headers.length,1);assert.equal(headers[0].imageBytes,undefined);assert.equal(headers[0].imageByteCount,3);
});
test('explicit DOM snapshot makes no fetch, deduplicates URLs, resolves srcset and data-src placeholders',async()=>{
 await reset();const dom=new JSDOM('<img id=a src="/ordinary.jpg"><img id=b src="/small.jpg" srcset="/small.jpg 320w, /large.jpg 1600w"><img id=c src="data:image/gif;base64,R0lG" data-src="/lazy.jpg"><img id=d src="/ordinary.jpg">',{url:'https://example.test/page'});
 globalThis.document=dom.window.document;globalThis.location=dom.window.location;globalThis.matchMedia=()=>({matches:true});let fetches=0;globalThis.fetch=()=>{fetches++;throw new Error('Scan must not fetch originals');};
 for(const image of document.images){Object.defineProperty(image,'currentSrc',{value:image.src});Object.defineProperty(image,'complete',{value:false});}
 const result=normalizePageScan(collectPageImages());assert.equal(fetches,0);assert.equal(result.items.length,3);assert.equal(result.items.find(x=>x.imageUrl.endsWith('/ordinary.jpg')).occurrences,2);assert.equal(result.items.find(x=>x.imageUrl.endsWith('/large.jpg')).knownWidth,1600);assert.equal(result.items.find(x=>x.imageUrl.endsWith('/large.jpg')).knownHeight,null);assert.equal(result.items.find(x=>x.imageUrl.endsWith('/lazy.jpg')).knownWidth,null);
});
test('unknown-size filtering stays explicit; width and format filters work without probing images',async()=>{
 const unknown={knownWidth:null,knownHeight:null,format:'JPEG'};assert.equal(matchesFilter(unknown,{minWidth:1000,includeUnknown:false}),false);assert.equal(matchesFilter(unknown,{minWidth:1000,includeUnknown:true}),true);assert.equal(matchesFilter({knownWidth:1600,knownHeight:1200,format:'PNG'},{minWidth:1000,minHeight:800,format:'JPEG'}),false);
});
test('retaining unknown dimensions cannot bypass a known width or height minimum',async()=>{
 const filter={minWidth:1000,minHeight:500,includeUnknown:true,format:'ALL'};
 assert.equal(matchesFilter({...item('narrow'),knownWidth:640,knownHeight:null},filter),false);
 assert.equal(matchesFilter({...item('short'),knownWidth:null,knownHeight:360},filter),false);
 assert.equal(matchesFilter({...item('wide'),knownWidth:1600,knownHeight:null},filter),true);
 assert.equal(matchesFilter({...item('wide'),knownWidth:1600,knownHeight:null},{...filter,includeUnknown:false}),false);
});
test('serial pipeline acknowledges each item, isolates 403 and reuses actual content identity',async()=>{
 await reset();const a=adapters(),e=new BatchEngine(a.api);const job=await prepare(e,['duplicate-a','403','duplicate-b']);await e.start(job.id,target);await e.waitIdle();const saved=await store.getBatch(job.id);assert.equal(saved.status,'completedWithFailures');assert.deepEqual(saved.items.map(x=>x.state),['completed','failed','existing']);assert.equal(saved.items[0].receipt.assetId,saved.items[2].receipt.assetId);assert.equal(e.metrics.maxActivePipelines,1);assert.equal(a.stats.maxActive,1);
});
test('failed retry keeps the same capture identity and never resends already successful items',async()=>{
 await reset();let fail=true;const a=adapters(),fetch=a.api.fetch;a.api.fetch=async row=>row.imageUrl.includes('retry')&&fail?store.save({...row,status:'originalUnavailable',error:'HTTP403'}):fetch(row);const e=new BatchEngine(a.api);const job=await prepare(e,['ok','retry']);await e.start(job.id,target);await e.waitIdle();const before=await store.getBatch(job.id),id=before.items[1].captureId,sends=a.stats.sends;fail=false;await e.start(job.id,target,'failed');await e.waitIdle();const after=await store.getBatch(job.id);assert.equal(after.items[1].captureId,id);assert.equal(a.stats.sends,sends+1);assert.equal(after.items[0].receipt.assetId,before.items[0].receipt.assetId);
});
test('cancel does not start later items and confirms the already submitted item',async()=>{
 await reset();let entered,release;const started=new Promise(r=>entered=r),gate=new Promise(r=>release=r);const a=adapters({send:async(row,d,env)=>{entered();await gate;const receipt={captureId:row.id,...d,assetId:crypto.randomUUID(),contentSha256:row.contentSha256,outcome:'Imported'};env.committed.set(row.id,receipt);return store.save({...row,status:'completed',receipt,imageBytes:null});}});const e=new BatchEngine(a.api);const job=await prepare(e,['first','second','third']);await e.start(job.id,target);await started;await e.cancel(job.id);release();await e.waitIdle();const saved=await store.getBatch(job.id);assert.equal(a.stats.sends,1);assert.equal(a.stats.reads,1);assert.equal(saved.items[0].state,'completed');assert.deepEqual(saved.items.slice(1).map(x=>x.state),['cancelled','cancelled']);
});
test('cancel after a lost committed response uses read-only receipt lookup and starts no next import',async()=>{
 await reset();let entered,release;const started=new Promise(r=>entered=r),gate=new Promise(r=>release=r);const a=adapters({send:async(row,d,env)=>{const receipt={captureId:row.id,...d,assetId:crypto.randomUUID(),contentSha256:row.contentSha256,outcome:'Imported'};env.committed.set(row.id,receipt);entered();await gate;return store.save({...row,status:'pending',errorCode:'BridgeUnavailable',retryable:true,error:'Response lost'});}});const e=new BatchEngine(a.api);const job=await prepare(e,['first','second']);await e.start(job.id,target);await started;await e.cancel(job.id);release();await e.waitIdle();const saved=await store.getBatch(job.id);assert.equal(a.stats.sends,1);assert.ok(a.stats.queries>=1);assert.equal(saved.items[0].state,'completed');assert.equal(saved.items[1].state,'cancelled');
});
test('read-only reconciliation of an uncommitted cancelled item never starts it',async()=>{
 await reset();const a=adapters(),e=new BatchEngine(a.api);const job=await prepare(e,['one']);const row=await a.api.fetch({id:crypto.randomUUID(),imageUrl:job.items[0].imageUrl,capturedAtUtc:new Date().toISOString(),destination:target,attemptedDestination:target});job.items[0].captureId=row.id;job.items[0].state='pendingReceipt';job.status='cancelling';job.cancelRequested=true;await store.saveBatch(job);await e.wake();await e.waitIdle();assert.equal(a.stats.sends,0);assert.equal((await store.getBatch(job.id)).items[0].state,'cancelled');
});
test('worker restart recovers a committed sending item by receipt without creating a replacement',async()=>{
 await reset();const a=adapters(),e=new BatchEngine(a.api);const job=await prepare(e,['one']);const row=await a.api.fetch({id:crypto.randomUUID(),imageUrl:job.items[0].imageUrl,capturedAtUtc:new Date().toISOString(),destination:target,attemptedDestination:target});const receipt={captureId:row.id,...target,assetId:crypto.randomUUID(),contentSha256:row.contentSha256,outcome:'Imported'};a.committed.set(row.id,receipt);job.items[0].captureId=row.id;job.items[0].state='sending';job.destination=target;job.status='running';await store.saveBatch(job);const reopened=new BatchEngine(a.api);await reopened.wake();await reopened.waitIdle();assert.equal(a.stats.sends,0);assert.equal((await store.getBatch(job.id)).items[0].receipt.assetId,receipt.assetId);
});
test('destination invalidation pauses before any next item; explicit reselect preserves the item and forks only its attempted operation',async()=>{
 await reset();let invalid=true;const a=adapters({send:async(row,d,env)=>{if(invalid)return store.save({...row,status:'pending',errorCode:'DestinationInvalid',error:'reselect',retryable:false});const receipt={captureId:row.id,...d,assetId:crypto.randomUUID(),contentSha256:row.contentSha256,outcome:'Imported'};env.committed.set(row.id,receipt);return store.save({...row,status:'completed',receipt,imageBytes:null});}});const e=new BatchEngine(a.api);const job=await prepare(e,['first','second']);await e.start(job.id,target);await e.waitIdle();const paused=await store.getBatch(job.id);assert.equal(paused.pauseCode,'DestinationInvalid');assert.equal(a.stats.sends,1);assert.equal(a.stats.reads,1);const oldId=paused.items[0].captureId;invalid=false;await e.continue(job.id,{...target,projectId:'33333333-3333-3333-3333-333333333333'});await e.waitIdle();const after=await store.getBatch(job.id);assert.notEqual(after.items[0].captureId,oldId);assert.equal(after.items[0].id,paused.items[0].id);assert.equal((await store.get(oldId)).status,'retargeted');assert.ok(after.items.every(x=>['completed','existing'].includes(x.state)));
});
test('metadata headers omit original buffers and batch selection is durably assigned before the first read',async()=>{
 await reset();let inspect;const a=adapters(),original=a.api.fetch;a.api.fetch=async row=>{const saved=await store.getBatch(inspect);assert.ok(saved.items.filter(x=>x.selected).every(x=>x.captureId));return original(row);};const e=new BatchEngine(a.api);const job=await prepare(e,['one','two']);inspect=job.id;await e.start(job.id,target);await e.waitIdle();assert.ok((await store.list()).every(x=>!('imageBytes'in x)));assert.ok((await store.listBatches()).every(x=>!('items'in x)));
});
test('deselecting a paused unstarted item prevents it being sent after continue',async()=>{
 await reset();let unavailable=true;const a=adapters({send:async(row,d,env)=>{if(unavailable)return store.save({...row,status:'pending',errorCode:'DesktopUnavailable',retryable:true,error:'offline'});const receipt={captureId:row.id,...d,assetId:crypto.randomUUID(),contentSha256:row.contentSha256,outcome:'Imported'};env.committed.set(row.id,receipt);return store.save({...row,status:'completed',receipt,imageBytes:null});}});const e=new BatchEngine(a.api);const job=await prepare(e,['first','second']);await e.start(job.id,target);await e.waitIdle();await e.select(job.id,[job.items[1].id],false);unavailable=false;await e.continue(job.id);await e.waitIdle();const saved=await store.getBatch(job.id);assert.equal(a.stats.sends,2);assert.equal(saved.items[1].state,'cancelled');assert.equal(saved.items[0].state,'completed');
});
test('raw pending-byte budget pauses before downloading another original',async()=>{
 await reset();const a=adapters();const wrapped={...store,list:async()=>[{batchId:'retained',status:'pending',imageByteCount:64*1024*1024}]};const e=new BatchEngine(a.api,wrapped);const job=await prepare(e,['one']);await e.start(job.id,target);await e.waitIdle();assert.equal(a.stats.reads,0);assert.equal((await store.getBatch(job.id)).pauseCode,'StorageBudget');
});
const results=[];
for(const entry of tests){try{await entry.fn();results.push({name:entry.name,status:'PASS'});console.log('PASS',entry.name);}catch(error){results.push({name:entry.name,status:'FAIL',message:error.stack});console.error('FAIL',entry.name,error.stack);}}
const output=process.argv[2];if(output)fs.writeFileSync(output,JSON.stringify({runtime:process.version,scope:'Independent DOM/IndexedDB/engine tests; not actual Chrome acceptance',results,passed:results.filter(x=>x.status==='PASS').length,failed:results.filter(x=>x.status==='FAIL').length},null,2));
if(results.some(x=>x.status==='FAIL'))process.exitCode=1;
