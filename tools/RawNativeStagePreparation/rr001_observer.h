// Original diagnostic support only; no upstream implementation is distributed here.
#ifndef RR001_OBSERVER_H
#define RR001_OBSERVER_H
#include <windows.h>
#include <bcrypt.h>
#include <cstdio>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <vector>
#pragma comment(lib, "bcrypt.lib")
static_assert(sizeof(void*)==8,"RR001 requires x64");
namespace rr001 {
static FILE *sink = nullptr;
static int errors = 0;
struct Sha {
  BCRYPT_ALG_HANDLE alg = nullptr; BCRYPT_HASH_HANDLE hash = nullptr;
  std::vector<unsigned char> object; bool ok = false;
  Sha() {
    DWORD size=0, got=0;
    if (BCryptOpenAlgorithmProvider(&alg,BCRYPT_SHA256_ALGORITHM,nullptr,0)<0) return;
    if (BCryptGetProperty(alg,BCRYPT_OBJECT_LENGTH,(PUCHAR)&size,sizeof(size),&got,0)<0) return;
    object.resize(size);
    ok=BCryptCreateHash(alg,&hash,object.data(),size,nullptr,0,0)>=0;
  }
  void add(const void *p, ULONG n) { if(ok) ok=BCryptHashData(hash,(PUCHAR)p,n,0)>=0; }
  bool finish(char out[65]) {
    unsigned char digest[32];
    if(!ok || BCryptFinishHash(hash,digest,32,0)<0) return false;
    const char *hex="0123456789abcdef";
    for(int i=0;i<32;i++){out[i*2]=hex[digest[i]>>4];out[i*2+1]=hex[digest[i]&15];}out[64]=0;return true;
  }
  ~Sha(){if(hash)BCryptDestroyHash(hash);if(alg)BCryptCloseAlgorithmProvider(alg,0);}
};
// No padding, unused slots or border reads. CFA9-only contract: do not generalize.
template<class Color> static bool digest(libraw_data_t &d, bool dense, Color color, char out[65]) {
  const unsigned w=d.sizes.iwidth,h=d.sizes.iheight;
  if(!d.image||w!=d.sizes.width||h!=d.sizes.height||w<=32||h<=32||w>20000||h>20000||d.idata.colors!=3||d.idata.filters!=9) return false;
  std::vector<unsigned char> row((w-32)*(dense?3:1)*2); Sha sha;
  for(unsigned y=16;y<h-16;y++) {
    size_t n=0;
    for(unsigned x=16;x<w-16;x++) {
      int cfa=color(y,x); if(cfa<0||cfa>2)return false;
      for(int k=0;k<(dense?3:1);k++) {
        unsigned short v=d.image[size_t(y)*w+x][dense?k:cfa];
        row[n++]=(unsigned char)v;row[n++]=(unsigned char)(v>>8);
      }
    }
    sha.add(row.data(),(ULONG)n);
  }
  return sha.finish(out);
}
template<class Color> static void stage(libraw_data_t &d,const char *name,bool dense,int quality,int passes,const char *branch,Color color,int shrink,int fuji_width,int inline_black) {
  if(!sink)return;
  char hash[65]={0}; bool valid=shrink==0 && fuji_width==0 && (!strcmp(branch,"not-dispatched") || !strcmp(branch,"xtrans")) && digest(d,dense,color,hash);
  if(!valid)errors++;
  fprintf(sink,"{\"stage\":\"%s\",\"branch\":\"%s\",\"quality\":%d,\"passes\":%d,\"valid\":%s,\"sha256\":\"%s\",\"width\":%u,\"height\":%u,\"iwidth\":%u,\"iheight\":%u,\"filters\":%u,\"colors\":%d,\"sourcePixelSlots\":4,\"inset\":16,\"channelsPerPixel\":%d,\"shrink\":%d,\"fujiWidth\":%d,\"subtractInline\":%d,\"black\":%u,\"maximum\":%u,\"dataMaximum\":%u,\"cblack\":[%u,%u,%u,%u,%u,%u],\"noAutoScale\":%d,\"noInterpolation\":%d,\"progress\":%u}\n",name,branch,quality,passes,valid?"true":"false",hash,d.sizes.width,d.sizes.height,d.sizes.iwidth,d.sizes.iheight,d.idata.filters,d.idata.colors,dense?3:1,shrink,fuji_width,inline_black,d.color.black,d.color.maximum,d.color.data_maximum,d.color.cblack[0],d.color.cblack[1],d.color.cblack[2],d.color.cblack[3],d.color.cblack[4],d.color.cblack[5],d.params.no_auto_scale,d.params.no_interpolation,d.progress_flags);
  if(fflush(sink)||ferror(sink))errors++;
}
}
extern "C" __declspec(dllexport) const char *rr001_abi() {
 static char b[512];
 snprintf(b,sizeof(b),"{\"pointerBytes\":%zu,\"dataBytes\":%zu,\"image\":%zu,\"sizes\":%zu,\"idata\":%zu,\"color\":%zu,\"params\":%zu,\"rawparams\":%zu,\"rawdata\":%zu,\"parent\":%zu}",sizeof(void*),sizeof(libraw_data_t),offsetof(libraw_data_t,image),offsetof(libraw_data_t,sizes),offsetof(libraw_data_t,idata),offsetof(libraw_data_t,color),offsetof(libraw_data_t,params),offsetof(libraw_data_t,rawparams),offsetof(libraw_data_t,rawdata),offsetof(libraw_data_t,parent_class)); return b;
}
extern "C" __declspec(dllexport) int rr001_begin(const wchar_t *p) {
 if(rr001::sink)return -1; rr001::errors=0;
 return _wfopen_s(&rr001::sink,p,L"wx");
}
extern "C" __declspec(dllexport) int rr001_end() {
 if(!rr001::sink)return -1; if(fclose(rr001::sink))rr001::errors++;rr001::sink=nullptr;return rr001::errors;
}
extern "C" __declspec(dllexport) int rr001_self_test() {
 // Heap allocation is confined to an explicit no-RAW test, never image initialization.
 libraw_data_t *d=new libraw_data_t(); const unsigned w=40,h=40;
 d->sizes.width=d->sizes.iwidth=w;d->sizes.height=d->sizes.iheight=h;
 d->idata.filters=9;d->idata.colors=3;
 std::vector<unsigned short> image(w*h*4,123);d->image=(unsigned short(*)[4])image.data();
 auto color=[](unsigned y,unsigned x){return int((y+x)%3);};
 char a[65],b[65],c[65],s1[65],s2[65],s3[65],known[65];
 bool ok=rr001::digest(*d,true,color,a)&&rr001::digest(*d,false,color,s1);
 for(unsigned y=0;y<h;y++)for(unsigned x=0;x<w;x++){
   d->image[y*w+x][3]=65535;
   if(x<16||x>=w-16||y<16||y>=h-16)for(int k=0;k<3;k++)d->image[y*w+x][k]=999;
   else for(int k=0;k<3;k++)if(k!=color(y,x))d->image[y*w+x][k]=456;
 }
 ok=ok&&rr001::digest(*d,false,color,s2)&&!strcmp(s1,s2);
 // Dense mutation must change hash; sparse valid mutation must change hash.
 ok=ok&&rr001::digest(*d,true,color,b)&&strcmp(a,b);
 d->image[17*w+17][color(17,17)]++;
 ok=ok&&rr001::digest(*d,false,color,s3)&&strcmp(s2,s3);
 ok=ok&&rr001::digest(*d,true,color,c)&&strcmp(b,c);
 // Restore unused interior RGB before testing dense unused-slot/border exclusion.
 for(unsigned y=16;y<h-16;y++)for(unsigned x=16;x<w-16;x++)for(int k=0;k<3;k++)d->image[y*w+x][k]=123;
 ok=ok&&rr001::digest(*d,true,color,b)&&!strcmp(a,b);
 d->sizes.iwidth=39;ok=ok&&!rr001::digest(*d,true,color,b);
 rr001::Sha sha;sha.add("abc",3);ok=ok&&sha.finish(known)&&!strcmp(known,"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
 delete d;return ok?0:1;
}
#endif
