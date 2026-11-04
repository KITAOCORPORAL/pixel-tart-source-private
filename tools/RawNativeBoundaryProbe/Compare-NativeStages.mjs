import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const hash=b=>crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
function requireOk(v,m){if(!v)throw Error(m);}
function compare(a,b,out){
 requireOk(JSON.stringify(a.layout)===JSON.stringify(b.layout),'Different same-stage layouts');
 const {Width:w,Height:h,ValidChannels:c}=a.layout; const total=w*h*c*2;
 requireOk(Number.isSafeInteger(total)&&total>0&&total===a.bytes&&total===b.bytes,'Invalid stage lengths');
 requireOk(fs.statSync(a.path).size===total&&fs.statSync(b.path).size===total,'Truncated or extra bytes');
 const fa=fs.openSync(a.path,'r'),fb=fs.openSync(b.path,'r'),fc=fs.openSync(out,'wx');fs.writeSync(fc,'x,y,channel,first,second,secondMinusFirst\n');
 const ha=crypto.createHash('sha256'),hb=crypto.createHash('sha256');let changedChannels=0,changedPixels=0,sum=0,max=0,firstDifference=null,maximumLocation=null,bounds=null,lastPixel=-1,borderChangedChannels=0;const perChannel=Array(c).fill(0);const ba=Buffer.alloc(1048576),bb=Buffer.alloc(1048576);
 try{for(let pos=0;pos<total;){const n=Math.min(ba.length,total-pos);requireOk(fs.readSync(fa,ba,0,n,null)===n&&fs.readSync(fb,bb,0,n,null)===n,'Short read');ha.update(ba.subarray(0,n));hb.update(bb.subarray(0,n));let lines=[];
  for(let i=0;i<n;i+=2){const av=ba.readUInt16LE(i),bv=bb.readUInt16LE(i);if(av===bv)continue;const index=(pos+i)/2,pixel=Math.floor(index/c),channel=index%c,x=pixel%w,y=Math.floor(pixel/w),d=Math.abs(av-bv);const location={x,y,channel,first:av,second:bv};changedChannels++;perChannel[channel]++;if(pixel!==lastPixel){changedPixels++;lastPixel=pixel;}sum+=d;if(!firstDifference)firstDifference=location;if(d>max){max=d;maximumLocation=location;}if(!bounds)bounds={left:x,top:y,right:x,bottom:y};else{bounds.left=Math.min(bounds.left,x);bounds.right=Math.max(bounds.right,x);bounds.top=Math.min(bounds.top,y);bounds.bottom=Math.max(bounds.bottom,y);}if(x<16||y<16||x>=w-16||y>=h-16)borderChangedChannels++;lines.push(`${x},${y},${channel},${av},${bv},${bv-av}\n`);}
  if(lines.length)fs.writeSync(fc,lines.join(''));pos+=n;
 }}finally{fs.closeSync(fa);fs.closeSync(fb);fs.closeSync(fc);}
 const firstSha256=ha.digest('hex').toUpperCase(),secondSha256=hb.digest('hex').toUpperCase();requireOk(firstSha256===a.sha256&&secondSha256===b.sha256,'Frozen stage hash mismatch');return {name:a.name,layout:a.layout,firstSha256,secondSha256,equal:firstSha256===secondSha256,validSamples:total/2,changedChannels,changedPixels,perChannel,maxAbsoluteDifference:max,meanAbsoluteDifference:sum/(total/2),meanChangedAbsoluteDifference:changedChannels?sum/changedChannels:0,firstDifference,maximumLocation,bounds,borderChangedChannels,coordinates:'Within packed valid stage rectangle; add source Left/Top for original raw sites',localDifferenceCsv:out};
}
const [mode,root,output]=process.argv.slice(2);
if(mode==='--self-test'){
 requireOk(!fs.existsSync(root),'Preserve self-test');fs.mkdirSync(root,{recursive:true});let a=Buffer.from([0,0,1,0,255,255,2,0,3,0,4,0]),b=Buffer.from(a);b.writeUInt16LE(8,2);fs.writeFileSync(path.join(root,'a.bin'),a);fs.writeFileSync(path.join(root,'b.bin'),b);let stage=(p,buf)=>({name:'synthetic',path:p,bytes:buf.length,sha256:hash(buf),layout:{Width:2,Height:1,ValidChannels:3}});let r=compare(stage(path.join(root,'a.bin'),a),stage(path.join(root,'b.bin'),b),path.join(root,'changes.csv'));requireOk(r.changedChannels===1&&r.changedPixels===1&&r.maxAbsoluteDifference===7&&r.maximumLocation.channel===1,'Metrics incorrect');fs.writeFileSync(path.join(root,'self-test.json'),JSON.stringify({rawDecodeCalls:0,status:'OFFLINE_COMPARATOR_SELF_TEST_COMPLETED',result:r},null,2)+'\n');
}else if(mode==='--pair'){
 requireOk(output&&!fs.existsSync(output),'New output directory required');fs.mkdirSync(output,{recursive:true});const first=JSON.parse(fs.readFileSync(path.join(root,'first','stages.json'))),second=JSON.parse(fs.readFileSync(path.join(root,'second','stages.json')));requireOk(first.length===3&&second.length===3,'Expected exactly three stages');let results=[];for(let i=0;i<first.length;i++){requireOk(first[i].name===second[i].name,'Stage name mismatch');results.push(compare(first[i],second[i],path.join(output,first[i].name+'.changes.csv')));}fs.writeFileSync(path.join(output,'STAGE_COMPARISON.json'),JSON.stringify({version:'RAW-REOPEN-001-NativeCompareV2',rawDecodeCalls:0,results},null,2)+'\n');console.log(JSON.stringify(results.map(x=>({name:x.name,equal:x.equal,changedChannels:x.changedChannels,max:x.maxAbsoluteDifference}))));
}else throw Error('Usage: --self-test <new-dir> | --pair <pair-root> <new-comparison-dir>');
