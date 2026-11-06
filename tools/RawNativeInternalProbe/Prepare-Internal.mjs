import fs from 'node:fs';import path from 'node:path';import crypto from 'node:crypto';import assert from 'node:assert/strict';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const here=path.dirname(fileURLToPath(import.meta.url));export const SOURCE='cccb97647fcee56801fa68231fa8a38aa8b52ef7',REVIEW='5a4f233dea5ba11622b18380fea137c8f3944e53';
export const sha=b=>crypto.createHash('sha256').update(b).digest('hex');const git=(r,...a)=>execFileSync('git',['-C',r,...a],{maxBuffer:64*1024*1024});
export const strip=s=>s.replace(/\/\*RR001I_BEGIN\*\/[\s\S]*?\/\*RR001I_END\*\//g,'');
export function patch(source,rules,implementation=false){let edits=[];function add(anchor,code,after=false,extent=anchor.length,scope=null){let start=scope?source.indexOf(scope):0;assert(start>=0);let at=source.indexOf(anchor,start);assert(at>=0&&source.indexOf(anchor,at+1)<0,'Anchor missing/duplicate: '+anchor);edits.push({at:at+(after?extent:0),code:'/*RR001I_BEGIN*/'+code+'/*RR001I_END*/'});}add('#include "../../internal/',`${implementation?'#define RR001I_IMPLEMENTATION\n':''}#include "rr001_internal.h"\n`);rules(add,source);edits.sort((a,b)=>b.at-a.at);let result=source;for(const e of edits)result=result.slice(0,e.at)+e.code+result.slice(e.at);assert.equal(strip(result),source);return{result,insertions:edits.length};}
const processFile='src/postprocessing/dcraw_process.cpp',utilsFile='src/postprocessing/postprocessing_utils_dcrdefs.cpp',rawFile='src/preprocessing/raw2image.cpp',xFile='src/demosaic/xtrans_demosaic.cpp',blackFile='src/preprocessing/subtract_black.cpp',adjustFile='src/utils/utils_libraw.cpp',labFile='src/demosaic/ahd_demosaic.cpp';
export function instrument(files){const out={};const stage=id=>`rr001i::io(IO.shrink,IO.fuji_width,IO.mix_green,IO.raw_color,IO.zero_is_bad);rr001i::g.quality=${id==='P1'?'-1':'quality'};rr001i::stage(imgdata,"${id}",${['P1','P3b','P5'].includes(id)});`;
 out[processFile]=patch(files[processFile],a=>{
 a('    // Adjust sizes',stage('P1')+'\n');
 a('    int rc = raw2image_ex(subtract_inline);',`    rr001i::g.subtractInline=subtract_inline;
#ifdef LIBRAW_USE_OPENMP
rr001i::integer("compile-LIBRAW_USE_OPENMP",1);
#else
rr001i::integer("compile-LIBRAW_USE_OPENMP",0);
#endif
#ifdef LIBRAW_NOTHREADS
rr001i::integer("compile-LIBRAW_NOTHREADS",1);
#else
rr001i::integer("compile-LIBRAW_NOTHREADS",0);
#endif
#ifdef NO_LCMS
rr001i::integer("compile-NO_LCMS",1);
#else
rr001i::integer("compile-NO_LCMS",0);
#endif
`);
 a('    if (!O.no_auto_scale)',`    rr001i::io(IO.shrink,IO.fuji_width,IO.mix_green,IO.raw_color,IO.zero_is_bad);rr001i::g.quality=quality;rr001i::state(imgdata,"P2-after-pre-scale-callback-before-scale");\n`);
 a('    if (callbacks.pre_preinterpolate_cb)',stage('P3b')+'\n');
 a('    pre_interpolate();','\nrr001i::io(IO.shrink,IO.fuji_width,IO.mix_green,IO.raw_color,IO.zero_is_bad);rr001i::state(imgdata,"P4-after-pre-interpolate");',true);
 const leaves=[['(callbacks.interpolate_bayer_cb)(this);','custom-bayer',-1],['(callbacks.interpolate_xtrans_cb)(this);','custom-xtrans',-1],['lin_interpolate();','linear',-1],['vng_interpolate();','vng',-1],['ppg_interpolate();','ppg',-1],['xtrans_interpolate(quality > 2 ? 3 : 1);','xtrans','quality>2?3:1'],['ahd_interpolate(); // really don\'t need it here due to fallback op','ahd',-1],['dcb(iterations, dcb_enhance);','dcb',-1],['dht_interpolate();','dht',-1],['aahd_interpolate();','aahd',-1],['        ahd_interpolate();\n        imgdata.process_warnings','ahd-fallback',-1]];
 for(const [anchor,name,passes]of leaves){const call=name==='ahd-fallback'?'        ahd_interpolate();':name==='ahd'?'ahd_interpolate();':anchor;a(anchor,`{rr001i::g.branch="${name}";rr001i::g.passes=${passes};${stage('P5')}\n`);// Suffix uses the same unique full anchor and the shorter call extent.
 const i=files[processFile].indexOf(anchor); // A unique prefix position for suffix insertion without replacing the call.
 const full=files[processFile];const suffixAnchor=full.slice(i,i+call.length);a(anchor,`\n${stage('P6')} }`,true,call.length);
 }
 a('    convert_to_rgb();',stage('P7')+'\n');a('    convert_to_rgb();','\n'+stage('P9')+'\nrr001i::bytes("P9-initialized-histogram",libraw_internal_data.output_data.histogram,4*LIBRAW_HISTOGRAM_SIZE*sizeof(int));',true);
 a('    O.four_color_rgb = save_4color; // also, restore','\nrr001i::io(IO.shrink,IO.fuji_width,IO.mix_green,IO.raw_color,IO.zero_is_bad);rr001i::state(imgdata,"P10-after-postconvert-stretch-restore");',true);
 const calls=['remove_zeroes();','bad_pixels(O.bad_pixels);','subtract(O.dark_frame);','green_matching();','scale_colors();','exp_bef(expos, preser);','fbdd(noiserd);','median_filter();','blend_highlights();','recover_highlights();','fuji_rotate();','apply_profile(O.camera_profile, O.output_profile);','stretch();'];
 for(const c of calls){a(c,`{rr001i::event("${c.split('(')[0]}","enter");`);a(c,`rr001i::event("${c.split('(')[0]}","return");}`,true);}
 a('      SET_PROC_FLAG(LIBRAW_PROGRESS_MIX_GREEN);','rr001i::event("mix-green","completed");\n');
 const cb=['pre_subtractblack','pre_scalecolors','pre_preinterpolate','pre_interpolate','post_interpolate','pre_converttorgb','post_converttorgb'];
 a('    int rc = raw2image_ex(subtract_inline);',cb.concat(['interpolate_bayer','interpolate_xtrans']).map(n=>`rr001i::integer("callback-present-${n}",callbacks.${n}_cb!=nullptr);`).join('\n')+'\n');
 for(const n of cb){const call=`(callbacks.${n}_cb)(this);`;a(call,`{rr001i::event("callback-${n}","enter");`);a(call,`rr001i::event("callback-${n}","return");}`,true);}
 },true);
 out[utilsFile]=patch(files[utilsFile],a=>{
 a('  RUN_CALLBACK(LIBRAW_PROGRESS_SCALE_COLORS, 0, 2);','  rr001i::state(imgdata,"WB-entry-before-selection");\n');
 a('    memcpy(pre_mul, user_mul, sizeof pre_mul);','{rr001i::event("WB-user-multipliers","selected");');a('    memcpy(pre_mul, user_mul, sizeof pre_mul);','}',true);
 a('    memset(dsum, 0, sizeof dsum);','rr001i::event("WB-auto-or-camera-fallback","entered");\n');
 a('    if (imgdata.color.as_shot_wb_applied)','rr001i::unsigneds("WB-white8x8-sums",sum,8);\n');
 a('      pre_mul[0] = pre_mul[1] = pre_mul[2] = pre_mul[3] = 1.0;','rr001i::event("WB-as-shot-already-applied","selected");\n');
 a('      FORC4 pre_mul[c] = (float)sum[c + 4] / sum[c];','{rr001i::event("WB-white8x8","selected");');a('      FORC4 pre_mul[c] = (float)sum[c + 4] / sum[c];','}',true);
 a('      memcpy(pre_mul, cam_mul, sizeof pre_mul);','{rr001i::event("WB-camera-multipliers","selected");');a('      memcpy(pre_mul, cam_mul, sizeof pre_mul);','}',true);
 a('      imgdata.process_warnings |= LIBRAW_WARN_BAD_CAMERA_WB;','rr001i::event("WB-camera-invalid","warning");\n');
 for(const call of ['wavelet_denoise();']){a(call,`{rr001i::event("wavelet","enter");`);a(call,'rr001i::event("wavelet","return");}',true);}
 a('  scale_colors_loop(scale_mul);','rr001i::state(imgdata,"P3a-after-WB-wavelet-before-scale-loop");rr001i::bits("actual-scale_mul",scale_mul,4);\n');
 a('    for (c = 0; c < 3; c++)\n      pre_mul[c] /= cam_mul[c];','rr001i::event("WB-as-shot-daylight-undo","entered");\n');
 a('      img = (ushort *)malloc(size * sizeof *img);','rr001i::integer("aber-actual-channel",c);\n');
 a('  convert_to_rgb_loop(out_cam);','rr001i::io(shrink,fuji_width,mix_green,raw_color,zero_is_bad);rr001i::state(imgdata,"P8-effective-out-cam");\nif(!raw_color){for(int rr=0;rr<3;rr++)rr001i::bits("effective-out_cam-row",out_cam[rr],colors);}else rr001i::event("out-cam","not-consumed-raw-color-bypass");\n');
 });
 out[rawFile]=patch(files[rawFile],a=>{
 a('void LibRaw::raw2image_start()\n{','\nrr001i::event("raw2image-start","enter");rr001i::saved(imgdata);',true);
 a('    int alloc_sz = alloc_width * alloc_height;','rr001i::integer("raw2image-do-crop",do_crop);rr001i::integer("raw2image-fuji-layout",libraw_internal_data.unpacker_data.fuji_layout);\n');
 a('    // Adjust black levels','rr001i::allocation(imgdata,alloc_width,alloc_height);\n');
 for(const call of ['copy_bayer(cblack, &dmax);','copy_fuji_uncropped(cblack, &dmax);']){a(call,`rr001i::event("${call.split('(')[0]}","actual-entry");\n`);}
 a('    // Move saved bitmap to imgdata.image','rr001i::integer("raw2image-copywidth",copywidth);rr001i::integer("raw2image-copyheight",copyheight);\n',false,0,'int LibRaw::raw2image_ex(');
 });
 out[xFile]=patch(files[xFile],a=>{
 a('  size_t buffer_size = LIBRAW_AHD_TILE * LIBRAW_AHD_TILE * (ndir * 11 + 6);','\nrr001i::xbegin(LIBRAW_AHD_TILE,ndir,passes,buffer_count,buffer_size);rr001i::bytes("xtrans-fully-defined-allhex",allhex,sizeof(allhex));rr001i::state(imgdata,"xtrans-serial-green-bounds-complete");rr001i::payload(imgdata,"XTRANS-serial-ready",4);',true);
 a('            int mcol = MIN(left + LIBRAW_AHD_TILE, width - 3);','\nrr001i::tile(top,left,mrow,mcol,buffer);',true);
 a('    free_omp_buffers(buffers, buffer_count);','rr001i::xend();\n');a('    border_interpolate(8);','rr001i::event("xtrans-border-interpolate-8","enter");\n');a('    border_interpolate(8);','\nrr001i::event("xtrans-border-interpolate-8","return");',true);
 });
 out[blackFile]=patch(files[blackFile],a=>{a('int LibRaw::subtract_black_internal()\n{','\nrr001i::Scope rr_black_scope(imgdata,"subtract-black-internal");',true);for(const ret of ['    return 0;']){a(ret,'rr001i::state(imgdata,"subtract-black-exit");rr001i::event("subtract-black-internal","return");\n');}});
 out[adjustFile]=patch(files[adjustFile],a=>{a('int LibRaw::adjust_maximum()\n{','\nrr001i::Scope rr_max_scope(imgdata,"adjust-maximum");',true);a('void LibRaw::adjust_bl()\n{','\nrr001i::Scope rr_adjust_scope(imgdata,"adjust-bl");',true);});
 out[labFile]=patch(files[labFile],a=>{a('    return;\n  }\n  xyz[0]','rr001i::bytes("cielab-initialized-cbrt",cbrt,0x10000*sizeof(float));for(int rr=0;rr<3;rr++)rr001i::bits("cielab-effective-xyz-cam",xyz_cam[rr],colors);\n');});
 return out;
}
export function prepare(source,repo,out){assert(!fs.existsSync(out),'Preserve outputs');assert.equal(git(source,'rev-parse','HEAD').toString().trim(),SOURCE);assert.equal(git(source,'status','--porcelain').toString(),'');const base='docs/research/raw-reopen-001-contract-20261104/process-internal-review/';const lock=JSON.parse(git(repo,'show',REVIEW+':'+base+'INTERNAL_SOURCE_LOCK.json'));const docs=['CHECKPOINTS.md','CHECKPOINT_SPEC.json','INTERNAL_SOURCE_LOCK.json'];const records=lock.sourceRecords.filter(x=>x.project==='libraw');for(const r of records){assert.equal(sha(git(source,'show',SOURCE+':'+r.path)),r.sha256);assert.equal(r.commit,SOURCE);}const files=Object.fromEntries([processFile,utilsFile,rawFile,xFile,blackFile,adjustFile,labFile].map(p=>[p,git(source,'show',SOURCE+':'+p).toString()]));const changed=instrument(files);fs.mkdirSync(out,{recursive:true});for(const f of git(source,'ls-files','-z').toString().split('\0').filter(Boolean)){assert(!path.isAbsolute(f)&&!f.split('/').includes('..'));const p=path.join(out,f);fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,git(source,'show',SOURCE+':'+f));}for(const [p,v]of Object.entries(changed))fs.writeFileSync(path.join(out,p),v.result);for(const dir of new Set(Object.keys(changed).map(path.dirname)))fs.copyFileSync(path.join(here,'rr001_internal.h'),path.join(out,dir,'rr001_internal.h'));
 const inventory=[];function walk(dir){for(const x of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,x.name);if(x.isDirectory())walk(p);else inventory.push({path:path.relative(out,p).split(path.sep).join('/'),sha256:sha(fs.readFileSync(p))});}}walk(out);fs.writeFileSync(path.join(out,'FILES.json'),JSON.stringify(inventory,null,2)+'\n');const receipt={version:'RAW-REOPEN-001-InternalV3',status:'GENERATED_NOT_NATIVE_COMPILED',source:SOURCE,research:REVIEW,lockedLibRawFiles:records.length,originalByteRestoration:true,modifiedFiles:Object.entries(changed).map(([p,v])=>({path:p,insertions:v.insertions,originalSha256:sha(files[p]),generatedSha256:sha(v.result)})),researchDocuments:docs.map(p=>({path:base+p,sha256:sha(git(repo,'show',REVIEW+':'+base+p))})),observerHeaderSha256:sha(fs.readFileSync(path.join(here,'rr001_internal.h'))),nativeExecuted:false,rawCalls:0};fs.writeFileSync(path.join(out,'PREPARATION.json'),JSON.stringify(receipt,null,2)+'\n');return receipt;}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){const[source,repo,out]=process.argv.slice(2);console.log(JSON.stringify(prepare(source,repo,out),null,2));}
