import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import assert from 'node:assert/strict';
const [repo,source,nativeRoot,binding,out]=process.argv.slice(2);assert(out&&!fs.existsSync(out),'NEW receipt path required');
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');const git=(r,...a)=>execFileSync('git',['-C',r,...a],{maxBuffer:16*1024*1024});
function pe(file){
 const b=fs.readFileSync(file);assert.equal(b.toString('ascii',0,2),'MZ');const p=b.readUInt32LE(0x3c);assert.equal(b.toString('ascii',p,p+4),'PE\0\0');
 const count=b.readUInt16LE(p+6),opt=p+24,sz=b.readUInt16LE(p+20),is64=b.readUInt16LE(opt)===0x20b;assert(is64);
 const sections=[];for(let i=0;i<count;i++){const s=opt+sz+i*40;sections.push({rva:b.readUInt32LE(s+12),virtualSize:b.readUInt32LE(s+8),size:b.readUInt32LE(s+16),offset:b.readUInt32LE(s+20)});}
 const map=r=>{const s=sections.find(s=>r>=s.rva&&r<s.rva+s.size);assert(s,'Unmapped PE RVA');return s.offset+r-s.rva;};
 const str=r=>{const s=map(r),e=b.indexOf(0,s);assert(e>=s);return b.toString('ascii',s,e);};
 const imports=[],rva=b.readUInt32LE(opt+112+8);if(rva){let i=map(rva);while(b.readUInt32LE(i)||b.readUInt32LE(i+12)||b.readUInt32LE(i+16)){imports.push(str(b.readUInt32LE(i+12)));i+=20;assert(i+20<=b.length);}}
 return {file:path.basename(file),bytes:b.length,sha256:sha(b),machine:b.readUInt16LE(p+4),optionalHeaderMagic:b.readUInt16LE(opt),imports};
}
const ref='origin/research/raw-reopen-001-contract-20261104';const lock=JSON.parse(git(repo,'show',`${ref}:docs/research/raw-reopen-001-contract-20261104/SOURCE_LOCK.json`));const lib=lock.repositories.find(x=>x.id==='libraw');
assert.equal(git(source,'rev-parse','HEAD').toString().trim(),lib.commit);
const files=lib.files.map(f=>{const b=git(source,'show',`HEAD:${f.path}`);const got=sha(b);assert.equal(got,f.gitBlobSha256,f.path);return {...f,verified:true};});
const dlls=['raw_r.dll','lcms2.dll','jpeg8.dll','zlib1.dll'].map(n=>pe(path.join(nativeRoot,n)));assert.equal(dlls[0].sha256,'f500c0732feb21b188d5b52cea05fd824d5b3c8016eb2ca68d8312acc9f914b9');
assert.equal(sha(fs.readFileSync(binding)),'6df698647c79df05e754072ed393a02315d780af7287c455d765a767df93d4a3');
const receipt={researchHead:git(repo,'rev-parse',ref).toString().trim(),sourceCommit:lib.commit,sourceFiles:files,dependencies:dlls,bindingSha256:sha(fs.readFileSync(binding)),nativeSourceEquivalence:'NOT_PROVEN',nativeCompilerFlagsAndPatches:'UNKNOWN_RUNTIME_NUSPEC_HAS_NO_COMMIT',nativeLoaded:false,rawCalls:0};fs.writeFileSync(out,JSON.stringify(receipt,null,2)+'\n');console.log(JSON.stringify({verifiedLockedSourceFiles:files.length,dependencies:dlls,nativeLoaded:false,rawCalls:0},null,2));
