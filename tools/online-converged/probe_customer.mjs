import fs from "node:fs";
import vm from "node:vm";
import crypto from "node:crypto";
import assert from "node:assert/strict";
const [urlFile, scriptFile, output, mode="normal", controlFile] = process.argv.slice(2);
assert.ok(!fs.existsSync(output),"Preserve previous receipt");
const issued = new URL(fs.readFileSync(urlFile,"utf8").trim());
const script=fs.readFileSync(scriptFile,"utf8");
const served=await fetch(new URL("/selection-client.js",issued)); assert.equal(served.status,200); assert.equal(await served.text(),script);
const inputs=[], nodes=new Map(), requests=[], writes=[];
class Element {
 constructor(tag){this.tagName=tag;this.children=[];this.dataset={};this.disabled=false;this.hidden=false;this.checked=false;this.textContent="";}
 append(...children){this.children.push(...children);for(const child of children)if(child.tagName==="input")inputs.push(child);}
 replaceChildren(){this.children=[];if(this===nodes.get("gallery"))inputs.length=0;}
 setAttribute(name,value){this[name]=value;}
}
const document={getElementById(id){if(!nodes.has(id))nodes.set(id,new Element("div"));return nodes.get(id);},createElement(tag){return new Element(tag);},querySelectorAll(selector){assert.equal(selector,"input[type=checkbox]");return inputs;}};
const storage=new Map(); const sessionStorage={getItem:key=>storage.get(key)??null,setItem:(key,value)=>storage.set(key,String(value)),removeItem:key=>storage.delete(key)};
const location={pathname:issued.pathname,hash:issued.hash};
const realFetch=async(path,options={})=>{
 const url=new URL(path,issued);
 try {
  const result=await fetch(url,options); requests.push({method:options.method??"GET",path:url.pathname,status:result.status});
  if(options.method==="PUT")writes.push(JSON.parse(options.body));
  return result;
 } catch(error){requests.push({method:options.method??"GET",path:url.pathname,transportFailure:true});throw error;}
};
const context=vm.createContext({document,location,sessionStorage,history:{replaceState(_a,_b,path){location.hash="";location.pathname=path;}},URL,URLSearchParams,crypto:crypto.webcrypto,fetch:realFetch,console,Date});
const until=async(predicate,message)=>{for(let i=0;i<150;i++){if(predicate())return;await new Promise(resolve=>setTimeout(resolve,40));}throw new Error(message);};
vm.runInContext(script,context,{filename:"selection-client.js"});
await until(()=>inputs.length===1 && !inputs[0].disabled,"editable gallery not ready");
assert.equal(location.hash,"");
for(const selected of [true,false,true]){inputs[0].checked=selected;await inputs[0].onchange();}
assert.equal(new Set(writes.map(x=>x.operationId)).size,3);
if(controlFile)fs.writeFileSync(controlFile,JSON.stringify({mode}));
await nodes.get("confirm").onclick();
let checks=[];
if(mode==="confirm-wrong-project"){
 assert.ok(nodes.get("status").textContent.includes("有效确认回执"));assert.equal(nodes.get("receipt").hidden,true);assert.equal(inputs[0].disabled,false);
 checks=["actual HTTP 200 wrong-project receipt rejected","no false success or lock","customer can retry"];
}else{
 assert.equal(nodes.get("receipt").hidden,false);assert.ok(nodes.get("receipt").textContent.includes("服务器确认回执"));assert.equal(inputs[0].disabled,true);assert.equal(inputs[0].checked,true);
 checks=["validated server receipt locks selected item","receipt project/version/revision/asset/time contract checked"];
 if(mode==="confirm-drop-after-commit"){assert.ok(requests.some(x=>x.transportFailure));checks.push("HTTP confirmation response lost after actual server commit","stored receipt recovered by authenticated GET");}
 if(mode!=="block-view-after-confirm"){
  inputs.length=0;nodes.clear();vm.runInContext(script,context,{filename:"selection-client.js"});
  await until(()=>inputs.length===1 && nodes.get("receipt").hidden===false,"stored confirmation not restored");
  assert.equal(inputs[0].disabled,true);assert.equal(inputs[0].checked,true);checks.push("reload restores server-persisted confirmation");
 }else checks.push("success independent of a subsequent refresh");
}
if(controlFile)fs.writeFileSync(controlFile,JSON.stringify({mode:"none"}));
fs.writeFileSync(output,JSON.stringify({status:"PASS",mode,checks,runtime:process.version,browser:false,safari:false,actualHttp:true,requests},null,2));
console.log(JSON.stringify({status:"PASS",mode,requests:requests.length}));
