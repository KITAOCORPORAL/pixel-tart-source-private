"""Generate isolated synthetic-only sibling entry and passive taps; never patch frozen natural DLL."""
from pathlib import Path
import argparse,json,hashlib,subprocess,re
p=argparse.ArgumentParser();p.add_argument('frozen_stream_source',type=Path);p.add_argument('fresh_source',type=Path);p.add_argument('fixtures',type=Path);p.add_argument('--trace',action='store_true');a=p.parse_args();root=a.fresh_source.resolve()
if subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()!='c1447c6ab618fa4b1cb11fd2fe12826855007059' or subprocess.check_output(['git','-C',str(root),'status','--porcelain'],text=True).strip():raise ValueError('Fresh pinned source required')
files=['engine/src/pipeline/engine.cpp','engine/src/pipeline/pipeline.cpp','engine/src/pipeline/pipeline.hpp','engine/src/gpu/gpu.hpp','engine/src/gpu/vulkan_gpu.cpp','engine/windows_exports.def','engine/CMakeLists.txt','API-SPEC-callable-render-service.md']
frozen=json.loads((Path(__file__).with_name('expected-stream-source.json')).read_text())
if subprocess.check_output(['git','-C',str(a.frozen_stream_source),'rev-parse','HEAD'],text=True).strip()!=frozen['baseCommit']:raise ValueError('Wrong frozen stream base')
for rel in files:
 data=(a.frozen_stream_source/rel).read_bytes();normal=data
 if rel=='engine/src/pipeline/engine.cpp':
  text=data.decode('utf-8');matches=re.findall(r'const auto allowed=std::filesystem::weakly_canonical\(std::filesystem::path\("([^"\n]+)"\)\)',text)
  if len(matches)!=1:raise ValueError('Cannot uniquely identify compiled authorized root')
  normal=data.replace(matches[0].encode(),frozen['companyNativeRoot'].encode())
 if hashlib.sha256(normal).hexdigest()!=frozen['files'][rel]:raise ValueError('Frozen stream source drift: '+rel)
