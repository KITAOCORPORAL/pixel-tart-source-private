"""Research-only build. Original GPLfunctions staylocal; no referencecode entersproduct."""
import pathlib,subprocess,json,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/icc-reliability';refs=A/'references'
def extract(t,s):
 start=t.index(s);b=t.index('{',start);i=b+1;depth=1
 while depth:
  if t[i]=='{':depth+=1
  elif t[i]=='}':depth-=1
  i+=1
 return t[start:i]
rt=(refs/'rt.cc').read_text();dt=(refs/'dt.cc').read_text();header='#include <cstdio>\n#include <cstdlib>\n#include <vector>\n#include <memory>\n#include <cmath>\n#include <algorithm>\n#define DT_OMP_FOR()\n#define MIN(a,b) std::min((a),(b))\nstatic float* dt_get_perthread(float*p,size_t){return p;}\nnamespace rtengine{template<typename T>T min(T a,T b,T c){return std::min(a,std::min(b,c));}\n'
wrapper=r'''int main(int argc,char**argv){if(argc!=5)return 2;int mode=atoi(argv[1]),w=atoi(argv[2]),h=atoi(argv[3]),r=atoi(argv[4]);std::vector<float>x(w*h),y(w*h);for(auto&v:x)if(scanf("%f",&v)!=1)return 3;if(mode==0){std::vector<float*>a(h),b(h);for(int i=0;i<h;i++){a[i]=x.data()+i*w;b[i]=y.data()+i*w;}rtengine::boxblur(a.data(),b.data(),r,w,h,false);}else{std::vector<float>scratch(std::max(w,h)),trans(w*h);_blur_horizontal_1ch(x.data(),h,w,r,scratch.data(),scratch.size());for(int j=0;j<h;j++)for(int i=0;i<w;i++)trans[i*h+j]=x[j*w+i];_blur_horizontal_1ch(trans.data(),w,h,r,scratch.data(),scratch.size());for(int j=0;j<h;j++)for(int i=0;i<w;i++)y[j*w+i]=trans[i*h+j];}for(auto v:y)printf("%.10g\n",v);return 0;}'''
code=header+extract(rt,'void boxblur(float** src')+'\n}\n'+extract(dt,'static void _blur_horizontal_1ch(')+'\n'+wrapper
p=refs/'filter_refs.cpp';p.write_text(code);zig=r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/compiler/ziglang/zig.exe';subprocess.run([zig,'c++','-O2','-std=c++17','-Drestrict=__restrict','-U__SSE2__',str(p),'-o',str(A/'filter_refs.exe')],check=True);print(json.dumps({'generatedSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'originalFiles':[{'file':n,'sha256':hashlib.sha256((refs/n).read_bytes()).hexdigest()} for n in ['rt.cc','dt.cc']],'GPLcodeInProduct':False}))
