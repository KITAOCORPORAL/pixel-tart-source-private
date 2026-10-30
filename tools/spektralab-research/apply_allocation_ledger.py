"""External diagnostic patch generator. Allocation events only; no pixel math changes."""
from pathlib import Path
import argparse,subprocess,hashlib,json
p=argparse.ArgumentParser();p.add_argument('source',type=Path);a=p.parse_args();root=a.source
if subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()!='c1447c6ab618fa4b1cb11fd2fe12826855007059':raise ValueError('Wrong fixed source')
if subprocess.check_output(['git','-C',str(root),'status','--porcelain'],text=True).strip():raise ValueError('Use clean external checkout')
p=root/'engine/src/gpu/vulkan_gpu.cpp';s=p.read_text(encoding='utf-8');s=s.replace('namespace spk::gpu {','''namespace spk::gpu {
// ResearchAllocationLedgerV1: observe allocator lifetime only; diagnostic not runtime policy.
static void research_allocation(const char* op,const void* id,size_t bytes,uint32_t flags,const char* role) {
 if(!std::getenv("SPK_R7_ALLOCATIONS"))return;
 const char* phase=std::getenv("SPK_R7_PHASE");
 std::fprintf(stderr,"{\\"r7_event\\":\\"%s\\",\\"id\\":\\"%p\\",\\"bytes\\":%zu,\\"flags\\":%u,\\"role\\":\\"%s\\",\\"phase\\":\\"%s\\"}\\n",op,id,bytes,flags,role,phase?phase:"");
}
''',1);s=s.replace('        if (transfer_timings_) {\n            std::fprintf(stderr,\n                "{\\"vulkan_allocation', '        research_allocation("allocate",(const void*)(uintptr_t)b.memory,size_t(b.allocation_bytes),uint32_t(b.memory_flags),staging?(readback?"read_staging":"upload_staging"):"compute");\n        if (transfer_timings_) {\n            std::fprintf(stderr,\n                "{\\"vulkan_allocation',1);s=s.replace('    void destroy_buffer(Buffer* b) {','    void destroy_buffer(Buffer* b) {\n        if(b->memory)research_allocation("free",(const void*)(uintptr_t)b->memory,size_t(b->allocation_bytes),uint32_t(b->memory_flags),"gpu");',1);s=s.replace('        if (!pixels) { error = "cannot allocate Vulkan result pixels"; return nullptr; }','        if (!pixels) { error = "cannot allocate Vulkan result pixels"; return nullptr; }\n        research_allocation("allocate",pixels,buffer->bytes,0,"cpu_result");',1);s=s.replace('    void release_texture(void* pixels) override { std::free(pixels); }','    void release_texture(void* pixels) override { research_allocation("free",pixels,0,0,"cpu_result");std::free(pixels); }');s=s.replace('void Gpu::release_texture_static(void* pixels) { std::free(pixels); }','void Gpu::release_texture_static(void* pixels) { research_allocation("free",pixels,0,0,"cpu_result");std::free(pixels); }');p.write_text(s,encoding='utf-8');print(json.dumps({'version':'ResearchAllocationLedgerV1','sourceSha256':hashlib.sha256(p.read_bytes()).hexdigest()}))
