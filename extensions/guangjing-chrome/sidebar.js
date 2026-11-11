import{matchesFilter}from"./scan.mjs";
const $=id=>document.getElementById(id);let job=null,jobs=[],targets=null,busy=false,lastRender="",id=null;
const names={candidate:"候选",queued:"待处理",fetching:"读取原图",sending:"发送中",completed:"已入库",existing:"已有素材",failed:"失败",pending:"待桌面恢复",waitingDestination:"重选目的地",pendingReceipt:"待核对回执",cancelled:"已取消",sourceRejected:"原图无法解析"};
const rpc=async message=>{const result=await chrome.runtime.sendMessage(message);if(!result?.ok)throw new Error(result?.error||"扩展后台未连接");return result.value;};
function say(text){$("status").textContent=text;}
function destination(){return{libraryId:$("library").value,projectId:$("project").value||null,boardId:$("board").value||null};}
function filter(){return{format:$("format").value,minWidth:$("minWidth").value,minHeight:$("minHeight").value,includeUnknown:$("includeUnknown").checked};}
function visible(){return job?.items.filter(item=>matchesFilter(item,filter()))||[];}
function fill(element,values,label){const selected=element.value;element.replaceChildren(new Option(label,""));for(const value of values)element.add(new Option(value.name,value.id));if(values.some(x=>x.id===selected))element.value=selected;}
function applyDestination(target){if(!target)return;let stale=false;for(const[name,values]of[["library",targets.libraries],["project",targets.projects],["board",targets.boards]]){const value=target[name+"Id"]||"";if(value&&!values.some(x=>x.id===value)){stale=true;$(name).value="";}else $(name).value=value;}if(stale){$("library").value="";say("原目的地已失效，请明确重新选择；没有改送其他项目。");}}
async function loadTargets(){try{const result=await rpc({type:"destinations"});if(!result.ok)throw new Error(result.message);targets=result.destinations;fill($("library"),targets.libraries,"请选择素材库");fill($("project"),targets.projects,"不关联项目");fill($("board"),targets.boards,"不加入灵感板");const recent=(await chrome.storage.local.get("recentDestination")).recentDestination;applyDestination(job?.destination||recent);if(!targets.libraries.length)say("请先在配套桌面打开素材库，再刷新目的地。");}catch(error){targets=null;say(error.message);}controls();}
function controls(){const running=["running","cancelling"].includes(job?.status),selected=job?.items.filter(x=>x.selected&&!['completed','existing'].includes(x.state)).length||0;$("send").textContent="发送所选 "+selected;$("send").disabled=busy||running||!selected||!targets||!$("library").value;$("scan").disabled=busy||running;$("selectAll").disabled=busy||running||!job;$("selectNone").disabled=busy||running||!job;$("continue").disabled=busy||running||!job||!["paused","cancelled","cancelledSettled"].includes(job.status)||!targets||!$("library").value;$("retry").disabled=busy||running||!job?.items.some(x=>x.state==="failed")||!targets||!$("library").value;$("cancel").disabled=busy||!job||!job.items.some(x=>["queued","fetching","sending","pending","waitingDestination","pendingReceipt"].includes(x.state));$("reconcile").hidden=job?.pauseCode!=="ReceiptPending";}
function render(){
 if(!job){controls();return;}
 $("page").textContent=job.pageTitle+" · "+job.pageUrl+(job.truncated?" · 已达500元素扫描上限":"");
 const candidates=visible(),selected=job.items.filter(x=>x.selected).length,hiddenSelected=job.items.filter(x=>x.selected&&!candidates.some(v=>v.id===x.id)).length;
 $("counts").textContent=candidates.length+" / "+job.items.length+"张 · 已选"+selected+(hiddenSelected?"（筛选外"+hiddenSelected+"）":"");
 const done=job.items.filter(x=>['completed','existing'].includes(x.state)).length,failed=job.items.filter(x=>x.state==='failed').length,queued=job.items.filter(x=>['queued','fetching','sending','pending','waitingDestination','pendingReceipt'].includes(x.state)).length;
 $("progress").max=Math.max(1,done+failed+queued);$("progress").value=done+failed;$("progressText").textContent="成功 "+done+" · 失败 "+failed+" · 未完成 "+queued+" · "+({running:"正在逐张处理",paused:"队列已暂停",cancelling:"取消后续，核对当前回执",cancelledSettled:"已取消，已提交项已核对",completed:"完成",completedWithFailures:"完成，部分失败可重试",scanned:"尚未发送"}[job.status]||job.status);
 if(job.error)say(job.error);
 const key=job.updatedAtUtc+JSON.stringify(filter());if(key!==lastRender){lastRender=key;$("grid").replaceChildren();
  for(const item of candidates){const card=document.createElement('article');card.className='card'+(item.selected?' selected':'');
   const thumb=document.createElement('div');thumb.className='thumb';if(item.thumbnailDataUrl){const image=document.createElement('img');image.src=item.thumbnailDataUrl;image.alt='页面已有预览';image.loading='lazy';thumb.append(image);}else{const placeholder=document.createElement('span');placeholder.className='missingPreview';placeholder.textContent='预览未读取\n不下载原图补预览';thumb.append(placeholder);}
   const check=document.createElement('input');check.type='checkbox';check.className='cardCheck';check.checked=item.selected;check.disabled=['running','cancelling'].includes(job.status)||['completed','existing','sourceRejected'].includes(item.state);check.setAttribute('aria-label','勾选 '+item.imageUrl);check.onchange=()=>act(async()=>{await rpc({type:'batchSelect',id,itemIds:[item.id],selected:check.checked});await refresh();});
   const badge=document.createElement('span');badge.className='badge '+item.state;badge.textContent=names[item.state]||item.state;
   const body=document.createElement('div');body.className='cardbody';const title=document.createElement('p');title.className='name';try{title.textContent=decodeURIComponent(new URL(item.imageUrl).pathname.split('/').pop())||'图片';}catch{title.textContent='图片';}title.title=item.imageUrl;
   const dimensions=document.createElement('p');dimensions.className='meta';dimensions.textContent=(item.knownWidth||'?')+' × '+(item.knownHeight||'?')+' · '+item.format+(item.occurrences>1?' · 重复×'+item.occurrences:'');
   const origin=document.createElement('p');origin.className='source';origin.textContent=item.dimensionSource+' · '+item.sourceSelection;origin.title=item.imageUrl;
   body.append(title,dimensions,origin);if(item.error){const error=document.createElement('p');error.className='error';error.textContent=item.error;body.append(error);}if(item.receipt){const asset=document.createElement('p');asset.className='asset';asset.textContent='素材ID '+item.receipt.assetId;body.append(asset);}card.append(thumb,check,badge,body);$("grid").append(card);
  }
  if(!candidates.length){const empty=document.createElement('div');empty.className='empty';empty.textContent='当前筛选没有图片。尺寸未知项目可单独保留。';$("grid").append(empty);}
 }
 controls();
}
async function refresh(){const result=await rpc({type:'batchState',id});job=result.job;jobs=result.jobs;const chosen=$("jobs").value;$("jobs").replaceChildren(new Option('选择已保存队列',''));for(const entry of jobs)$("jobs").add(new Option(entry.pageTitle+' · '+entry.itemCount+'张',entry.id));$("jobs").value=id||chosen||'';render();}
async function act(operation){if(busy)return;busy=true;controls();try{await operation();}catch(error){say(error.message);}finally{busy=false;controls();}}
async function grantSelected(mode){const selected=job.items.filter(x=>mode==='failed'?x.state==='failed':x.selected&&!['completed','existing'].includes(x.state));const origins=[...new Set(selected.map(x=>new URL(x.imageUrl).origin+'/*'))];if(origins.length&&!await chrome.permissions.request({origins}))throw new Error('未授权所选原图域名，未启动发送。');}
$("scan").onclick=()=>act(async()=>{const result=await rpc({type:'batchScan'});id=result.id;job=result;lastRender='';say('扫描完成，仅用了页面已有预览和属性；请筛选勾选。');await refresh();await loadTargets();});
$("selectAll").onclick=()=>act(async()=>{await rpc({type:'batchSelect',id,itemIds:visible().map(x=>x.id),selected:true});await refresh();});
$("selectNone").onclick=()=>act(async()=>{await rpc({type:'batchSelect',id,itemIds:job.items.map(x=>x.id),selected:false});await refresh();});
$("send").onclick=()=>act(async()=>{await grantSelected('selected');await rpc({type:'batchStart',id,destination:destination()});say('开始逐张发送，等桌面实际回执。');await refresh();});
$("retry").onclick=()=>act(async()=>{await grantSelected('failed');await rpc({type:'batchStart',id,destination:destination(),mode:'failed'});say('仅重试失败项，保持原采集身份。');await refresh();});
$("continue").onclick=()=>act(async()=>{await grantSelected('selected');await rpc({type:'batchContinue',id,destination:destination()});say('继续保存的未完成项。');await refresh();});
$("cancel").onclick=()=>act(async()=>{await rpc({type:'batchCancel',id});say('已停止启动后续项，当前已提交项会核对回执。');await refresh();});
$("reconcile").onclick=()=>act(async()=>{await rpc({type:'batchReconcile',id});say('只查询已保存回执，不重新提交未确认导入。');await refresh();});
$("refreshDestinations").onclick=()=>act(loadTargets);$("history").onclick=()=>rpc({type:'singleHistory'});
$("jobs").onchange=()=>act(async()=>{id=$("jobs").value||null;lastRender='';await refresh();await loadTargets();});
for(const name of ['minWidth','minHeight','format','includeUnknown'])$(name).oninput=render;
for(const name of ['library','project','board'])$(name).onchange=controls;
id=(await chrome.storage.local.get('lastBatchId')).lastBatchId||null;await refresh();await loadTargets();
setInterval(()=>{if(!busy)refresh().catch(error=>say(error.message));},1200);
