const fs=require('fs'),vm=require('vm'),path=require('path');
const root=__dirname;const ref=path.join(root,'references/polaroid-lab-print-corrector');
const context=vm.createContext({});
vm.runInContext(fs.readFileSync(path.join(ref,'js/lut.js'),'utf8')+'\n'+fs.readFileSync(path.join(ref,'js/pipeline.js'),'utf8'),context);
const samples=JSON.parse(fs.readFileSync(path.join(root,'results/reference-input.json'),'utf8'));
context.inputs=samples;
const result=vm.runInContext(`(()=>{const lut=buildLut(filmSim);return {size:lut.size,data:Array.from(lut.data),direct:inputs.map(x=>filmSim(...x)),sampled:inputs.map(x=>sampleLut(lut,...x))};})()`,context);
fs.writeFileSync(path.join(root,'results/lab-reference-execution.json'),JSON.stringify(result));
console.log('Executed pinned MIT Lab heuristic forward transform + native JS trilinear on '+samples.length+' samples. Not measured camera model.');
