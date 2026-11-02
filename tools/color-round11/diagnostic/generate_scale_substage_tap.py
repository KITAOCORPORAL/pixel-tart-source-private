"""Passive FilmScaleSubstageTapV1 on a fresh exact company diagnostic source."""
from pathlib import Path
import argparse,subprocess,hashlib,json
p=argparse.ArgumentParser();p.add_argument('old',type=Path);p.add_argument('fresh',type=Path);a=p.parse_args();r=a.fresh.resolve();old=a.old.resolve();base='c1447c6ab618fa4b1cb11fd2fe12826855007059';repo=Path(__file__).resolve().parents[3]
if subprocess.check_output(['git','-C',str(r),'rev-parse','HEAD'],text=True).strip()!=base or subprocess.check_output(['git','-C',str(r),'status','--porcelain'],text=True).strip():raise ValueError('Fresh clean fixed checkout required')
expected=json.loads((repo/'docs/research/color-engine-20261029/company-diagnostic-20261102/build-before-run.json').read_text())['sourceFiles']
for name,want in expected.items():
 data=(old/name).read_bytes();normal=data
 if name=='engine/src/pipeline/engine.cpp':
  import re
  roots=re.findall(rb'const auto allowed=std::filesystem::weakly_canonical\(std::filesystem::path\("([^"\n]+)"\)\)',data)
  if len(roots)!=2 or len(set(roots))!=1:raise ValueError('Cannot identify only permitted allowed-root literals')
  normal=data.replace(roots[0],b'E:/Kitao-Research')
 if hashlib.sha256(normal).hexdigest()!=want:raise ValueError('Sealed source changed beyond authorized-root literal '+name)
 (r/name).write_bytes(data)
(r/'API-SPEC-callable-render-service.md').write_bytes((old/'API-SPEC-callable-render-service.md').read_bytes())
def edit(name,change):
 f=r/name;s=f.read_text(encoding='utf-8');s=change(s);f.write_text(s,encoding='utf-8')
edit('engine/src/gpu/gpu.hpp',lambda s:s.replace('namespace spk::gpu {','''namespace spk::gpu {
inline thread_local const char* scale_upload_label=nullptr;
inline thread_local bool scale_exposure=false;
struct ScaleUploadLabel {const char* prior;ScaleUploadLabel(const char* s):prior(scale_upload_label){scale_upload_label=s;}~ScaleUploadLabel(){scale_upload_label=prior;}};
struct ScaleExposure {bool prior;ScaleExposure():prior(scale_exposure){scale_exposure=true;}~ScaleExposure(){scale_exposure=prior;}};
''',1))
def vulkan(s):
 s=s.replace('#include <fstream>','#include <fstream>\n#include <filesystem>\n#include <stdexcept>',1)
 needle='        if (!mapped_range(upload_staging_, bytes, true, error)) return {};'
 add='''        if(const char* dir=std::getenv("SPK_SCALE_SUBSTAGE_DIR");dir && scale_upload_label) {
            auto path=std::filesystem::path(dir)/(std::string(scale_upload_label)+".uploaded.f32");
            if(std::filesystem::exists(path))throw std::runtime_error("Duplicate scale upload tap");
            std::ofstream f(path,std::ios::binary);f.write((const char*)upload_staging_.mapped,std::streamsize(bytes));if(!f)throw std::runtime_error("Scale upload tap failed");
        }
'''
 if s.count(needle)!=1:raise ValueError('Upload anchor')
 s=s.replace(needle,add+needle,1)
 needle='            stage.pSpecializationInfo = &special;'
 add=r'''
            if(const char* dir=std::getenv("SPK_SCALE_SUBSTAGE_DIR");dir && (std::strcmp(spec.name,"spk_tc_b")==0 || std::strcmp(spec.name,"spk_lut2d_cubic")==0 || std::strcmp(spec.name,"spk_affine3")==0)) {
                std::ofstream f(std::filesystem::path(dir)/"selected-math-mode.jsonl",std::ios::app);
                f<<"{\"kernel\":\""<<spec.name<<"\",\"fused_fma_runtime\":"<<fused_fma_<<",\"specialization_id\":0,\"specialization_bool\":"<<unsigned(fused)<<"}\n";
                if(!f)throw std::runtime_error("Math-mode tap failed");
            }
'''
 return s.replace(needle,needle+add,1)
