"use strict";
(() => {
const $ = id => document.getElementById(id);
const publicId = location.pathname.split("/").filter(Boolean).pop();
const key = "guangjing-selection/" + publicId;
let access = new URLSearchParams(location.hash.slice(1)).get("token");
try { if (access) sessionStorage.setItem(key, access); else access = sessionStorage.getItem(key); } catch {}
history.replaceState(null, "", location.pathname);
const base = "/v1/client/selection/" + encodeURIComponent(publicId);
let project, busy = false, locked = false, cursor = null, totalSelected = 0;
const loaded = new Set();
let showFileNames = true;
function message(text) { $("message").textContent = text; }
function update() { $("count").textContent = "已选 " + totalSelected + " 张"; $("confirm").disabled = busy || locked || !project; $("state").textContent = locked ? "已确认" : "受保护的相册"; document.querySelectorAll("input").forEach(input => input.disabled = busy || locked); }
async function api(path, method="GET", body) {
 const response = await fetch(base + path, {method, headers:{"X-PixelTart-Dev-Token":access || "",...(body?{"Content-Type":"application/json"}:{})},body:body?JSON.stringify(body):undefined,cache:"no-store"});
 const value = await response.json(); if(!response.ok){const error = new Error(value.message || "请求未完成，请重试。");error.code=value.code;throw error;}return value;
}
function operation(asset,selected){ const name=key+"/pending/"+asset;let value;try{value=JSON.parse(sessionStorage.getItem(name));}catch{}if(!value||value.selected!==selected){value={selected,id:crypto.randomUUID()};try{sessionStorage.setItem(name,JSON.stringify(value));}catch{}}return value;}
async function choose(asset, input) {
 busy=true;update();const op=operation(asset.selectionAssetId,input.checked);
 try {const result=await api("/choices/"+asset.selectionAssetId,"PUT",{selected:op.selected,favorite:false,expectedSelectionVersion:project.selectionVersion,expectedRevision:project.revision,operationId:op.id});project.revision=result.revision;try{sessionStorage.removeItem(key+"/pending/"+asset.selectionAssetId);}catch{}message("已保存选择。");await refresh();}
 catch(error){message(error.message);try{await refresh();const state=await api("/assets?limit=100");const item=state.items.find(value=>value.selectionAssetId===asset.selectionAssetId);if(item)input.checked=item.selected;}catch{input.checked=!op.selected;}}
 finally {busy=false;update();}
}
async function refresh(){const view=await api("");project=view.project;showFileNames=view.rule.showFileNames;locked=view.isLocked;totalSelected=view.progress.selected;$("title").textContent=project.name;$("description").textContent="为 "+project.clientDisplayName+" 准备 · 目标 "+view.rule.targetCount+" 张";if(locked){$("hint").textContent="摄影师可在电脑端收回已确认结果。";}update();}
async function page(){const result=await api("/assets?limit=50"+(cursor?"&cursor="+encodeURIComponent(cursor):""));for(const asset of result.items){if(loaded.has(asset.selectionAssetId))continue;loaded.add(asset.selectionAssetId);const label=document.createElement("label");label.className="photo";const image=document.createElement("img");image.src=asset.previewUrl;image.alt="照片 "+asset.originalFileName;image.loading="lazy";const caption=document.createElement("div");caption.className="caption";const input=document.createElement("input");input.type="checkbox";input.dataset.assetId=asset.selectionAssetId;input.checked=asset.selected;input.disabled=locked;input.addEventListener("change",()=>choose(asset,input));const name=document.createElement("span");name.textContent=showFileNames?asset.originalFileName:("照片 "+loaded.size);caption.append(input,name);label.append(image,caption);$("gallery").append(label);}cursor=result.nextCursor;$("more").hidden=!cursor;}
$("more").onclick=async()=>{try{await page();}catch(error){message(error.message);}};
let nonce;try{nonce=sessionStorage.getItem(key+"/nonce");}catch{}if(!nonce){nonce=crypto.randomUUID();try{sessionStorage.setItem(key+"/nonce",nonce);}catch{}}
$("confirm").onclick=async()=>{if(!window.confirm("确认提交当前选片？提交后将锁定，请摄影师协助修改。"))return;busy=true;update();try{await api("/confirm","POST",{confirmed:true,confirmationNonce:nonce,expectedSelectionVersion:project.selectionVersion,expectedRevision:project.revision});locked=true;message("选片已确认，请通知摄影师刷新客户结果。");await refresh();}catch(error){message(error.message);}finally{busy=false;update();}};
(async()=>{try{if(!access)throw new Error("链接缺少访问凭据，请向摄影师索取完整链接。");await refresh();await api("/media-session","POST",{});await page();message(locked?"这份选片已确认。":"点击照片下方的方框勾选。");}catch(error){$("title").textContent="暂时无法打开相册";message(error.message);$("hint").textContent="链接可能已撤销或过期，请联系摄影师。";}})();
})();