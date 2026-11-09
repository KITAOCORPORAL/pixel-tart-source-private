import fs from 'node:fs';
import vm from 'node:vm';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
const [urlFile, scriptFile, receiptFile, mode='normal'] = process.argv.slice(2);
const issued = new URL(fs.readFileSync(urlFile,'utf8').trim());
const scriptText=fs.readFileSync(scriptFile,'utf8');
const servedScript=await fetch(new URL('/selection.js',issued));
assert.equal(servedScript.status,200);
assert.equal(await servedScript.text(),scriptText,'deployed customer script differs from tested source');
const inputs=[], nodes=new Map(), receipts=[], writes=[];
class Element {
 constructor(tag){this.tagName=tag;this.children=[];this.dataset={};this.disabled=false;this.hidden=false;this.checked=false;this.textContent='';}
 append(...children){this.children.push(...children);for(const child of children){if(child.tagName==='input')inputs.push(child);}}
 addEventListener(name,handler){this[name]=handler;}
}
const document={getElementById(id){if(!nodes.has(id))nodes.set(id,new Element('div'));return nodes.get(id);},createElement(tag){return new Element(tag);},querySelectorAll(selector){assert.equal(selector,'input');return inputs;}};
const storage=new Map();const sessionStorage={getItem:k=>storage.get(k)??null,setItem:(k,v)=>storage.set(k,String(v)),removeItem:k=>storage.delete(k)};
const location={pathname:issued.pathname,hash:issued.hash};let cookies='';
let confirmationFaultUsed=false;
const fetchReal=async(relative,options={})=>{
 const url=new URL(relative,issued);
 if(mode==='invalid-confirm-receipt' && options.method==='POST' && url.pathname.endsWith('/confirm') && !confirmationFaultUsed) {
   confirmationFaultUsed=true;
   return new Response(JSON.stringify({projectId:crypto.randomUUID(),selectionVersion:1,revision:999,isLocked:true,confirmedAtUtc:new Date().toISOString(),items:[]}),{status:200});
 }
 const response=await fetch(url,{...options,headers:{...options.headers,...(cookies?{Cookie:cookies}:{})}});
 if(response.headers.get('set-cookie'))cookies=response.headers.get('set-cookie').split(';')[0];
 receipts.push({method:options.method??'GET',path:url.pathname,status:response.status});
 if(options.method==='PUT')writes.push(JSON.parse(options.body));
 if(mode==='lost-confirm-reply' && options.method==='POST' && url.pathname.endsWith('/confirm') && !confirmationFaultUsed) {
   confirmationFaultUsed=true; await response.text(); throw new Error('Injected lost HTTP response after server commit');
 }
 if(mode==='receipt-then-refresh-fails' && confirmationFaultUsed && options.method==='GET' && url.pathname===issued.pathname.replace('/s/','/v1/client/selection/')) {
   throw new Error('Injected subsequent network failure');
 }
 if(mode==='receipt-then-refresh-fails' && options.method==='POST' && url.pathname.endsWith('/confirm')) confirmationFaultUsed=true;
 return response;
};
const context=vm.createContext({document,location,sessionStorage,history:{replaceState(_a,_b,pathname){location.hash='';location.pathname=pathname;}},URLSearchParams,crypto:crypto.webcrypto,fetch:fetchReal,window:{confirm:()=>true},console});
vm.runInContext(scriptText,context,{filename:'selection.js'});
const until=async(predicate,reason)=>{for(let i=0;i<150;i++){if(predicate())return;await new Promise(resolve=>setTimeout(resolve,40));}throw new Error(reason);};
await until(()=>inputs.length===1,'gallery did not populate');
await until(()=>nodes.get('message').textContent.includes('点击照片'),'client init incomplete');
assert.equal(location.hash,'');assert.equal(inputs[0].checked,false);
const toggle=async(value)=>{inputs[0].checked=value;await inputs[0].change();};
await toggle(true);await toggle(false);await toggle(true);
assert.equal(new Set(writes.map(write=>write.operationId)).size,3,'fresh toggle reused an acknowledged operation id');
assert.equal(nodes.get('count').textContent,'已选 1 张');
await nodes.get('confirm').onclick();
if(mode==='invalid-confirm-receipt') {
 assert.notEqual(nodes.get('state').textContent,'服务器已确认');assert.equal(inputs[0].disabled,false);
 assert.ok(nodes.get('message').textContent.includes('有效确认回执'));
 fs.writeFileSync(receiptFile,JSON.stringify({status:'PASS',mode,browser:false,safari:false,checks:['HTTP 200 with wrong project receipt was rejected','customer remains able to retry, not falsely confirmed'],http:receipts},null,2));
 console.log('Invalid server receipt rejection PASS'); process.exit(0);
}
assert.equal(nodes.get('state').textContent,'服务器已确认');assert.equal(inputs[0].disabled,true);
assert.ok(nodes.get('receipt').textContent.includes('服务器确认回执'));assert.equal(nodes.get('receipt').hidden,false);
if(mode==='receipt-then-refresh-fails') {
 assert.equal(confirmationFaultUsed,true);
 fs.writeFileSync(receiptFile,JSON.stringify({status:'PASS',mode,browser:false,safari:false,checks:['confirmation remains successful based on persisted server receipt even when subsequent requests fail'],http:receipts},null,2));
 console.log('Server receipt confirmation independent of subsequent refresh PASS'); process.exit(0);
}
// Reload executes the same shipped script against stored customer state.
inputs.length=0;nodes.clear();vm.runInContext(scriptText,context,{filename:'selection.js'});
await until(()=>inputs.length===1,'reloaded gallery did not populate');
assert.equal(inputs[0].checked,true);assert.equal(inputs[0].disabled,true);
assert.ok(receipts.every(item=>item.status===200),'real HTTP call failed');
fs.writeFileSync(receiptFile,JSON.stringify({status:'PASS',runtime:process.version,browser:false,safari:false,mode,checks:['shipped JS loaded protected gallery','fragment token removed from location','three toggles use distinct acknowledged operation IDs','validated server receipt locks checkboxes','reload restores selected and confirmed state'],http:receipts},null,2));
console.log('Actual customer JS + real HTTPS API PASS ('+receipts.length+' requests)');
