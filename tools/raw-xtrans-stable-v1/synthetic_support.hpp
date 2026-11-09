// Self-authored no-RAW surrogate storage/allocator. Not LibRaw ABI or decoder.
#pragma once
#include <algorithm>
#include <array>
#include <atomic>
#include <cassert>
#include <cfloat>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iostream>
#include <limits>
#include <map>
#include <set>
#include <stdexcept>
#include <string>
#include <vector>
#include <omp.h>
using ushort=unsigned short;using uchar=unsigned char;
static const int LIBRAW_EXCEPTION_ALLOC=1,LIBRAW_EXCEPTION_IO_CORRUPT=2;
class LibRaw_constants {public:static const double xyz_rgb[3][3];static const float d65_white[3];};
struct SyntheticTLS {struct{float cbrt[65536],xyz_cam[3][4];}ahd_data;SyntheticTLS(){memset(this,0,sizeof(*this));ahd_data.cbrt[0]=-2.;}};
class LibRaw {
 std::map<void*,std::pair<size_t,unsigned char*>> blocks;
public:
 int width,height,colors=3;uchar xtrans[6][6]={{1,1,0,1,1,2},{1,1,2,1,1,0},{2,0,1,0,2,1},{1,1,2,1,1,0},{1,1,0,1,1,2},{0,2,1,2,0,1}};
 ushort(*image)[4]=nullptr;float rgb_cam[3][4]={{1,0,0,0},{0,1,0,0},{0,0,1,0}};SyntheticTLS* tls=nullptr;
 std::vector<ushort> storage;std::vector<ushort> stableWitness;std::vector<unsigned char> scratchChecks;
 unsigned char poison;size_t live=0,peak=0,calls=0,failAt=0;bool stableChecked=false,borderAfterCommit=false;int borderCalls=0;int tileTop=-1,tileLeft=-1;
 explicit LibRaw(int w,int h,unsigned char fill=0xA5):width(w),height(h),poison(fill){storage.assign(size_t(w)*h*4+64,0xD00D);image=reinterpret_cast<ushort(*)[4]>(storage.data()+32);memset(image,0,size_t(w)*h*8);tls=new SyntheticTLS;}
 ~LibRaw(){for(auto&b:blocks)::free(b.second.second);delete tls;}
 void* malloc(size_t n){++calls;if(failAt==calls)throw LIBRAW_EXCEPTION_ALLOC;auto b=(unsigned char*)::malloc(n+128);if(!b)throw LIBRAW_EXCEPTION_ALLOC;memset(b,0xE7,64);memset(b+64,poison,n);memset(b+64+n,0xE7,64);void*q=b+64;blocks[q]={n,b};live+=n;peak=std::max(peak,live);return q;}
 void* calloc(size_t n,size_t z){auto q=malloc(n*z);memset(q,0,n*z);return q;}
 void free(void*p){if(!p)return;auto it=blocks.find(p);if(it==blocks.end())throw std::runtime_error("double/untracked free");auto n=it->second.first;auto b=it->second.second;for(size_t i=0;i<64;i++)if(b[i]!=0xE7||b[64+n+i]!=0xE7)throw std::runtime_error("allocation canary overwritten");live-=n;::free(b);blocks.erase(it);}
 void checkImageCanary()const{for(size_t i=0;i<32;i++)if(storage[i]!=0xD00D||storage[storage.size()-1-i]!=0xD00D)throw std::runtime_error("image boundary overwritten");}
 void captureStable(){stableWitness.assign((ushort*)image,(ushort*)image+size_t(width)*height*4);}
 void verifyStable(){if(stableWitness.size()!=size_t(width)*height*4||memcmp(stableWitness.data(),image,stableWitness.size()*2))throw std::runtime_error("shared input changed before join/commit");stableChecked=true;}
 int fcol(int row,int col)const{return xtrans[(row+6)%6][(col+6)%6];}
 void xtrans_interpolate(int);void candidate_interpolate(int);void oracle_tile(int);void border_interpolate(int);void cielab(ushort[3],short[3]);char** malloc_omp_buffers(int,size_t);void free_omp_buffers(char**,int);
};
