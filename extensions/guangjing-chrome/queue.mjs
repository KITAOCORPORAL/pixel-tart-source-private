// Version 2 is additive: legacy single-image captures and byte buffers are retained unchanged.
const DB="guangjing-capture-v1", VERSION=2;
export function captureHeader(row){const value={...row,hasImageBytes:!!row.imageBytes,imageByteCount:row.imageBytes?.byteLength||0};delete value.imageBytes;return value;}
function batchHeader(row){return{id:row.id,pageTitle:row.pageTitle,pageUrl:row.pageUrl,status:row.status,createdAtUtc:row.createdAtUtc,updatedAtUtc:row.updatedAtUtc,itemCount:row.items?.length||0};}
export function open(){return new Promise((resolve,reject)=>{
 const request=indexedDB.open(DB,VERSION);
 request.onupgradeneeded=()=>{
  const db=request.result,tx=request.transaction;
  if(!db.objectStoreNames.contains("captures"))db.createObjectStore("captures",{keyPath:"id"});
  if(!db.objectStoreNames.contains("captureHeaders")){
   const headers=db.createObjectStore("captureHeaders",{keyPath:"id"});
   const cursor=tx.objectStore("captures").openCursor();cursor.onsuccess=()=>{const c=cursor.result;if(c){headers.put(captureHeader(c.value));c.continue();}};
  }
  if(!db.objectStoreNames.contains("batches"))db.createObjectStore("batches",{keyPath:"id"});
  if(!db.objectStoreNames.contains("batchHeaders"))db.createObjectStore("batchHeaders",{keyPath:"id"});
 };
 request.onsuccess=()=>{const db=request.result;db.onversionchange=()=>db.close();resolve(db);};request.onerror=()=>reject(request.error);
});}
async function readOne(store,id){const db=await open();try{return await new Promise((resolve,reject)=>{const q=db.transaction(store).objectStore(store).get(id);q.onsuccess=()=>resolve(q.result);q.onerror=()=>reject(q.error);});}finally{db.close();}}
async function all(store){const db=await open();try{return await new Promise((resolve,reject)=>{const q=db.transaction(store).objectStore(store).getAll();q.onsuccess=()=>resolve(q.result);q.onerror=()=>reject(q.error);});}finally{db.close();}}
export const get=id=>readOne("captures",id);
export const getHeader=id=>readOne("captureHeaders",id);
export const getBatch=id=>readOne("batches",id);
export async function list(){return(await all("captureHeaders")).sort((a,b)=>b.capturedAtUtc.localeCompare(a.capturedAtUtc));}
export async function listBatches(){return(await all("batchHeaders")).sort((a,b)=>b.createdAtUtc.localeCompare(a.createdAtUtc));}
export async function saveMany(rows){const db=await open();try{await new Promise((resolve,reject)=>{
 const tx=db.transaction(["captures","captureHeaders"],"readwrite");for(const row of rows){tx.objectStore("captures").put(row);tx.objectStore("captureHeaders").put(captureHeader(row));}
 tx.oncomplete=resolve;tx.onerror=()=>reject(tx.error);tx.onabort=()=>reject(tx.error);
});}finally{db.close();}}
export async function save(row){await saveMany([row]);return row;}
async function update(store,headers,id,change,header){const db=await open();try{return await new Promise((resolve,reject)=>{
 const tx=db.transaction([store,headers],"readwrite"),target=tx.objectStore(store);let value;
 const request=target.get(id);request.onsuccess=()=>{if(!request.result)return;try{value=change(request.result);if(!value)throw new Error("Record update cannot delete existing data");target.put(value);tx.objectStore(headers).put(header(value));}catch(error){tx.abort();reject(error);}};
 tx.oncomplete=()=>resolve(value);tx.onerror=()=>reject(tx.error);tx.onabort=()=>reject(tx.error||new Error("Local storage transaction aborted"));
});}finally{db.close();}}
export const mutate=(id,change)=>update("captures","captureHeaders",id,change,captureHeader);
export const mutateBatch=(id,change)=>update("batches","batchHeaders",id,change,batchHeader);
export async function saveBatch(batch,captures=[]){const db=await open();try{await new Promise((resolve,reject)=>{
 const tx=db.transaction(["batches","batchHeaders","captures","captureHeaders"],"readwrite");tx.objectStore("batches").put(batch);tx.objectStore("batchHeaders").put(batchHeader(batch));
 for(const row of captures){tx.objectStore("captures").put(row);tx.objectStore("captureHeaders").put(captureHeader(row));}
 tx.oncomplete=resolve;tx.onerror=()=>reject(tx.error);tx.onabort=()=>reject(tx.error);
});}finally{db.close();}return batch;}
