import{get,list}from"./queue.mjs";
const $=id=>document.getElementById(id);let id=new URLSearchParams(location.search).get("id"),row=null,targets=null,previewUrl=null,busy=false;
const rpc=async message=>{const reply=await chrome.runtime.sendMessage(message);if(!reply?.ok)throw new Error(reply?.error||"采集连接失败。");return reply.value;};
function message(text){$("status").textContent=text;}
function selected(){return{libraryId:$("library").value,projectId:$("project").value||null,boardId:$("board").value||null};}
function update(){const valid=!!$("library").value;$("send").disabled=busy||!row?.imageBytes||!targets||!valid||row.status==="completed";$("fetch").disabled=busy||row?.status==="completed"||!!row?.attemptedDestination;}
function options(element,items,label){element.replaceChildren(new Option(label,""));for(const item of items)element.add(new Option(item.name,item.id));}
function chooseSaved(target){
 if(!target)return;let invalid=false;
 for(const [field,values]of [["library",targets.libraries],["project",targets.projects],["board",targets.boards]]){
  const value=target[field+"Id"]||"";
  if(value&&!values.some(x=>x.id===value)){invalid=true;$(field).value="";}else $(field).value=value;
 }
 if(invalid){$("library").value="";message("最近目的地已失效，请重新选择。没有自动送到其他项目。");}
}
async function loadTargets(){
 try{const reply=await rpc({type:"destinations"});if(!reply.ok)throw new Error(reply.message);targets=reply.destinations;
  options($("library"),targets.libraries,"请选择素材库");options($("project"),targets.projects,"仅入素材库，不关联项目");options($("board"),targets.boards,"不加入灵感板");
  const recent=(await chrome.storage.local.get("recentDestination")).recentDestination;chooseSaved(row?.destination||recent);
  if(!targets.libraries.length)message("请先在光镜创建或打开素材库，然后刷新目的地。");
 }catch(error){targets=null;message(error.message);}update();
}
async function render(){
 if(!id){const pending=(await list()).filter(x=>x.status!=="retargeted");$("queue").replaceChildren();if(new URLSearchParams(location.search).has("queueFull"))$("queue").textContent="已有20项待处理，请先完成待处理项后再采集。";else if(!pending.length)$("queue").textContent="没有待处理采集。请在网页图片上右键，选择“发送到光镜”。";
  for(const item of pending){const button=document.createElement("button");button.className="queueItem";button.textContent=(item.status==="completed"?"已入库 · ":"待处理 · ")+(item.pageTitle||item.fileName||"图片");button.onclick=()=>{location.href="capture.html?id="+item.id;};$("queue").append(button);}return;}
 row=await get(id);if(!row){$("queue").textContent="待处理项不存在。";return;}$("queue").hidden=true;$("editor").hidden=false;
 $("source").textContent=(row.pageTitle||"网页图片")+"\n"+row.imageUrl;$("permissionHint").textContent="读取原图只会请求本次图片域名的访问权限："+new URL(row.imageUrl).hostname;
 if(previewUrl)URL.revokeObjectURL(previewUrl);previewUrl=null;$("preview").hidden=true;
 if(row.imageBytes){previewUrl=URL.createObjectURL(new Blob([row.imageBytes]));$("preview").src=previewUrl;$("preview").hidden=false;}
 if(row.status==="completed"){message("光镜已确认入库。实际素材ID见下方回执。");$("receipt").textContent=JSON.stringify(row.receipt,null,2);$("receipt").hidden=false;}
 else message(row.error||(row.status==="reading"?"正在读取本次原图…":"请选择目的地；等待光镜桌面回执。"));update();
}
$("fetch").onclick=async()=>{if(!row||busy)return;busy=true;update();try{
 const origin=new URL(row.imageUrl).origin+"/*";const granted=await chrome.permissions.request({origins:[origin]});if(!granted)throw new Error("未授权读取原图，未改用缩略图。");
 await rpc({type:"fetchOriginal",id});await render();
 }catch(error){message(error.message);}finally{busy=false;update();}};
$("reload").onclick=loadTargets;
$("send").onclick=async()=>{if(busy)return;busy=true;update();message("正在等待光镜入库回执…");try{const value=await rpc({type:"send",id,destination:selected()});id=value.id;history.replaceState(null,"","capture.html?id="+id);await render();}catch(error){message(error.message);}finally{busy=false;update();}};
for(const element of [$("library"),$("project"),$("board")])element.onchange=update;
await render();if(id)await loadTargets();
// A short visible editor poll reflects the one original read started by the right-click action.
const timer=setInterval(async()=>{if(row?.status!=="reading"){clearInterval(timer);return;}await render();},500);
