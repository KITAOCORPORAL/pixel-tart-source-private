#include "skcms.h"
#include <cstdio>
#include <vector>
std::vector<unsigned char> read(const char*p){FILE*f=fopen(p,"rb");if(!f)return {};fseek(f,0,SEEK_END);long n=ftell(f);rewind(f);std::vector<unsigned char>b(n);fread(b.data(),1,n,f);fclose(f);return b;}
int main(int c,char**v){if(c!=3)return 2;auto a=read(v[1]),b=read(v[2]);skcms_ICCProfile s,t;if(!skcms_Parse(a.data(),a.size(),&s)||!skcms_Parse(b.data(),b.size(),&t))return 3;float x[3],y[3];while(scanf("%f %f %f",x,x+1,x+2)==3){if(!skcms_Transform(x,skcms_PixelFormat_RGB_fff,skcms_AlphaFormat_Opaque,&s,y,skcms_PixelFormat_RGB_fff,skcms_AlphaFormat_Opaque,skcms_sRGB_profile(),1))return 4;printf("%.10g %.10g %.10g\n",y[0],y[1],y[2]);}return 0;}
