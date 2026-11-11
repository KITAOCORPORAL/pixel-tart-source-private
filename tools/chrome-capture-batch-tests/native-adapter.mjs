import{spawn}from'node:child_process';
export function installChromeForNative(exe,extensionId){
 const events=()=>({addListener(){}}),storage={};const stats={requests:[],active:0,maxActive:0};
 globalThis.chrome={contextMenus:{removeAll:callback=>callback?.(),create(){},onClicked:events()},action:{onClicked:events()},sidePanel:{open:async()=>{}},tabs:{query:async()=>[]},scripting:{executeScript:async()=>{throw new Error('Machine integration must not claim a Chrome DOM action');}},permissions:{contains:async()=>true},windows:{create:async()=>{}},storage:{session:{set:async()=>{}},local:{set:async values=>Object.assign(storage,values),get:async key=>({[key]:storage[key]})}},runtime:{onInstalled:events(),onStartup:events(),onMessage:events(),lastError:null,connectNative(name){
  if(name!=='com.guangjing.capture')throw new Error('Unexpected native host');const messages=[],disconnects=[];let child=null,closed=false,buffer=Buffer.alloc(0);
  const port={onMessage:{addListener:fn=>messages.push(fn)},onDisconnect:{addListener:fn=>disconnects.push(fn)},postMessage(request){
   const data=Buffer.from(JSON.stringify(request));const length=Buffer.alloc(4);length.writeUInt32LE(data.length);stats.requests.push({operation:request.operation,captureId:request.capture?.captureId||request.captureId||null,bytes:data.length});stats.active++;stats.maxActive=Math.max(stats.maxActive,stats.active);
   child=spawn(exe,['chrome-extension://'+extensionId+'/'],{windowsHide:true,stdio:['pipe','pipe','pipe']});let error='';child.stderr.on('data',data=>error+=data.toString());
   child.stdout.on('data',data=>{buffer=Buffer.concat([buffer,data]);if(buffer.length>=4){const n=buffer.readUInt32LE(0);if(buffer.length>=n+4){const reply=JSON.parse(buffer.subarray(4,n+4));buffer=buffer.subarray(n+4);for(const fn of messages)fn(reply);}}});
   child.on('error',err=>{chrome.runtime.lastError={message:err.message};for(const fn of disconnects)fn();});
   child.on('close',()=>{stats.active--;if(!closed){chrome.runtime.lastError=error?{message:error}:null;for(const fn of disconnects)fn();}});child.stdin.end(Buffer.concat([length,data]));
  },disconnect(){closed=true;child?.stdin.end();}};return port;
 }}};return stats;
}
