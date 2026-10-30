"""Generate SpektraStreamingResearchV1 only in a fresh pinned external checkout.
Calls original pixel nodes; no GPL implementation/resource vendored into product.
"""
from pathlib import Path
import argparse,subprocess,json,hashlib
p=argparse.ArgumentParser();p.add_argument('source',type=Path);p.add_argument('resources',type=Path);a=p.parse_args();root=a.source.resolve()
if subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()!='c1447c6ab618fa4b1cb11fd2fe12826855007059':raise ValueError('Wrong engine commit')
if subprocess.check_output(['git','-C',str(root),'status','--porcelain'],text=True).strip():raise ValueError('Use new clean external checkout')
if (root/'src/RAWSelectionAssistant.Core').exists():raise ValueError('Product source prohibited')
repo=Path(__file__).resolve().parents[2];case=json.loads((repo/'docs/research/color-engine-20261029/company-round6/natural-cases.json').read_text());expected=next(c for c in case['cases'] if c['localDirectory']=='portra-A-proxy')['resolved'];expected['striped']=False;expected['strip_rows']=0;expected['strip_budget_bytes']=0
hashes={p.relative_to(a.resources).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(a.resources.rglob('*')) if p.is_file()};bound=next(c for c in case['cases'] if c['localDirectory']=='portra-A-proxy')['resourceHashes']
if hashes!=bound:raise ValueError('Use unchanged frozen resources-patch collection')
# Global thread-local tracking is diagnostic/limiter for allocations made by this
# private synchronous engine on one worker. Existing pixel arithmetic is untouched.
gpu=root/'engine/src/gpu/gpu.hpp';s=gpu.read_text(encoding='utf-8');s=s.replace('namespace spk::gpu {','''namespace spk::gpu {
struct R8Limits { bool enabled=false;size_t device=0,staging=0,device_live=0,staging_live=0,device_peak=0,staging_peak=0,allocations=0; };
inline thread_local R8Limits r8_limits;
''',1);gpu.write_text(s,encoding='utf-8')
gpu=root/'engine/src/gpu/vulkan_gpu.cpp';s=gpu.read_text(encoding='utf-8');s=s.replace('    bool persistent = false;','    bool persistent = false;\n    bool r8_accounted = false;bool r8_staging = false;',1);s=s.replace('        VkMemoryAllocateInfo allocate_info{};','''        if(r8_limits.enabled) {
            const size_t live=staging?r8_limits.staging_live:r8_limits.device_live;
            const size_t cap=staging?r8_limits.staging:r8_limits.device;
            if(requirements.size>cap || live>cap-size_t(requirements.size)) {
                error=staging?"R8 staging hard budget exceeded":"R8 device hard budget exceeded";
                destroy_buffer(&b);return false;
            }
        }
        VkMemoryAllocateInfo allocate_info{};''',1)
s=s.replace('        if (transfer_timings_) {\n            std::fprintf(stderr,\n                "{\\"vulkan_allocation','''        if(r8_limits.enabled) {
            auto& live=staging?r8_limits.staging_live:r8_limits.device_live;
            live+=size_t(b.allocation_bytes);auto& peak=staging?r8_limits.staging_peak:r8_limits.device_peak;
            peak=std::max(peak,live);++r8_limits.allocations;b.r8_accounted=true;b.r8_staging=staging;
        }
        if (transfer_timings_) {
            std::fprintf(stderr,
                "{\\"vulkan_allocation''',1)
s=s.replace('    void destroy_buffer(Buffer* b) {','''    void destroy_buffer(Buffer* b) {
        if(b->memory && b->r8_accounted && r8_limits.enabled && b->allocation_bytes) {
            auto& live=b->r8_staging?r8_limits.staging_live:r8_limits.device_live;
            // Failed pre-bind allocations were not admitted/accounted.
            if(live>=size_t(b->allocation_bytes))live-=size_t(b->allocation_bytes);b->r8_accounted=false;
        }''',1);gpu.write_text(s,encoding='utf-8')
