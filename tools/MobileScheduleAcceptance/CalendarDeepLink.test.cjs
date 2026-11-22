const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const source=fs.readFileSync(require('node:path').join(__dirname,'../../src/Guangjing.Schedule.Server/wwwroot/calendar-client.js'),'utf8');
const bookingId='41c744b1-d21a-462d-a865-4630519ed83a';
async function run(hash, storedToken=null, detailStatus=200){
 const elements=new Map(), requests=[], storage=new Map(storedToken?[['guangjing-schedule/v1',storedToken]]:[]);
 function element(){return {value:'',textContent:'',hidden:true,disabled:false,children:[],dataset:{},append(...children){this.children.push(...children)},replaceChildren(...children){this.children=[...children]}};}
 const $=id=>{if(!elements.has(id))elements.set(id,element());return elements.get(id)};
 $('detail');
 const location={hash,pathname:'/calendar'};let cleared=false;
 const fetch=async(path,options)=>{
  requests.push({path,options});let value, status=200;
  if(path.endsWith('/session'))value={device:{name:'test device'},timeZone:'Asia/Shanghai'};
  else if(path.includes('/bookings?'))value={items:[]};
  else if(path.endsWith('/bookings/'+bookingId)){
   status=detailStatus;value=status===200?{id:bookingId,title:'Preserved historical booking',clientDisplayName:'Synthetic client',timeZoneIana:'Asia/Shanghai',startAtUtc:'2026-11-15T02:00:00Z',endAtUtc:'2026-11-15T03:00:00Z',status:'Confirmed',revision:2}:{message:'预约不存在或已归档。'};
  }else throw new Error('Unexpected request '+path);
  return {ok:status===200,json:async()=>value};
 };
 const context=vm.createContext({document:{getElementById:$,createElement:element},location,history:{replaceState(_s,_t,path){assert.equal(path,'/calendar');cleared=true;location.hash=''}},sessionStorage:{getItem:key=>storage.get(key)||null,setItem:(key,value)=>storage.set(key,value)},fetch,URLSearchParams,Intl,Date,crypto:require('node:crypto').webcrypto});
 vm.runInContext(source,context);
 for(let i=0;i<20;i++)await new Promise(resolve=>setImmediate(resolve));
 return {elements,requests,storage,cleared};
}
test('Historical booking opens by validated fragment id and keeps revision without a write',async()=>{
 const r=await run('#token=opaque-test-token&booking='+bookingId);
 assert.equal(r.requests.filter(x=>x.path.endsWith('/bookings/'+bookingId)).length,1);
 assert.equal(r.elements.get('detail').hidden,false);assert.equal(r.elements.get('version').textContent,'当前版本 v2');
 assert.equal(r.elements.get('title').textContent,'Preserved historical booking');
 assert.ok(r.cleared);assert.ok(r.requests.every(x=>x.options.method==='GET'));
 assert.ok(r.requests.every(x=>x.options.headers.Authorization==='Bearer opaque-test-token'));
 assert.equal(r.storage.get('guangjing-schedule/v1'),'opaque-test-token');
});
test('A normal link keeps the list-only behavior',async()=>{
 const r=await run('#token=opaque-test-token');assert.equal(r.requests.length,2);assert.equal(r.elements.get('detail').hidden,true);
});
test('Malformed booking ids never enter a request path',async()=>{
 for(const value of ['../../devices','not-a-guid',bookingId+'/extra']){
  const r=await run('#token=opaque-test-token&booking='+encodeURIComponent(value));assert.equal(r.requests.length,2);
 }
});
test('A saved session can open a booking link without embedding a second token',async()=>{
 const r=await run('#booking='+bookingId,'saved-test-token');assert.equal(r.elements.get('detail').hidden,false);
 assert.ok(r.requests.every(x=>x.options.headers.Authorization==='Bearer saved-test-token'));
});
test('Missing credentials stop before all HTTP reads',async()=>{
 const r=await run('#booking='+bookingId);assert.equal(r.requests.length,0);assert.match(r.elements.get('message').textContent,/缺少手机授权/);
});
test('A missing historical booking displays the server rejection without mutation',async()=>{
 const r=await run('#token=opaque-test-token&booking='+bookingId,null,404);
 assert.equal(r.elements.get('detail').hidden,true);assert.equal(r.elements.get('message').textContent,'预约不存在或已归档。');assert.ok(r.requests.every(x=>x.options.method==='GET'));
});
