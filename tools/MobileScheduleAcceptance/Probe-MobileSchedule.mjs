import fs from 'node:fs';
import vm from 'node:vm';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
const [urlFile,scriptFile,identityFile,receiptFile,action='modify']=process.argv.slice(2);
const issued=new URL(fs.readFileSync(urlFile,'utf8').trim()),identity=JSON.parse(fs.readFileSync(identityFile,'utf8'));
const source=fs.readFileSync(scriptFile,'utf8');assert.equal(await (await fetch(new URL('/calendar-client.js',issued))).text(),source);
const nodes=new Map(),requests=[];let lostReply=action==='lost-reply';
class Element{constructor(tag){this.tagName=tag;this.children=[];this.dataset={};this.value='';this.textContent='';this.hidden=false;this.disabled=false;}append(...children){this.children.push(...children);}replaceChildren(...children){this.children=children;}}
const $=id=>{if(!nodes.has(id))nodes.set(id,new Element(id==='status'?'select':'div'));return nodes.get(id);};
const document={getElementById:$,createElement:tag=>new Element(tag)};const session=new Map();const sessionStorage={getItem:k=>session.get(k)??null,setItem:(k,v)=>session.set(k,String(v)),removeItem:k=>session.delete(k)};
const location={pathname:'/calendar',hash:issued.hash};
const fetchReal=async(path,options={})=>{const url=new URL(path,issued);const response=await fetch(url,options);requests.push({method:options.method??'GET',path:url.pathname,status:response.status});if(lostReply&&options.method==='PUT'&&response.ok){lostReply=false;await response.text();throw new Error('Injected response lost after calendar commit');}return response;};
const context=vm.createContext({document,location,history:{replaceState(){location.hash='';}},sessionStorage,fetch:fetchReal,URLSearchParams,Intl,Date,crypto:crypto.webcrypto,console});
vm.runInContext(source,context,{filename:'calendar-client.js'});
const until=async(predicate,reason)=>{for(let n=0;n<150;n++){if(predicate())return;await new Promise(resolve=>setTimeout(resolve,30));}throw new Error(reason);};
await until(()=>$('message').textContent.includes('共用同一日历'),'calendar did not load');
assert.equal(location.hash,'');assert.ok($('list').children.length>=2);await $('upcoming').onclick();assert.equal($('list').children.length,3);
const item=$('list').children.find(item=>item.dataset.bookingId===identity.bookingId);assert.ok(item);await item.onclick();assert.equal($('detail').hidden,false);const originalRevision=$('version').textContent;
if(action==='readback'){
 assert.equal($('status').value,'Confirmed');assert.ok($('start').value.endsWith('13:00'));
 fs.writeFileSync(receiptFile,JSON.stringify({status:'PASS',action,browser:false,safari:false,checks:['mobile script refresh reads PC editor adjustment','time 13:00 and Confirmed status visible','server version presented'],revision:$('version').textContent,http:requests},null,2));console.log('Mobile reads PC change PASS');process.exit(0);
}
if(action==='lost-reply'){$('start').value=$('start').value.slice(0,11)+'09:15';$('end').value=$('end').value.slice(0,11)+'10:15';}else{const current=new Date($('start').value+':00Z');current.setUTCMinutes(current.getUTCMinutes()+15);$('start').value=current.toISOString().slice(0,16);const end=new Date($('end').value+':00Z');end.setUTCMinutes(end.getUTCMinutes()+15);$('end').value=end.toISOString().slice(0,16);}$('status').value='Postponed';
await $('edit').onsubmit({preventDefault(){}});
if(action==='lost-reply'){
 assert.ok(!$('receipt').textContent.includes('服务器已保存'));await $('edit').onsubmit({preventDefault(){}});assert.ok($('message').textContent.includes('未重复修改'),$('message').textContent);
}
assert.ok($('receipt').textContent.includes('服务器已保存'));assert.notEqual($('version').textContent,originalRevision);
await $('reload').onclick();assert.equal($('status').value,'Postponed');
fs.writeFileSync(receiptFile,JSON.stringify({status:'PASS',action,browser:false,safari:false,checks:['deployed customer JS equals source','day/upcoming and details loaded over real HTTPS','mobile changed time and status using server version','successful server mutation receipt displayed','reopen detail restores saved data',...(action==='lost-reply'?['same request retry after lost reply returned original receipt']:[])],revision:$('version').textContent,http:requests},null,2));console.log('Mobile JS '+action+' PASS: '+requests.length+' requests');