# Public only inside research engine; bypass prefix geometry which assumes whole
# input, explicitly fixing original complete frame scale once for all row calls.
h=root/'engine/src/pipeline/pipeline.hpp';s=h.read_text(encoding='utf-8');anchor='    // Bake everything that does not depend on a pixel'
s=s.replace(anchor,'''    bool r8_stream_context(uint32_t width,uint32_t height,std::string& error);
    bool r8_stream_film(const Image& in,Image& out,std::string& error);
    bool r8_stream_print(const Image& in,Image& out,std::string& error);
    uint32_t r8_stream_halo() const;
''' +anchor,1);h.write_text(s,encoding='utf-8')
pip=root/'engine/src/pipeline/pipeline.cpp';s=pip.read_text(encoding='utf-8');s+='''
namespace spk {
bool Pipeline::r8_stream_context(uint32_t width,uint32_t height,std::string& error) {
 set_source_long_edge(std::max(width,height),std::max(width,height));
 pixel_size_um_=params_.camera.film_format_mm*1000.0/double(std::max(width,height));
 progress_=nullptr;last_ae_ev_.reset();film_seed_=0x5EEDu;print_seed_=0;
 return print_prefix(error);
}
bool Pipeline::r8_stream_film(const Image& in,Image& out,std::string& error) {
 return film_segment(in,out,error);
}
bool Pipeline::r8_stream_print(const Image& in,Image& out,std::string& error) {
 return print_segment(in,out,error);
}
uint32_t Pipeline::r8_stream_halo() const { return print_scan_finish_demand().halo; }
} // namespace spk
''';pip.write_text(s,encoding='utf-8')
# Self-contained glue injected into existing engine translation unit, so its
# engine ownership and pixel routines stay the ones already under test.
cpp=r'''
// SpektraStreamingResearchV1: private synchronous row ABI. No spk_open/full planes.
#include <filesystem>
#include <windows.h>
#include <bcrypt.h>
#include <climits>
#include <stdexcept>
#include <limits>
namespace {
using R8Read=int(*)(void*,uint32_t,uint32_t,float*);
using R8Consume=int(*)(void*,uint32_t,uint32_t,const uint16_t*);
using R8Poll=int(*)(void*);
static std::atomic_flag r8_busy=ATOMIC_FLAG_INIT;
static const char* r8_expected=R"R8EXPECTED(EXPECTED_JSON)R8EXPECTED";
static const char* r8_resources=R"R8RESOURCES(RESOURCE_JSON)R8RESOURCES";
struct R8Hash {
 BCRYPT_ALG_HANDLE alg=nullptr;BCRYPT_HASH_HANDLE hash=nullptr;
 R8Hash(){if(BCryptOpenAlgorithmProvider(&alg,BCRYPT_SHA256_ALGORITHM,nullptr,0)<0||BCryptCreateHash(alg,&hash,nullptr,0,nullptr,0,0)<0)throw std::runtime_error("SHA init failed");}
 ~R8Hash(){if(hash)BCryptDestroyHash(hash);if(alg)BCryptCloseAlgorithmProvider(alg,0);}
 void add(const void*p,size_t n){if(n>ULONG_MAX||BCryptHashData(hash,(PUCHAR)p,(ULONG)n,0)<0)throw std::runtime_error("SHA update failed");}
 std::string finish(){unsigned char bytes[32];if(BCryptFinishHash(hash,bytes,32,0)<0)throw std::runtime_error("SHA finish failed");char text[65];for(int i=0;i<32;i++)std::sprintf(text+i*2,"%02x",bytes[i]);return text;}
};
std::string r8_hashfile(const std::filesystem::path& p){std::ifstream f(p,std::ios::binary);if(!f)throw std::runtime_error("Hash input unavailable");R8Hash h;std::vector<char>b(1024*1024);while(f){f.read(b.data(),b.size());h.add(b.data(),size_t(f.gcount()));}return h.finish();}
void r8_need(bool ok,const char* text){if(!ok)throw std::runtime_error(text);}
std::string r8_self_hash(){HMODULE module=nullptr;r8_need(GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS|GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,(LPCWSTR)&r8_self_hash,&module),"Cannot locate self DLL");std::vector<wchar_t> path(32768);r8_need(GetModuleFileNameW(module,path.data(),(DWORD)path.size())>0,"Cannot get self path");return r8_hashfile(std::filesystem::path(path.data()));}
}
extern "C" __declspec(dllexport) int spk_stream_research_v1(
 const char* resources_dir,const char* request_json,uint32_t width,uint32_t height,
 uint32_t requested_rows,uint64_t host_cap,uint64_t device_cap,uint64_t staging_cap,
 R8Read read_rows,R8Consume consume,R8Poll poll,void* user,char** out_json) noexcept {
 const bool occupied=r8_busy.test_and_set();
 if(occupied){if(out_json)*out_json=nullptr;return -30;}
 struct BusyGuard{~BusyGuard(){r8_busy.clear();}} busy_guard;
 gpu::r8_limits={};auto start=std::chrono::steady_clock::now();Json report=Json::object();report.set("version",Json(std::string("SpektraStreamingResearchV1")));report.set("success",Json(false));
 std::unique_ptr<spk_engine> engine;std::unique_ptr<Pipeline> pipeline;int status=-1;
 size_t host_peak=0;uint32_t rows=0,halo=0;double read_ms=0,upload_ms=0,compute_ms=0,readback_ms=0,store_ms=0,consumer_ms=0,init_ms=0;size_t gpu_after=0,stage_after=0;std::filesystem::path store_path;
 try {
  r8_need(out_json && resources_dir && request_json && read_rows && consume && poll,"Invalid stream arguments");*out_json=nullptr;
  r8_need(width>0&&height>0&&width<=20000&&height<=20000&&uint64_t(width)*height<=(1ull<<31)/3,"Invalid frame dimensions");
  r8_need(requested_rows>0&&host_cap<=512ull*1024*1024&&device_cap<=2ull*1024*1024*1024&&staging_cap<=256ull*1024*1024,"Invalid research caps");
  Json request,expected,resource_manifest;std::string error;
  r8_need(Json::parse(request_json,request,error)&&request.is_object(),"Invalid request JSON");
  const std::vector<std::string> keys={"version","route","domain","orientation","sourceSha256","inputPixelSha256","backendSha256","resourceManifestSha256","resolved","negativeStore"};
  r8_need(request.fields().size()==keys.size(),"Unknown/missing request field");for(const auto& k:keys)r8_need(request.has(k),"Missing request field");
  r8_need(request.at("version").as_string()=="SpektraStreamingResearchV1"&&request.at("route").as_string()=="A"&&request.at("domain").as_string()=="standard ProPhoto RGB D50 linear float32; negatives/>1 retained"&&request.at("orientation").as_int()==1,"Unknown stream input contract");
  r8_need(request.at("sourceSha256").as_string()=="b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401"&&request.at("inputPixelSha256").as_string().size()==64,"Unknown authorized source/input identity");
  r8_need(request.at("backendSha256").as_string()==r8_self_hash(),"Backend self identity changed");
  r8_need(Json::parse(r8_expected,expected,error)&&request.at("resolved").dump()==expected.dump(),"Unknown resolved parameter/effect");
  r8_need(Json::parse(r8_resources,resource_manifest,error),"Invalid compiled resource manifest");
  r8_need(request.at("resourceManifestSha256").as_string()=="RESOURCE_SHA","Resource manifest identity mismatch");
  size_t seen=0;for(const auto& entry:std::filesystem::recursive_directory_iterator(std::filesystem::u8path(resources_dir))){if(!entry.is_regular_file())continue;const auto rel=std::filesystem::relative(entry.path(),std::filesystem::u8path(resources_dir)).generic_string();r8_need(resource_manifest.has(rel)&&r8_hashfile(entry.path())==resource_manifest.at(rel).as_string(),"Resource bytes/collection changed");++seen;}r8_need(seen==resource_manifest.fields().size(),"Missing resource");
  const auto store=std::filesystem::u8path(request.at("negativeStore").as_string());const auto allowed=std::filesystem::weakly_canonical(std::filesystem::path("E:/Kitao-Research"));const auto parent=std::filesystem::weakly_canonical(store.parent_path());const auto rel=std::filesystem::relative(parent,allowed);r8_need(!rel.empty()&&*rel.begin()!=".."&&!std::filesystem::exists(store),"Negative store must be new authorized local path");
  // Rows are a policy result, not an ignored hint. Hard allocator caps catch every live Vulkan allocation including staging.
  const uint64_t fixed=8ull*1024*1024;const uint64_t denom=uint64_t(width)*160;
  r8_need(device_cap>fixed&&staging_cap>65536&&host_cap>2*1024*1024,"Insufficient bounded budgets");
  const uint64_t bydevice=(device_cap-fixed)/denom;const uint64_t bystage=staging_cap/(uint64_t(width)*48);const uint64_t byhost=(host_cap-2*1024*1024)/(uint64_t(width)*64);
  const uint64_t possible=std::min(bydevice,std::min(bystage,byhost));r8_need(possible>8,"Budget cannot hold minimum strip and halo");rows=uint32_t(std::min<uint64_t>(height,std::min<uint64_t>(requested_rows,possible-8)));
  gpu::r8_limits={};gpu::r8_limits.enabled=true;gpu::r8_limits.device=size_t(device_cap);gpu::r8_limits.staging=size_t(staging_cap);
  auto mark=std::chrono::steady_clock::now();engine.reset(spk_engine_create(resources_dir,nullptr));r8_need(bool(engine),"Cannot create bounded engine");
  Params params;r8_need(init_params(engine->resources_dir,"kodak_portra_400","kodak_portra_endura",engine->neutral_filters,params,error),error.c_str());params.io.output_color_space="ProPhoto RGB";params.io.output_cctf_encoding=true;apply_delta(params,expected);params.settings.working_precision="float32";r8_need(digest(params,engine->neutral_filters,error),error.c_str());r8_need(read_params(params).dump()==expected.dump(),"Resolved defaults drifted");
  r8_need(params.scanner.unsharp_mask[0]==.7&&params.scanner.unsharp_mask[1]==.7&&params.scanner.lens_blur==0&&params.camera.lens_blur_um==0&&!params.camera.diffusion_filter.active&&!params.enlarger.diffusion_filter.active&&params.enlarger.lens_blur==0&&!params.print_render.contrast_mask.active&&!params.print_render.density_curves_morph.active&&!params.io.digital_intermediate&&!params.debug.deactivate_spatial_effects,"Hidden effect contract drifted");
  pipeline=std::make_unique<Pipeline>(engine->gpu,&engine->colour,&engine->blob,&engine->setup_cache);r8_need(pipeline->build(params,error)&&pipeline->r8_stream_context(width,height,error),error.c_str());halo=pipeline->r8_stream_halo();r8_need(halo<=4,"Unverified halo");init_ms=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-mark).count();
  store_path=store;
  struct StoreGuard{std::filesystem::path path;~StoreGuard(){std::error_code e;std::filesystem::remove(path,e);}} store_guard{store};
  std::fstream negative(store,std::ios::binary|std::ios::out|std::ios::in|std::ios::trunc);r8_need(bool(negative),"Negative store open failed");
  const uint32_t maxrows=std::min<uint32_t>(height,rows+2*halo);std::vector<float> host(size_t(width)*maxrows*3);std::vector<uint16_t> rgba(size_t(width)*maxrows*4);host_peak=host.size()*4+rgba.size()*2+2*1024*1024;r8_need(host_peak<=host_cap,"Host cap exceeded");R8Hash input_hash;
  auto check_cancel=[&](){if(poll(user)){status=-7;throw std::runtime_error("render cancelled");}};
  for(uint32_t y=0;y<height;y+=rows){check_cancel();const uint32_t count=std::min(rows,height-y);auto t=std::chrono::steady_clock::now();if(read_rows(user,y,count,host.data())!=0){status=-21;throw std::runtime_error("read_rows failed");}read_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();for(size_t i=0;i<size_t(width)*count*3;i++)r8_need(std::isfinite(host[i]),"Nonfinite callback input");input_hash.add(host.data(),size_t(width)*count*12);
   {Image in,out;in.w=width;in.h=count;in.c=3;t=std::chrono::steady_clock::now();in.buf=engine->gpu->upload(host.data(),in.bytes(),error);r8_need(bool(in.buf),error.c_str());upload_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();t=std::chrono::steady_clock::now();r8_need(pipeline->r8_stream_film(in,out,error)&&engine->gpu->flush(error),error.c_str());compute_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();t=std::chrono::steady_clock::now();r8_need(engine->gpu->read(out.buf.get(),0,host.data(),out.bytes(),error),error.c_str());readback_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();t=std::chrono::steady_clock::now();negative.seekp(uint64_t(y)*width*12);negative.write((const char*)host.data(),std::streamsize(out.bytes()));r8_need(bool(negative),"Negative row store write failed");store_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();}
   engine->gpu->trim_pool(0);
  }
  r8_need(input_hash.finish()==request.at("inputPixelSha256").as_string(),"Read source hash mismatch");negative.flush();
  for(uint32_t y=0;y<height;y+=rows){check_cancel();const uint32_t count=std::min(rows,height-y),first=y>halo?y-halo:0,end=std::min(height,y+count+halo),got=end-first;auto t=std::chrono::steady_clock::now();negative.seekg(uint64_t(first)*width*12);negative.read((char*)host.data(),std::streamsize(uint64_t(got)*width*12));r8_need(bool(negative),"Negative row store read failed");store_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();
   {Image in,out;in.w=width;in.h=got;in.c=3;t=std::chrono::steady_clock::now();in.buf=engine->gpu->upload(host.data(),in.bytes(),error);r8_need(bool(in.buf),error.c_str());upload_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();t=std::chrono::steady_clock::now();r8_need(pipeline->r8_stream_print(in,out,error),error.c_str());auto packed=engine->gpu->alloc(size_t(width)*got*8,error);r8_need(bool(packed),error.c_str());uint32_t meta[]={uint32_t(out.pixels()),width,width};r8_need(engine->gpu->dispatch("spk_to_rgba16",{gpu::Arg::buf(out.buf),gpu::Arg::inline_bytes(meta,3),gpu::Arg::buf(packed)},out.pixels(),error)&&engine->gpu->flush(error),error.c_str());compute_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();t=std::chrono::steady_clock::now();r8_need(engine->gpu->read(packed.get(),0,rgba.data(),size_t(width)*got*8,error),error.c_str());readback_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();check_cancel();t=std::chrono::steady_clock::now();if(consume(user,y,count,rgba.data()+size_t(y-first)*width*4)!=0){status=-22;throw std::runtime_error("consumer failed");}consumer_ms+=std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-t).count();}
   engine->gpu->trim_pool(0);
  }
  negative.close();status=0;report.set("success",Json(true));report.set("resolved",read_params(params));
 }catch(const std::exception& e){report.set("error",Json(std::string(e.what())));}catch(...){report.set("error",Json(std::string("unknown native exception")));}
 pipeline.reset();engine.reset();
 if(!store_path.empty() && std::filesystem::exists(store_path)) {std::error_code cleanup_error;std::filesystem::remove(store_path,cleanup_error);if(cleanup_error){report.set("negativeStoreCleanupError",Json(cleanup_error.message()));status=status==0?-31:status;report.set("success",Json(false));}}
 report.set("negativeStoreAbsentAfter",Json(store_path.empty()||!std::filesystem::exists(store_path)));
 gpu_after=gpu::r8_limits.device_live;stage_after=gpu::r8_limits.staging_live;
 report.set("status",Json(double(status)));report.set("rows",Json(double(rows)));report.set("halo",Json(double(halo)));report.set("width",Json(double(width)));report.set("height",Json(double(height)));report.set("hostPeakBudgetedBytes",Json(double(host_peak)));report.set("devicePeakBytes",Json(double(gpu::r8_limits.device_peak)));report.set("stagingPeakBytes",Json(double(gpu::r8_limits.staging_peak)));report.set("deviceLiveAfter",Json(double(gpu_after)));report.set("stagingLiveAfter",Json(double(stage_after)));report.set("allocations",Json(double(gpu::r8_limits.allocations)));report.set("initMs",Json(init_ms));report.set("sourceReadMs",Json(read_ms));report.set("uploadMs",Json(upload_ms));report.set("computeMs",Json(compute_ms));report.set("readbackMs",Json(readback_ms));report.set("negativeStoreMs",Json(store_ms));report.set("consumerMs",Json(consumer_ms));report.set("wallMs",Json(std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-start).count()));gpu::r8_limits.enabled=false;
 try{if(out_json)*out_json=dup_json(report);}catch(...){return -99;}return status;
}
'''
cpp=cpp.replace('EXPECTED_JSON',json.dumps(expected,sort_keys=True,separators=(',',':'))).replace('RESOURCE_JSON',json.dumps(hashes,sort_keys=True,separators=(',',':'))).replace('RESOURCE_SHA',hashlib.sha256(json.dumps(hashes,sort_keys=True,separators=(',',':')).encode()).hexdigest())
p=root/'engine/src/pipeline/engine.cpp';p.write_text(p.read_text(encoding='utf-8')+'\n'+cpp,encoding='utf-8')
p=root/'engine/windows_exports.def';p.write_text(p.read_text()+'\n    spk_stream_research_v1\n')
p=root/'engine/CMakeLists.txt';p.write_text(p.read_text(encoding='utf-8')+'\nif(WIN32)\n target_link_libraries(spektralab_engine_shared PRIVATE bcrypt)\nendif()\n',encoding='utf-8')
# External live-wire addendum only; the original service/API is not registered or changed.
api=root/'API-SPEC-callable-render-service.md'
api.write_text(api.read_text(encoding='utf-8')+'\n\n## Isolated research addendum: SpektraStreamingResearchV1\n\nOnly this external research checkout exposes spk_stream_research_v1. It does not call spk_open, does not add a UI/service method, and is not an upstream production capability. Fixed PortraA resolved99fields and actual resource hashes are compiled in; input/backend/resource identities mandatory. Row callbacks synchronous, borrowed buffers valid only during callback; failures return nonzero, no exception across C boundary. Poll cancellation joins before DLL unload. One global call at a time (-30 on concurrent); per-call engine/pipeline destroyed before return. Device/staging limits enforced at vk allocation; host vector budget plus caller process guard separately. Complete frame scale and original halo preserved. Negative disk store removed after every path. Reports native actual peaks/live0; caller must not infer process-RSS cap from those counters.\n',encoding='utf-8')
subprocess.run([__import__('sys').executable,str(root/'modern_UI/Spektrafilm/Tools/gen-project.py')],capture_output=True,check=True)
print(json.dumps({'version':'SpektraStreamingResearchV1','compiledResourceManifestSha256':hashlib.sha256(json.dumps(hashes,sort_keys=True,separators=(',',':')).encode()).hexdigest(),'files':[{'path':p,'sha256':hashlib.sha256((root/p).read_bytes()).hexdigest()} for p in ['engine/src/pipeline/engine.cpp','engine/src/pipeline/pipeline.cpp','engine/src/pipeline/pipeline.hpp','engine/src/gpu/gpu.hpp','engine/src/gpu/vulkan_gpu.cpp','engine/windows_exports.def','engine/CMakeLists.txt','API-SPEC-callable-render-service.md']]}))
