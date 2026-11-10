import{chooseOriginal,httpUrl,MAX_IMAGE_BYTES,validateReceipt}from"./source.mjs";
import{get,save,list}from"./queue.mjs";
const HOST="com.guangjing.capture";
const installMenu=()=>chrome.contextMenus.removeAll(()=>chrome.contextMenus.create({id:"send-to-guangjing",title:"发送到光镜",contexts:["image"]}));
chrome.runtime.onInstalled.addListener(installMenu);chrome.runtime.onStartup.addListener(installMenu);
export function native(request){return new Promise((resolve,reject)=>{
 let done=false;let port;
 try{port=chrome.runtime.connectNative(HOST);}catch{reject(new Error("本机桥接尚未安装，请运行配套注册脚本。"));return;}
 const timer=setTimeout(()=>finish(new Error("桌面暂未回执，采集仍在待处理中。")),95000);
 const finish=(error,value)=>{if(done)return;done=true;clearTimeout(timer);try{port.disconnect();}catch{}error?reject(error):resolve(value);};
 port.onMessage.addListener(reply=>finish(null,reply));port.onDisconnect.addListener(()=>{const detail=(chrome.runtime.lastError?.message||"").toLowerCase();const message=detail.includes("forbidden")?"Chrome拒绝了本机桥接访问，请核对扩展ID或公司浏览器策略；采集已保留。":detail.includes("not found")?"未找到本机桥接，请导入配套当前用户注册文件；采集已保留。":"本机桥接未连接，采集已保留。请打开光镜配套入口后重试。";finish(new Error(message));});
 port.postMessage(request);
});}
function base64(bytes){let text="";for(let i=0;i<bytes.length;i+=32768)text+=String.fromCharCode(...bytes.subarray(i,i+32768));return btoa(text);}
function fromBase64(value){const text=atob(value),bytes=new Uint8Array(text.length);for(let i=0;i<text.length;i++)bytes[i]=text.charCodeAt(i);return bytes;}
async function readOwnResponse(url){
 const response=await fetch(url,{credentials:"omit",referrerPolicy:"no-referrer",cache:"no-store"});
 if(!response.ok)throw new Error("无法取得原图（HTTP "+response.status+"），未使用页面缩略图。");
 const reader=response.body.getReader();const chunks=[];let count=0;
 while(true){const{done,value}=await reader.read();if(done)break;count+=value.length;if(count>MAX_IMAGE_BYTES){await reader.cancel();throw new Error("原图超过16 MiB，未缩小替代。");}chunks.push(value);}
 const bytes=new Uint8Array(count);let offset=0;for(const chunk of chunks){bytes.set(chunk,offset);offset+=chunk.length;}return bytes;
}
async function readPageImage(tabId,url){
 const result=await chrome.scripting.executeScript({target:{tabId},func:async(url,limit)=>{
  try{const response=await fetch(url,{credentials:"omit",referrerPolicy:"no-referrer",cache:"no-store"});if(!response.ok)return{error:"无法取得原图（HTTP "+response.status+"），未使用缩略图。"};
   const reader=response.body.getReader();const chunks=[];let n=0;while(true){const{done,value}=await reader.read();if(done)break;n+=value.length;if(n>limit){await reader.cancel();return{error:"原图超过16 MiB，未缩小替代。"};}chunks.push(value);}
   let text="";for(const chunk of chunks)for(let i=0;i<chunk.length;i+=32768)text+=String.fromCharCode(...chunk.subarray(i,i+32768));return{data:btoa(text)};
  }catch{return{permission:true,error:"当前页面不能读取原图，请在采集窗口允许读取本次图片域名后重试。"};}
 },args:[url,MAX_IMAGE_BYTES]});
 const value=result[0]?.result;if(!value||value.permission)throw new Error(value?.error||"页面已关闭，请允许读取本次图片域名后重试原图。");if(value.error)throw new Error(value.error);return fromBase64(value.data);
}
async function fetchOriginal(row){
 try{
  const origin=new URL(row.imageUrl).origin+"/*";
  const allowed=await chrome.permissions.contains({origins:[origin]});
  const bytes=allowed?await readOwnResponse(row.imageUrl):await readPageImage(row.tabId,row.imageUrl);
  if(!bytes.length)throw new Error("原图为空，未使用缩略图。");
  row.imageBytes=bytes.buffer;row.contentSha256=Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",bytes)),b=>b.toString(16).padStart(2,"0")).join("");row.status="ready";row.error="";
 }catch(error){row.status="originalUnavailable";row.error=error.message;}
 return save(row);
}
chrome.contextMenus.onClicked.addListener(async(info,tab)=>{
 if(info.menuItemId!=="send-to-guangjing"||!tab?.id)return;
 if((await list()).filter(x=>x.status!=="completed").length>=20){await chrome.windows.create({url:chrome.runtime.getURL("capture.html?queueFull=1"),type:"popup",width:460,height:710});return;}
 const id=crypto.randomUUID();let row={id,tabId:tab.id,pageUrl:info.pageUrl||tab.url,pageTitle:tab.title||"",capturedAtUtc:new Date().toISOString(),status:"reading",imageUrl:info.srcUrl,renderedImageUrl:info.srcUrl};
 try{
  row.pageUrl=httpUrl(row.pageUrl);const details=await chrome.scripting.executeScript({target:{tabId:tab.id},func:clicked=>Array.from(document.images).filter(img=>img.currentSrc===clicked||img.src===clicked).map(img=>({src:img.src,currentSrc:img.currentSrc,srcset:img.srcset,sources:Array.from(img.parentElement?.tagName==="PICTURE"?img.parentElement.querySelectorAll("source"):[]).map(s=>({srcset:s.srcset,active:!s.media||matchMedia(s.media).matches}))})),args:[info.srcUrl]});
  Object.assign(row,chooseOriginal({pageUrl:row.pageUrl,clickedUrl:info.srcUrl,images:details[0]?.result||[]}));
  let name=decodeURIComponent(new URL(row.imageUrl).pathname.split("/").pop()||"capture.jpg").replace(/[<>:"/\\|?*\x00-\x1f]/g,"_");row.fileName=name.slice(0,120)||"capture.jpg";
 }catch(error){row.status="originalUnavailable";row.error=error.message;}
 await save(row);await chrome.windows.create({url:chrome.runtime.getURL("capture.html?id="+id),type:"popup",width:470,height:760});
 if(row.status==="reading")await fetchOriginal(row);
});
async function sendCapture(id,destination){
 let row=await get(id);if(!row)throw new Error("待处理项已不存在。");if(!row.imageBytes)throw new Error(row.error||"原图尚未读取，不能入库。");
 const target={libraryId:destination.libraryId,projectId:destination.projectId||null,boardId:destination.boardId||null};
 if(!target.libraryId)throw new Error("请重新选择明确素材库。");
 if(row.attemptedDestination&&JSON.stringify(row.attemptedDestination)!==JSON.stringify(target)){
  const old=row;row={...row,id:crypto.randomUUID(),status:"ready",receipt:null,attemptedDestination:null};old.status="retargeted";old.imageBytes=null;await save(old);
 }
 row.destination=target;row.attemptedDestination=target;row.status="pending";row.error="";await save(row);
 try{
  const reply=await native({operation:"capture",capture:{captureId:row.id,destination:target,pageUrl:row.pageUrl,imageUrl:row.imageUrl,renderedImageUrl:row.renderedImageUrl,sourceSelection:row.sourceSelection,pageTitle:row.pageTitle,capturedAtUtc:row.capturedAtUtc,fileName:row.fileName,contentSha256:row.contentSha256,imageBase64:base64(new Uint8Array(row.imageBytes))}});
  if(!reply.ok)throw new Error(reply.message||"桌面未确认入库。");
  row.receipt=validateReceipt(row,reply);row.status="completed";row.imageBytes=null;row.error="";
  await chrome.storage.local.set({recentDestination:target});
 }catch(error){row.status="pending";row.error=error.message;}
 return save(row);
}
chrome.runtime.onMessage.addListener((message,_sender,respond)=>{
 (async()=>{
  if(message.type==="destinations")return native({operation:"destinations"});
  if(message.type==="fetchOriginal"){const row=await get(message.id);if(!row)throw new Error("待处理项不存在。");return fetchOriginal(row);}
  if(message.type==="send")return sendCapture(message.id,message.destination);
  throw new Error("不支持此操作。");
 })().then(value=>respond({ok:true,value}),error=>respond({ok:false,error:error.message}));return true;
});
