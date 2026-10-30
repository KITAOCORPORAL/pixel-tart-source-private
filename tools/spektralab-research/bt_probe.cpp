
#include "gpu/gpu.hpp"
#include "gpu_vulkan_read.hpp"
#include <cmath>
#include <cstdio>
#include <cstring>
#include <memory>
#include <vector>
uint32_t bits(float f){uint32_t u;std::memcpy(&u,&f,4);return u;}
int main(int argc,char**argv){if(argc!=2)return 2;std::string error;auto gpu=std::unique_ptr<spk::gpu::Gpu>(spk::gpu::Gpu::create_vulkan(std::string(argv[1])+"/vulkan",error));if(!gpu){puts(error.c_str());return 1;}
float bp=1.099f*std::pow(.018f,.45f)-(1.099f-1.f);printf("device=%s cpu_bp=%.17g bits=%08x double_bp=%.17g\n",gpu->device_name().c_str(),double(bp),bits(bp),1.099*std::pow(.018,.45)-(1.099-1.));
std::vector<float> in;for(int i=-6;i<=6;i++){float x=bp;for(int k=0;k<std::abs(i);k++)x=std::nextafter(x,i<0?0.f:1.f);in.push_back(x);}auto a=gpu->upload(in.data(),in.size()*4,error);auto b=gpu->upload(in.data(),in.size()*4,error);
for(uint32_t mode: {3u,6u,7u}){uint32_t meta[]={uint32_t(in.size()),mode};if(!gpu->dispatch("spk_cctf_decode",{spk::gpu::Arg::buf(a),spk::gpu::Arg::inline_bytes(meta,2),spk::gpu::Arg::buf(b)},in.size(),error)||!gpu->flush(error))return 3;std::vector<float> out;if(!read_test_buffer(gpu.get(),b,out,error))return 4;for(size_t i=0;i<in.size();i++){double v=in[i];double ref=v<bp?v/4.5:std::pow((v+double(1.099f-1.f))/double(1.099f),double(1.f/.45f));printf("mode=%u offset=%d input=%.17g input_bits=%08x output=%.17g output_bits=%08x cpu_branch=%s cpu_ref=%.17g delta=%.17g\n",mode,int(i)-6,v,bits(in[i]),double(out[i]),bits(out[i]),v<bp?"linear":"power",ref,std::abs(double(out[i])-ref));}}
}