edit('engine/src/gpu/vulkan_gpu.cpp',vulkan)
helper=r'''
static void scale_host_tap(const char* name,const double* data,size_t count) {
 const char* dir=std::getenv("SPK_SCALE_SUBSTAGE_DIR");if(!dir)return;
 auto path=std::filesystem::path(dir)/(std::string(name)+".cpu.f64");if(std::filesystem::exists(path))throw std::runtime_error("Duplicate host scale tap");
 std::ofstream out(path,std::ios::binary);out.write((const char*)data,std::streamsize(count*sizeof(double)));if(!out)throw std::runtime_error("Host scale tap failed");
}
static void scale_buffer_tap(gpu::Gpu* gpu,const gpu::BufferRef& buffer,const char* name,size_t count) {
 const char* dir=std::getenv("SPK_SCALE_SUBSTAGE_DIR");if(!dir)return;
 auto path=std::filesystem::path(dir)/(std::string(name)+".device.f32");if(std::filesystem::exists(path))throw std::runtime_error("Duplicate buffer scale tap");
 std::vector<float> values(count);std::string error;if(!gpu->read(buffer.get(),0,values.data(),count*sizeof(float),error))throw std::runtime_error("Device scale read failed: "+error);
 std::ofstream out(path,std::ios::binary);out.write((const char*)values.data(),std::streamsize(count*sizeof(float)));if(!out)throw std::runtime_error("Device scale tap failed");
}
'''
def pipeline(s):
 s=s.replace('#include <chrono>','#include <chrono>\n#include <filesystem>\n#include <fstream>\n#include <stdexcept>\n#include <cstdlib>',1);pos=s.index('// --- the two matrix');s=s[:pos]+helper+s[pos:]
 needle='    if (!film_sensitivity(*colour_, *blob_, film, params_.camera, film_sensitivity_, error)) return false;';s=s.replace(needle,needle+'\n    scale_host_tap("film-sensitivity",film_sensitivity_.data(),film_sensitivity_.size());',1)
 needle='        baked_.tc_lut = gpu_->upload_persistent_f32(lut->data(), lut->size(), error);';s=s.replace(needle,'        scale_host_tap("tc-lut",lut->data(),lut->size());\n        gpu::ScaleUploadLabel observe_tc_lut("tc-lut");\n'+needle,1)
 needle='        baked_.tc_b_matrix = gpu_->upload_persistent_f32(row, 9, error);';s=s.replace(needle,'        scale_host_tap("tc-b-matrix",row,9);\n        gpu::ScaleUploadLabel observe_tc_matrix("tc-b-matrix");\n'+needle,1)
 start=s.index('bool Pipeline::node_upsample(');end=s.index('\n}\n',start)+3;chunk=s[start:end]
 needle='    const uint32_t n[1] = {uint32_t(in.pixels())};';chunk=chunk.replace(needle,'    scale_buffer_tap(gpu_,in.buf,"00-input",in.elements());\n    scale_buffer_tap(gpu_,baked_.tc_b_matrix,"tc-b-matrix",9);\n    scale_buffer_tap(gpu_,baked_.tc_lut,"tc-lut",baked_.tc_lut_side*baked_.tc_lut_side*3);\n'+needle,1)
 needle='    if (!alloc_like(in, out, error)) return false;';chunk=chunk.replace(needle,'    scale_buffer_tap(gpu_,tc.buf,"01-tc",in.pixels()*2);\n    scale_buffer_tap(gpu_,b,"02-brightness",in.pixels());\n'+needle,1)
 chunk=chunk.replace('    return gpu_->dispatch("spk_lut2d_cubic",','    const bool ok=gpu_->dispatch("spk_lut2d_cubic",',1);needle='                          in.pixels(), error);';pos=chunk.rfind(needle);chunk=chunk[:pos]+chunk[pos:].replace(needle,needle+'\n    if(ok)scale_buffer_tap(gpu_,out.buf,"03-upsample",out.elements());\n    return ok;',1);s=s[:start]+chunk+s[end:]
 start=s.index('bool Pipeline::node_exposure(');end=s.index('\n}\n',start)+3;chunk=s[start:end].replace('    return blur_.affine(in, s, t0, out, error);','    scale_host_tap("exposure-scale",s,3);scale_host_tap("exposure-offset",t0,3);\n    gpu::ScaleExposure expose_scope;const bool ok=blur_.affine(in,s,t0,out,error);\n    if(ok)scale_buffer_tap(gpu_,out.buf,"04-exposure",out.elements());return ok;');return s[:start]+chunk+s[end:]
edit('engine/src/pipeline/pipeline.cpp',pipeline)
def blur(s):
 s=s.replace('#include <cmath>','#include <cmath>\n#include <cstdlib>',1);start=s.index('bool Blur::affine(');end=s.index('\n}\n',start)+3;chunk=s[start:end]
 chunk=chunk.replace('    gpu::BufferRef sb = gpu_->upload(sf, sizeof sf, error);','    gpu::BufferRef sb;\n    {gpu::ScaleUploadLabel observe(gpu::scale_exposure?"exposure-scale":nullptr);sb=gpu_->upload(sf,sizeof sf,error);}')
 chunk=chunk.replace('    gpu::BufferRef tb = gpu_->upload(tf, sizeof tf, error);','    gpu::BufferRef tb;\n    {gpu::ScaleUploadLabel observe(gpu::scale_exposure?"exposure-offset":nullptr);tb=gpu_->upload(tf,sizeof tf,error);}')
 return s[:start]+chunk+s[end:]
edit('engine/src/pipeline/blur.cpp',blur)
api=r/'API-SPEC-callable-render-service.md';api.write_text(api.read_text(encoding='utf-8')+'\n\n## FilmScaleSubstageTapV1 passive research observation\nSPK_SCALE_SUBSTAGE_DIR records CPU preparation and staging upload bytes, device constants and suboperation outputs. Original API/arithmetic/shader flags unchanged. Only fixed gray whole call, no product registration.\n',encoding='utf-8')
subprocess.run([__import__('sys').executable,str(r/'modern_UI/Spektrafilm/Tools/gen-project.py')],capture_output=True,check=True)
changed=list(expected)+['engine/src/pipeline/blur.cpp','API-SPEC-callable-render-service.md']
result={'version':'FilmScaleSubstageTapV1SourceGeneration','baseEngineCommit':base,'sealedSource':str(old),'sourceFiles':[{'path':name,'sha256':hashlib.sha256((r/name).read_bytes()).hexdigest()} for name in changed],'shaderChanges':False,'originalDispatchOrderChanged':False,'pixelMathChange':False,'newEntry':False}
Path(str(r)+'.generation.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
