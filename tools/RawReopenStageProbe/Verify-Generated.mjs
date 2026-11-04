import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(dir,'../..');
const spec=JSON.parse(await fs.readFile(path.join(dir,'SOURCE_GENERATION.json'),'utf8'));
const source=await fs.readFile(path.join(root,spec.source));
const hash=b=>crypto.createHash('sha256').update(b).digest('hex').toUpperCase();
if(hash(source)!==spec.sourceSha256)throw Error('Frozen product source identity changed');
let text=source.toString('utf8').replaceAll('\r\n','\n');
for(const [from,to] of spec.replacements){if(text.split(from).length!==2)throw Error('Observation anchor is not unique: '+from);text=text.replace(from,to);}
text=spec.preamble+text;
if(hash(text)!==spec.generatedSha256)throw Error('Generation contract mismatch');
const target=path.join(dir,'ObservedLibRawDecoder.g.cs');
if(process.argv.includes('--write'))await fs.writeFile(target,text);
else if((await fs.readFile(target,'utf8'))!==text)throw Error('Generated observer source differs');
console.log(JSON.stringify({version:spec.version,sourceSha256:hash(source),generatedSha256:hash(text),unchangedProductionStatements:true}));
