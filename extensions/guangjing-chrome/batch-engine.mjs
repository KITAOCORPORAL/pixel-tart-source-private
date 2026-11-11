import*as db from"./queue.mjs";
export const MAX_SELECTED=100,MAX_PERSISTED_BYTES=64*1024*1024,ORIGINAL_LIMIT=16*1024*1024;
const now=()=>new Date().toISOString();
const targetKey=value=>JSON.stringify({libraryId:value?.libraryId||null,projectId:value?.projectId||null,boardId:value?.boardId||null});
const terminal=new Set(["completed","existing"]);
function filename(url){let name;try{name=decodeURIComponent(new URL(url).pathname.split("/").pop()||"capture.jpg");}catch{name="capture.jpg";}return name.replace(/[<>:"/\\|?*\x00-\x1f]/g,"_").slice(0,120)||"capture.jpg";}
export class BatchEngine{
 constructor(adapters,store=db){this.a=adapters;this.s=store;this.pumping=null;this.waking=null;this.metrics={activePipelines:0,maxActivePipelines:0,originalLimit:ORIGINAL_LIMIT,persistedBudget:MAX_PERSISTED_BYTES};}
 async create(scan,tabId){const batch={id:crypto.randomUUID(),pageUrl:scan.pageUrl,pageTitle:scan.pageTitle,tabId,createdAtUtc:now(),updatedAtUtc:now(),status:"scanned",pauseCode:null,error:"",destination:null,items:scan.items,truncated:scan.truncated,totalDomImages:scan.totalDomImages,cancelRequested:false};await this.s.saveBatch(batch);return batch;}
 async select(id,itemIds,value){const ids=new Set(itemIds);return this.s.mutateBatch(id,b=>{
  if(["running","cancelling"].includes(b.status))throw new Error("发送期间不能更改勾选，请先取消或等待回执。");
  b.items.forEach(item=>{if(ids.has(item.id)&&!terminal.has(item.state)&&item.state!=="sourceRejected"){item.selected=!!value;if(!value&&item.state==="queued")item.state="cancelled";else if(!value&&item.state==="pending")item.state="pendingReceipt";}});b.updatedAtUtc=now();return b;
 });}
 async start(id,destination,mode="selected"){
  if(!destination?.libraryId)throw new Error("请选择明确素材库。");
  let batch=await this.s.getBatch(id);if(!batch)throw new Error("扫描队列不存在。");
  if(["running","cancelling"].includes(batch.status))throw new Error("当前队列正在处理。");
  const selected=batch.items.filter(item=>mode==="failed"?item.state==="failed":item.selected&&!terminal.has(item.state)&&item.state!=="sourceRejected");
  if(!selected.length)throw new Error("请勾选待发送图片。");if(selected.length>MAX_SELECTED)throw new Error("每轮最多发送100张，请缩小勾选范围。");
  const captures=[];
  for(const item of selected){
   let row=item.captureId?await this.s.get(item.captureId):null;
   if(row?.attemptedDestination&&targetKey(row.attemptedDestination)!==targetKey(destination)){
    const old={...row,status:"retargeted",imageBytes:null};captures.push(old);row={...row,id:crypto.randomUUID(),status:"ready",receipt:null,attemptedDestination:null};
   }
   if(!row)row={id:crypto.randomUUID(),tabId:batch.tabId,pageUrl:batch.pageUrl,pageTitle:batch.pageTitle,imageUrl:item.imageUrl,renderedImageUrl:item.renderedImageUrl,sourceSelection:item.sourceSelection,capturedAtUtc:now(),fileName:filename(item.imageUrl),status:"batchQueued",batchId:id,batchItemId:item.id};
   row={...row,batchId:id,batchItemId:item.id,destination:{...destination},error:""};
   item.captureId=row.id;item.state="queued";item.error="";item.selected=true;captures.push(row);
  }
  batch.destination={...destination};batch.status="running";batch.pauseCode=null;batch.error="";batch.cancelRequested=false;batch.updatedAtUtc=now();
  await this.s.saveBatch(batch,captures);this.kick();return batch;
 }
 async continue(id,destination=null){
  const job=await this.s.getBatch(id);if(!job)throw new Error("队列不存在。");
  if(destination&&targetKey(destination)!==targetKey(job.destination))return this.start(id,destination,"selected");
  await this.s.mutateBatch(id,b=>{b.items.forEach(item=>{if(item.selected&&["pending","cancelled","waitingDestination"].includes(item.state)){item.state="queued";}});b.status="running";b.pauseCode=null;b.error="";b.cancelRequested=false;b.updatedAtUtc=now();return b;});this.kick();return this.s.getBatch(id);
 }
 async cancel(id){
  const job=await this.s.getBatch(id),uncertain=new Set();
  for(const item of job.items)if(["pending","sending","pendingReceipt"].includes(item.state)){const header=await this.s.getHeader(item.captureId);if(header?.attemptedDestination||item.state!=="pending")uncertain.add(item.id);}
  const result=await this.s.mutateBatch(id,b=>{
   b.cancelRequested=true;
   b.items.forEach(i=>{if(uncertain.has(i.id))i.state="pendingReceipt";else if(["queued","pending","waitingDestination"].includes(i.state))i.state="cancelled";});
   b.status=b.items.some(i=>["fetching","sending","pendingReceipt"].includes(i.state))?"cancelling":"cancelled";b.updatedAtUtc=now();return b;
  });this.kick();return result;
 }

 async verifySubmitted(id){
  await this.s.mutateBatch(id,b=>{b.cancelRequested=true;b.status="cancelling";b.pauseCode=null;b.error="";for(const i of b.items){if(i.state==="pending")i.state="pendingReceipt";}b.updatedAtUtc=now();return b;});this.kick();return this.s.getBatch(id);
 }
 async wake(){
  if(this.waking)return this.waking;
  this.waking=(async()=>{if(this.pumping)return;for(const h of await this.s.listBatches()){
   if(!["running","cancelling","cancelled"].includes(h.status))continue;
   await this.s.mutateBatch(h.id,b=>{for(const i of b.items){if(i.state==="sending")i.state="pendingReceipt";else if(i.state==="fetching")i.state=b.cancelRequested?"cancelled":"queued";}return b;});
  }this.kick();})();try{await this.waking;}finally{this.waking=null;}
 }
 kick(){if(this.pumping)return this.pumping;this.pumping=this.pump().finally(()=>{this.pumping=null;});return this.pumping;}
 async waitIdle(){while(this.pumping)await this.pumping;}
 async item(id,itemId,change){return this.s.mutateBatch(id,b=>{const item=b.items.find(x=>x.id===itemId);if(item)Object.assign(item,change);b.updatedAtUtc=now();return b;});}
 async pause(id,code,message){return this.s.mutateBatch(id,b=>{b.status="paused";b.pauseCode=code;b.error=message;b.updatedAtUtc=now();return b;});}
 async success(id,itemId,row,receipt){
  await this.s.save({...row,receipt,status:"completed",imageBytes:null,error:"",errorCode:null});
  return this.item(id,itemId,{state:receipt.outcome==="Reused"?"existing":"completed",receipt,error:""});
 }
 async reconcile(job,item){
  const row=await this.s.get(item.captureId);if(!row)return this.item(job.id,item.id,{state:"failed",error:"本地采集身份缺失，未创建替代任务。"});
  let reply;try{reply=await this.a.receipt(row);}catch(error){reply={ok:false,code:"DesktopUnavailable",message:error.message};}
  if(reply.ok&&reply.receipt){await this.success(job.id,item.id,row,reply.receipt);return true;}
  if(reply.code==="CaptureNotFound"){
   await this.item(job.id,item.id,{state:job.cancelRequested||item.selected===false?"cancelled":"queued",error:""});return true;
  }
  await this.item(job.id,item.id,{state:"pendingReceipt",error:reply.message||"桌面回执待核对。"});
  await this.pause(job.id,"ReceiptPending",reply.message||"桌面未连接，已提交项保留待核对。");return false;
 }
 async pump(){
  while(true){
   const headers=await this.s.listBatches();const h=headers.find(x=>["running","cancelling"].includes(x.status)||x.status==="cancelled");if(!h)return;
   let job=await this.s.getBatch(h.id);
   const uncertain=job.items.find(i=>i.state==="pendingReceipt");
   if(uncertain){if(!await this.reconcile(job,uncertain))continue;job=await this.s.getBatch(job.id);}
   if(job.cancelRequested){
    await this.s.mutateBatch(job.id,b=>{b.items.forEach(i=>{if(["queued","pending","waitingDestination"].includes(i.state))i.state="cancelled";});b.status="cancelledSettled";b.updatedAtUtc=now();return b;});continue;
   }
   if(job.status!=="running")continue;
   const item=job.items.find(i=>i.state==="queued"&&i.selected);
   if(!item){await this.s.mutateBatch(job.id,b=>{b.status=b.items.some(i=>i.state==="failed")?"completedWithFailures":"completed";b.updatedAtUtc=now();return b;});continue;}
   this.metrics.activePipelines++;this.metrics.maxActivePipelines=Math.max(this.metrics.maxActivePipelines,this.metrics.activePipelines);
   try{
    let row=await this.s.get(item.captureId);if(!row){await this.item(job.id,item.id,{state:"failed",error:"本地采集身份不存在，未替换。"});continue;}
    if(row.status==="completed"&&row.receipt){await this.success(job.id,item.id,row,row.receipt);continue;}
    if(row.attemptedDestination){
     await this.item(job.id,item.id,{state:"pendingReceipt"});const checked=await this.reconcile(job,{...item,state:"pendingReceipt"});if(!checked)continue;
     const latest=await this.s.getBatch(job.id);if(terminal.has(latest.items.find(x=>x.id===item.id).state))continue;
    }
    if(!row.imageBytes){
     const bytes=(await this.s.list()).filter(x=>x.batchId&&x.status!=="completed").reduce((sum,x)=>sum+(x.imageByteCount||0),0);
     if(bytes+ORIGINAL_LIMIT>MAX_PERSISTED_BYTES){await this.pause(job.id,"StorageBudget","待处理原图已接近64MiB，请先处理已有队列。");continue;}
     await this.item(job.id,item.id,{state:"fetching",error:""});row=await this.a.fetch(row); // Shared single-image original selection/byte path.
     if(!row?.imageBytes){await this.item(job.id,item.id,{state:"failed",error:row?.error||"原图获取失败，未替换缩略图。"});continue;}
    }
    job=await this.s.getBatch(job.id);
    if(job.cancelRequested){await this.item(job.id,item.id,{state:"cancelled",error:"已取消，未向桌面提交此项。"});continue;}
    await this.item(job.id,item.id,{state:"sending",error:""});row=await this.a.send(row.id,job.destination);
    if(row.status==="completed"&&row.receipt)await this.success(job.id,item.id,row,row.receipt);
    else{
     const code=row.errorCode||"BridgeUnavailable";
     if(code==="DestinationInvalid"){await this.item(job.id,item.id,{state:"waitingDestination",error:row.error});await this.pause(job.id,code,"目的地已失效，队列已暂停，请明确重选。");}
     else if(row.retryable||["BridgeUnavailable","DesktopUnavailable"].includes(code)){
      const latest=await this.s.getBatch(job.id);await this.item(job.id,item.id,{state:latest.cancelRequested?"pendingReceipt":"pending",error:row.error});
      if(latest.cancelRequested)await this.s.mutateBatch(job.id,b=>{b.status="cancelling";return b;});
      else await this.pause(job.id,code,row.error||"桌面暂不可用，队列已保留。");
     }else await this.item(job.id,item.id,{state:"failed",error:row.error||"桌面未确认此项。"});
    }
   }catch(error){await this.item(job.id,item.id,{state:"failed",error:error.message});}
   finally{this.metrics.activePipelines--;}
  }
 }
}