for rel in files:(root/rel).write_bytes((a.frozen_stream_source/rel).read_bytes())
manifest=json.loads((a.fixtures/'fixtures.json').read_text());syntheticSource=hashlib.sha256(b'SyntheticPortraCrossDeviceV1:PUBLIC_SYNTHETIC_NO_CAMERA').hexdigest();approved=','.join('"'+x['pixelSha256']+'"' for x in manifest['fixtures'])
f=root/'engine/src/pipeline/engine.cpp';s=f.read_text();start=s.index('// SpektraStreamingResearchV1: private synchronous row ABI.');copy=s[start:];copy=copy.replace('SpektraStreamingResearchV1','SyntheticPortraStreamingDiagnosticV1').replace('spk_stream_research_v1','spk_stream_synthetic_diag_v1').replace('R8Read','DRead').replace('R8Consume','DConsume').replace('R8Poll','DPoll')
# Private helper names remain shared from prior translation-unit injection; remove duplicate helper namespace.
ns=copy.index('namespace {');ex=copy.index('extern "C" __declspec(dllexport)');copy=copy[:ns]+copy[ex:]
copy=copy.replace('"sourceSha256","inputPixelSha256"','"sourceSha256","inputPixelSha256"')
copy=copy.replace('"b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401"',json.dumps(syntheticSource))
needle='  r8_need(request.at("backendSha256").as_string()==r8_self_hash(),"Backend self identity changed");'
copy=copy.replace(needle,'  const std::vector<std::string> synthetic_hashes={'+approved+'}; r8_need(std::find(synthetic_hashes.begin(),synthetic_hashes.end(),request.at("inputPixelSha256").as_string())!=synthetic_hashes.end(),"Synthetic fixture not allowlisted");\n'+needle)
copy=copy.replace('const char* resources_dir,const char* request_json','const char* resources_dir,const char* request_json')
# Use existing callback typedefs, preserving C layout; no fictional natural source identity.
copy=copy.replace('DRead read_rows,DConsume consume,DPoll poll','R8Read read_rows,R8Consume consume,R8Poll poll')
s+='\n'+copy+'\nextern "C" __declspec(dllexport) int spk_synthetic_bt_probe_v1(const char* res,const float* in,uint32_t n,float* out) noexcept {\n try {if(!res||!in||!out||n==0||n>128)return -1;std::unique_ptr<spk_engine> e(spk_engine_create(res,nullptr));if(!e)return -2;std::string error;auto a=e->gpu->upload(in,n*4,error);auto b=e->gpu->upload(in,n*4,error);uint32_t meta[2]={n,3};if(!a||!b||!e->gpu->dispatch("spk_cctf_decode",{gpu::Arg::buf(a),gpu::Arg::inline_bytes(meta,2),gpu::Arg::buf(b)},n,error)||!e->gpu->flush(error)||!e->gpu->read(b.get(),0,out,n*4,error))return -3;return 0;}catch(...){return -4;}\n}\n';f.write_text(s)
f=root/'engine/windows_exports.def';f.write_text(f.read_text()+'\n    spk_stream_synthetic_diag_v1\n    spk_synthetic_bt_probe_v1\n')
if a.trace:
 f=root/'engine/src/pipeline/pipeline.cpp';s=f.read_text();s=s.replace('#include <chrono>','#include <chrono>\n#include <filesystem>\n#include <fstream>\n#include <cstdlib>\n#include <cstdio>')
 pos=s.index('// --- the two matrix');tap=r'''
// Passive small-image readback only. Does not change any source/output buffer.
static void synthetic_stage_tap(gpu::Gpu* gpu,const Image& image,const char* stage) {
 const char* root=std::getenv("SPK_SYNTHETIC_STAGE_DIR");if(!root||!image.valid())return;
 static unsigned serial=0;std::vector<float> data(image.elements());std::string error;
 if(!gpu->read(image.buf.get(),0,data.data(),image.bytes(),error))throw std::runtime_error("Diagnostic tap read failed");
 char name[256];std::snprintf(name,sizeof name,"%04u-%s-%u-%u-%u.f32",serial++,stage,image.h,image.w,image.c);
 std::ofstream out(std::filesystem::path(root)/name,std::ios::binary);out.write((const char*)data.data(),data.size()*4);if(!out)throw std::runtime_error("Diagnostic tap write failed");
}
''';s=s[:pos]+tap+s[pos:]
 for segment,table,count in [('film_segment','kFilmStages','kFilmStageCount'),('print_segment','kPrintStages','kPrintStageCount')]:
  begin=s.index('bool Pipeline::'+segment);end=s.index('\n}',begin);b=s[begin:end];b=b.replace('        cur = next;',f'        synthetic_stage_tap(gpu_,next.cur,{table}[i].name);\n        cur = next;');s=s[:begin]+b+s[end:]
 f.write_text(s)
# Preserve the base natural source whitelist verbatim; only the NEW entry admits synthetic identity.
check=(root/'engine/src/pipeline/engine.cpp').read_text();first=check[check.index('extern "C" __declspec(dllexport) int spk_stream_research_v1'):check.index('// SyntheticPortraStreamingDiagnosticV1:')];
if 'b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401' not in first:raise ValueError('Natural whitelist accidentally modified')
report={'version':'SyntheticDiagnosticGeneratedSourceV1','trace':a.trace,'syntheticSourceIdentity':syntheticSource,'fixtureManifestSha256':hashlib.sha256((a.fixtures/'fixtures.json').read_bytes()).hexdigest(),'naturalGuardPreserved':True,'sourceHashes':{rel:hashlib.sha256((root/rel).read_bytes()).hexdigest() for rel in files}};Path(str(root)+'.generation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))

