#include "libraw/libraw.h"
#include <windows.h>
#include <psapi.h>
#include <cstdio>
#include <cstdlib>
#include <chrono>
struct Owner{libraw_data_t*p=libraw_init(0);~Owner(){if(p)libraw_close(p);}};
struct Control{std::chrono::steady_clock::time_point start=std::chrono::steady_clock::now();int milliseconds;};
int progress(void*d,LibRaw_progress,int,int){auto*c=(Control*)d;return c->milliseconds>0&&std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::steady_clock::now()-c->start).count()>=c->milliseconds;}
void sample(const char* stage){PROCESS_MEMORY_COUNTERS_EX m={};m.cb=sizeof(m);GetProcessMemoryInfo(GetCurrentProcess(),(PROCESS_MEMORY_COUNTERS*)&m,sizeof(m));printf("{\"stage\":\"%s\",\"workingSet\":%llu,\"privateBytes\":%llu}\n",stage,(unsigned long long)m.WorkingSetSize,(unsigned long long)m.PrivateUsage);fflush(stdout);}
int main(int argc,char**argv){if(argc!=4)return 2;sample("baseline");int result=0;{Owner r;if(!r.p)return 3;Control c;c.milliseconds=atoi(argv[3]);libraw_set_progress_handler(r.p,progress,&c);int e=libraw_open_file(r.p,argv[1]);printf("{\"stage\":\"open\",\"code\":%d,\"version\":\"%s\"}\n",e,libraw_version());if(e)return 4;sample("opened");e=libraw_unpack(r.p);printf("{\"stage\":\"unpack\",\"code\":%d,\"native\":\"%s\"}\n",e,libraw_strerror(e));if(!e){auto&p=r.p->params;p.use_camera_wb=1;p.use_auto_wb=0;p.output_color=1;p.output_bps=16;p.output_tiff=1;p.user_flip=0;e=libraw_dcraw_process(r.p);printf("{\"stage\":\"process\",\"code\":%d,\"native\":\"%s\",\"width\":%d,\"height\":%d,\"flip\":%d,\"color4\":%s}\n",e,libraw_strerror(e),r.p->sizes.width,r.p->sizes.height,r.p->sizes.flip,r.p->rawdata.color4_image?"true":"false");sample("processed");if(!e&&c.milliseconds==0){e=libraw_dcraw_ppm_tiff_writer(r.p,argv[2]);printf("{\"stage\":\"export16\",\"code\":%d}\n",e);}}result=e;sample("before-close");}sample("after-native-close");for(int i=1;i<=10;i++){Sleep(1000);sample("natural-release");}printf("{\"result\":%d,\"cancelled\":%s,\"closed\":true}\n",result,result==LIBRAW_CANCELLED_BY_CALLBACK?"true":"false");return result==LIBRAW_CANCELLED_BY_CALLBACK?0:result?5:0;}
