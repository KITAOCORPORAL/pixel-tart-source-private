// Original diagnostic support, not copied vendor implementation. Include BEFORE dcraw macros.
#ifndef RR001_INTERNAL_OBSERVER_H
#define RR001_INTERNAL_OBSERVER_H
#ifndef LIBRAW_LIBRARY_BUILD
#define LIBRAW_LIBRARY_BUILD
#endif
#include <winsock2.h>
#include "libraw/libraw.h"
#include <windows.h>
#include <bcrypt.h>
#include <cstdio>
#include <cstdint>
#include <cstddef>
#include <cstring>
#include <vector>
#include <string>
#include <climits>
#include <array>
#include <atomic>
#ifdef _OPENMP
#include <omp.h>
#endif
#pragma comment(lib,"bcrypt.lib")
static_assert(sizeof(void*)==8,"x64 only");
namespace rr001i {
struct Hash {
 BCRYPT_ALG_HANDLE a=nullptr; BCRYPT_HASH_HANDLE h=nullptr; std::vector<unsigned char> object; bool ok=false;
 Hash(){DWORD n=0,r=0;if(BCryptOpenAlgorithmProvider(&a,BCRYPT_SHA256_ALGORITHM,nullptr,0)<0)return;if(BCryptGetProperty(a,BCRYPT_OBJECT_LENGTH,(PUCHAR)&n,4,&r,0)<0)return;object.resize(n);ok=BCryptCreateHash(a,&h,object.data(),n,nullptr,0,0)>=0;}
 void add(const void*p,size_t n){if(ok&&(n>ULONG_MAX||BCryptHashData(h,(PUCHAR)p,(ULONG)n,0)<0))ok=false;}
 bool finish(char out[65]){unsigned char b[32];if(!ok||BCryptFinishHash(h,b,32,0)<0)return false;for(int i=0;i<32;i++)sprintf_s(out+2*i,3,"%02x",b[i]);return true;}
 ~Hash(){if(h)BCryptDestroyHash(h);if(a)BCryptCloseAlgorithmProvider(a,0);}
};
struct Tile {int top,left,mrow,mcol,team;};
struct Worker {unsigned count=0;int team=0;uintptr_t scratch=0;std::array<Tile,1024> tiles;};
struct State {
 FILE*sink=nullptr;wchar_t root[32768]={0};int errors=0;unsigned event=0,generation=0;uintptr_t last=0,allocated=0;size_t extent=0;int aw=0,ah=0,shrink=0,fuji=0,mix=0,raw=0,zero=0,quality=-1,passes=-1,subtractInline=-1;const char*branch="not-dispatched";
 std::array<unsigned,16> stages={};std::vector<Worker> workers;std::atomic<bool> traceInvalid{false};size_t scratchExtent=0;int tileSize=0,ndir=0;
};
#ifdef RR001I_IMPLEMENTATION
State g;
#else
extern State g;
#endif
inline void invalid(){++g.errors;}
inline void event(const char*name,const char*phase){if(!g.sink)return;fprintf(g.sink,"{\"kind\":\"event\",\"sequence\":%u,\"name\":\"%s\",\"phase\":\"%s\"}\n",++g.event,name,phase);}
inline void io(int s,int f,int m,int r,int z){g.shrink=s;g.fuji=f;g.mix=m;g.raw=r;g.zero=z;}
inline void allocation(libraw_data_t&d,int w,int h){if(!g.sink)return;g.allocated=(uintptr_t)d.image;g.extent=(size_t)w*h*8;g.aw=w;g.ah=h;event("image-allocation","initialized-by-original-raw2image");}
inline void integer(const char*name,long long v){if(g.sink)fprintf(g.sink,"{\"kind\":\"scalar\",\"name\":\"%s\",\"value\":%lld}\n",name,v);}
inline void bits(const char*name,const float*p,size_t n){if(!g.sink)return;fprintf(g.sink,"{\"kind\":\"float32-bits\",\"name\":\"%s\",\"bits\":[",name);for(size_t i=0;i<n;i++){uint32_t v;memcpy(&v,p+i,4);fprintf(g.sink,"%s%u",i?",":"",v);}fputs("]}\n",g.sink);}
inline void doubles(const char*name,const double*p,size_t n){if(!g.sink)return;fprintf(g.sink,"{\"kind\":\"float64-bits\",\"name\":\"%s\",\"bits\":[",name);for(size_t i=0;i<n;i++){uint64_t v;memcpy(&v,p+i,8);fprintf(g.sink,"%s\"%016llx\"",i?",":"",(unsigned long long)v);}fputs("]}\n",g.sink);}
inline void unsigneds(const char*name,const unsigned*p,size_t n){if(!g.sink)return;fprintf(g.sink,"{\"kind\":\"uint32-values\",\"name\":\"%s\",\"values\":[",name);for(size_t i=0;i<n;i++)fprintf(g.sink,"%s%u",i?",":"",p[i]);fputs("]}\n",g.sink);}
inline void bytes(const char*name,const void*p,size_t n){if(!g.sink)return;Hash h;h.add(p,n);char hash[65]={0};if(!h.finish(hash)){invalid();return;}fprintf(g.sink,"{\"kind\":\"defined-byte-span-hash\",\"name\":\"%s\",\"bytes\":%zu,\"sha256\":\"%s\"}\n",name,n,hash);}
inline void state(libraw_data_t&d,const char*where){
 if(!g.sink)return;auto&s=d.sizes;auto&c=d.color;auto&p=d.params;
 if((uintptr_t)d.image!=g.last){g.last=(uintptr_t)d.image;++g.generation;}
 fprintf(g.sink,"{\"kind\":\"state\",\"where\":\"%s\",\"width\":%u,\"height\":%u,\"iwidth\":%u,\"iheight\":%u,\"rawWidth\":%u,\"rawHeight\":%u,\"rawPitch\":%u,\"left\":%u,\"top\":%u,\"flip\":%d,\"imagePointerDiagnosticOnly\":\"%llx\",\"pointerGeneration\":%u,\"declaredAllocationBytes\":%zu,\"allocWidth\":%d,\"allocHeight\":%d,\"filters\":%u,\"colors\":%d,\"shrink\":%d,\"fujiWidth\":%d,\"mixGreen\":%d,\"rawColor\":%d,\"zeroIsBad\":%d,\"quality\":%d,\"passes\":%d,\"branch\":\"%s\",\"subtractInline\":%d,\"black\":%u,\"maximum\":%u,\"dataMaximum\":%u,\"noAutoScale\":%d,\"noInterpolation\":%d,\"asShotWbApplied\":%d,\"rawOptions\":%u,\"userQual\":%d,\"cameraWb\":%d,\"autoWb\":%d,\"fourColor\":%d,\"highlight\":%d,\"medPasses\":%d,\"progress\":%u,\"warnings\":%u}\n",where,s.width,s.height,s.iwidth,s.iheight,s.raw_width,s.raw_height,s.raw_pitch,s.left_margin,s.top_margin,s.flip,(unsigned long long)(uintptr_t)d.image,g.generation,g.extent,g.aw,g.ah,d.idata.filters,d.idata.colors,g.shrink,g.fuji,g.mix,g.raw,g.zero,g.quality,g.passes,g.branch,g.subtractInline,c.black,c.maximum,c.data_maximum,p.no_auto_scale,p.no_interpolation,c.as_shot_wb_applied,d.rawparams.options,p.user_qual,p.use_camera_wb,p.use_auto_wb,p.four_color_rgb,p.highlight,p.med_passes,d.progress_flags,d.process_warnings);
 size_t grid=(size_t)c.cblack[4]*c.cblack[5];if(grid>LIBRAW_CBLACK_SIZE-6){invalid();return;}unsigneds("current-cblack-defined",c.cblack,6+grid);
 bits("pre_mul",c.pre_mul,4);bits("cam_mul",c.cam_mul,4);bits("user_mul",p.user_mul,4);bits("rgb_cam-defined",&c.rgb_cam[0][0],12);doubles("gamma",p.gamm,6);doubles("aber",p.aber,4);
 bytes("xtrans-six-by-six",d.idata.xtrans,36);bytes("xtrans-absolute-six-by-six",d.idata.xtrans_abs,36);bytes("white-defined-8x8",c.white,sizeof(c.white));unsigneds("cropbox",p.cropbox,4);
 if(fflush(g.sink)||ferror(g.sink))invalid();
}
struct Scope {libraw_data_t&d;const char*n;Scope(libraw_data_t&data,const char*name):d(data),n(name){event(n,"enter");state(d,n);}~Scope(){state(d,n);event(n,"return-or-unwind");}};
inline void saved(libraw_data_t&d){if(!g.sink)return;auto&r=d.rawdata;bits("P0-saved-pre_mul",r.color.pre_mul,4);bits("P0-saved-cam_mul",r.color.cam_mul,4);bits("P0-saved-rgb_cam",&r.color.rgb_cam[0][0],12);size_t n=(size_t)r.color.cblack[4]*r.color.cblack[5];if(n>LIBRAW_CBLACK_SIZE-6){invalid();return;}unsigneds("P0-saved-cblack",r.color.cblack,6+n);integer("P0-saved-maximum",r.color.maximum);integer("P0-saved-black",r.color.black);integer("P0-saved-raw-pitch",r.sizes.raw_pitch);integer("P0-saved-raw-width",r.sizes.raw_width);integer("P0-saved-raw-height",r.sizes.raw_height);integer("P0-saved-width",r.sizes.width);integer("P0-saved-height",r.sizes.height);integer("P0-saved-iwidth",r.sizes.iwidth);integer("P0-saved-iheight",r.sizes.iheight);integer("P0-saved-left",r.sizes.left_margin);integer("P0-saved-top",r.sizes.top_margin);integer("P0-saved-filters",r.iparams.filters);}
inline bool layout(libraw_data_t&d,unsigned channels){
 size_t bytes=(size_t)d.sizes.iwidth*d.sizes.iheight*8;
 return d.image&&d.sizes.width==d.sizes.iwidth&&d.sizes.height==d.sizes.iheight&&d.sizes.width>32&&d.sizes.height>32&&d.sizes.width<=20000&&d.sizes.height<=20000&&channels>=1&&channels<=4&&g.shrink==0&&g.fuji==0&&g.allocated==(uintptr_t)d.image&&g.extent>=bytes&&bytes<=1073741824;
}
// Full initialized 4-slot state is required at P5; no sparse-CFA equivalence shortcut.
inline bool digest(libraw_data_t&d,unsigned channels,char out[65],FILE*file=nullptr){
 if(!layout(d,channels))return false;Hash h;std::vector<unsigned short> row((size_t)d.sizes.width*channels);
 for(unsigned y=0;y<d.sizes.height;y++){for(unsigned x=0;x<d.sizes.width;x++)for(unsigned k=0;k<channels;k++){int slot=channels==1?d.idata.xtrans[y%6][x%6]:(int)k;if(slot<0||slot>3)return false;row[(size_t)x*channels+k]=d.image[(size_t)y*d.sizes.iwidth+x][slot];}size_t n=row.size()*2;h.add(row.data(),n);if(file&&fwrite(row.data(),1,n,file)!=n)return false;}return h.finish(out);
}
inline void payload(libraw_data_t&d,const char*id,unsigned channels){
 if(!g.sink)return;wchar_t name[32768];if(swprintf_s(name,L"%ls/%hs.%ls.u16",g.root,id,channels==4?L"image4":channels==3?L"rgb":L"cfa")<0){invalid();return;}FILE*f=nullptr;if(_wfopen_s(&f,name,L"wbx")){invalid();return;}char hash[65]={0};bool valid=digest(d,channels,hash,f);if(fclose(f))valid=false;
 fprintf(g.sink,"{\"kind\":\"payload\",\"id\":\"%s\",\"channels\":%u,\"valid\":%s,\"sha256\":\"%s\",\"width\":%u,\"height\":%u,\"sourcePitch\":%u,\"packedPitch\":%u,\"borderExcluded\":0,\"fullInitializedInput\":%s}\n",id,channels,valid?"true":"false",hash,d.sizes.width,d.sizes.height,d.sizes.iwidth*8,d.sizes.width*channels*2,channels==4?"true":"false");if(!valid)invalid();
}
inline void stage(libraw_data_t&d,const char*id,bool fullFour){
 if(!g.sink)return;int n=!strcmp(id,"P1")?1:!strcmp(id,"P3b")?3:!strcmp(id,"P5")?5:!strcmp(id,"P6")?6:!strcmp(id,"P7")?7:!strcmp(id,"P9")?9:-1;
 if((!strcmp(id,"P5")||!strcmp(id,"P6"))&&strcmp(g.branch,"xtrans")){invalid();return;}
 if(n<0||++g.stages[n]!=1){invalid();return;}state(d,id);payload(d,id,fullFour?4:3);
 if(!fullFour)payload(d,(std::string(id)+"-audit4").c_str(),4);
 if(!strcmp(id,"P1")||!strcmp(id,"P3b")){if(d.idata.filters!=9||d.idata.colors!=3)invalid();else payload(d,(std::string(id)+"-cfa").c_str(),1);}
}
inline int worker(){
#ifdef _OPENMP
 return omp_get_thread_num();
#else
 return 0;
#endif
}
inline void xbegin(int tile,int ndir,int passes,int buffers,size_t extent){if(!g.sink)return;if(buffers<1||buffers>256){invalid();return;}g.workers.resize(buffers);g.scratchExtent=extent;g.tileSize=tile;g.ndir=ndir;integer("actual-xtrans-passes",passes);integer("actual-xtrans-T",tile);integer("actual-xtrans-ndir",ndir);
#ifdef _OPENMP
 integer("omp-compiled",_OPENMP);integer("omp-max-threads",omp_get_max_threads());integer("omp-dynamic",omp_get_dynamic());integer("omp-nested",omp_get_nested());
#else
 integer("omp-compiled",0);
#endif
}
// Only the owning worker writes its bounded record. No logger/file/hash/lock in OMP region.
inline void tile(int top,int left,int mrow,int mcol,void*scratch){if(!g.sink)return;int w=worker();if(w<0||(size_t)w>=g.workers.size()){g.traceInvalid.store(true);return;}auto&r=g.workers[w];
#ifdef _OPENMP
 int team=omp_get_num_threads();
#else
 int team=1;
#endif
 if(r.count>=r.tiles.size()){g.traceInvalid.store(true);return;}r.team=team;r.scratch=(uintptr_t)scratch;r.tiles[r.count++]={top,left,mrow,mcol,team};}
inline void xend(){if(!g.sink)return;if(g.traceInvalid.load())invalid();for(size_t w=0;w<g.workers.size();w++){auto&r=g.workers[w];for(unsigned i=0;i<r.count;i++){auto&t=r.tiles[i];fprintf(g.sink,"{\"kind\":\"tile\",\"worker\":%zu,\"team\":%d,\"top\":%d,\"left\":%d,\"mrow\":%d,\"mcol\":%d,\"scratchPointerDiagnosticOnly\":\"%llx\",\"scratchBytes\":%zu,\"T\":%d,\"ndir\":%d}\n",w,t.team,t.top,t.left,t.mrow,t.mcol,(unsigned long long)r.scratch,g.scratchExtent,g.tileSize,g.ndir);}}event("xtrans-parallel-region","joined");}
} // namespace
#ifdef RR001I_IMPLEMENTATION
extern "C" __declspec(dllexport) const char* rr001i_abi(){static char b[2048];sprintf_s(b,"{\"version\":3,\"pointerBytes\":%zu,\"dataBytes\":%zu,\"image\":%zu,\"sizes\":%zu,\"idata\":%zu,\"params\":%zu,\"rawparams\":%zu,\"color\":%zu,\"rawdata\":%zu,\"parent\":%zu,\"slotBytes\":8,\"processedData\":%zu}",sizeof(void*),sizeof(libraw_data_t),offsetof(libraw_data_t,image),offsetof(libraw_data_t,sizes),offsetof(libraw_data_t,idata),offsetof(libraw_data_t,params),offsetof(libraw_data_t,rawparams),offsetof(libraw_data_t,color),offsetof(libraw_data_t,rawdata),offsetof(libraw_data_t,parent_class),offsetof(libraw_processed_image_t,data));return b;}
extern "C" __declspec(dllexport) int rr001i_begin(const wchar_t*root){using namespace rr001i;if(g.sink||wcslen(root)>32000)return -1;g.errors=0;g.event=0;g.stages.fill(0);g.workers.clear();g.traceInvalid.store(false);wcscpy_s(g.root,root);wchar_t name[32768];swprintf_s(name,L"%ls/native-events.jsonl",root);return _wfopen_s(&g.sink,name,L"wx");}
extern "C" __declspec(dllexport) int rr001i_end(){using namespace rr001i;if(!g.sink)return -1;for(int n:{1,3,5,6,7,9})if(g.stages[n]!=1)invalid();int e=g.errors;if(fclose(g.sink))++e;g.sink=nullptr;return e;}
extern "C" __declspec(dllexport) int rr001i_self_test(){using namespace rr001i;libraw_data_t*d=new libraw_data_t();std::vector<unsigned short> a(40*40*4,123);d->image=(unsigned short(*)[4])a.data();d->sizes.width=d->sizes.iwidth=40;d->sizes.height=d->sizes.iheight=40;g.allocated=(uintptr_t)d->image;g.extent=a.size()*2;char all[65],changed[65],rgb[65],again[65],known[65];bool ok=digest(*d,4,all)&&digest(*d,3,rgb);a[3]++;ok=ok&&digest(*d,4,changed)&&strcmp(all,changed)&&digest(*d,3,again)&&!strcmp(rgb,again);a[0]++;ok=ok&&digest(*d,3,again)&&strcmp(rgb,again);d->sizes.iwidth=39;ok=ok&&!digest(*d,4,again);Hash h;h.add("abc",3);ok=ok&&h.finish(known)&&!strcmp(known,"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");delete d;g.allocated=0;g.extent=0;return ok?0:1;}
extern "C" __declspec(dllexport) int rr001i_lifecycle_test(const wchar_t*root){if(rr001i_begin(root)!=0)return 1;if(rr001i_begin(root)!=-1)return 2;rr001i::event("SYNTHETIC_NO_RAW_LIFECYCLE_ONLY","begin-end-missing-checkpoint-detection");if(rr001i_end()<=0||rr001i::g.sink!=nullptr)return 3;if(rr001i_begin(root)==0){rr001i_end();return 4;}return 0;}
#endif
#endif
