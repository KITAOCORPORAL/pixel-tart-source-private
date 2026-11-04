import fs from 'node:fs/promises';
import path from 'node:path';
const pair=process.argv[2],out=process.argv[3];
if(!pair||!out)throw Error('Usage: node Compare-Parameters.mjs <pair-directory> <new-output.json>');
function differences(a,b,p=''){if(Object.is(a,b))return[];if(a===null||b===null||typeof a!=='object'||typeof b!=='object')return[{path:p,first:a,second:b}];return [...new Set([...Object.keys(a),...Object.keys(b)])].flatMap(k=>differences(a[k],b[k],p?p+'.'+k:k));}
const result={};for(const [key,file]of Object.entries({configured:'configured-output',opened:'opened-parameters',unpacked:'unpacked-parameters',processed:'processed-parameters',runtime:'runtime'})){const a=JSON.parse(await fs.readFile(path.join(pair,'first',file+'.json'),'utf8'));const b=JSON.parse(await fs.readFile(path.join(pair,'second',file+'.json'),'utf8'));result[key]=differences(a,b);}
await fs.writeFile(out,JSON.stringify(result,null,2)+'\n',{flag:'wx'});
console.log(JSON.stringify(Object.fromEntries(Object.entries(result).map(([k,v])=>[k,{differences:v.length,paths:v.map(x=>x.path)}])),null,2));
