"""Compile exact upstream CPU entry subsets into local research-only executables. No upstream code enters product/Git."""
import pathlib,subprocess,hashlib,json,re
ROOT=pathlib.Path(__file__).resolve().parents[3];A=ROOT/'artifacts/v3-product';old=pathlib.Path('N:/pixart/kitao-instant-film-evidence-v3-20261016/.validation/instant-v3/pinned-reference-read');out=A/'native-references';out.mkdir(exist_ok=True);zig=A/'compiler013/ziglang/zig.exe';rows=[]
def extract(s,signature):
 start=s.index(signature);brace=s.index('{',start);depth=1;i=brace+1
 while depth:
  if s[i]=='{':depth+=1
  if s[i]=='}':depth-=1
  i+=1
 return s[start:i]
def build(name,code,inputs,scope):
 p=out/(name+'.cpp');p.write_text(code,encoding='utf-8');exe=out/(name+'.exe');cmd=[str(zig),'c++','-O2','-std=c++17',str(p),'-o',str(exe)];r=subprocess.run(cmd,capture_output=True,text=True,timeout=180);(out/(name+'.build.log')).write_text(r.stdout+r.stderr);rows.append(dict(name=name,scope=scope,command=cmd,exitCode=r.returncode,inputs=[dict(path=str(x),sha256=hashlib.sha256(x.read_bytes()).hexdigest()) for x in inputs],generatedHarnessSha256=hashlib.sha256(p.read_bytes()).hexdigest(),exeSha256=hashlib.sha256(exe.read_bytes()).hexdigest() if exe.exists() else None));print(name,r.returncode,flush=True)
 if r.returncode:raise RuntimeError(r.stderr[:2500])
header='#include <cmath>\n#include <cstdio>\n#include <cstdlib>\n#include <cstdint>\n#include <algorithm>\n#include <vector>\n#include <cassert>\nusing namespace std;\n#define MIN(a,b) std::min(a,b)\n#define CLAMPF(a,b,c) std::clamp(a,b,c)\n#define DT_OMP_FOR()\nusing dt_aligned_pixel_t=float[4];\n'
# Actual darktable exposure process, thin pipe/data shim and no-op setup for manual exposure-only mode.
p=old/'darktable-src_iop_exposure.c';s=p.read_text();fn=extract(s,'void process(struct dt_iop_module_t *self,');shim='''struct dt_iop_module_t{};struct dt_iop_exposure_data_t{float black,scale;};struct desc{float processed_maximum[3];};struct pipe{desc dsc;};struct dt_dev_pixelpipe_iop_t{void* data;int colors;struct pipe* pipe;};struct dt_iop_roi_t{int width,height;};void _process_common_setup(dt_iop_module_t*,dt_dev_pixelpipe_iop_t*){}\n#define DT_OMP_FOR_SIMD(x)\n#define restrict __restrict\n'''
main='''int main(){float x;while(scanf("%f",&x)==1){float output;dt_iop_module_t m;dt_iop_exposure_data_t d={0,2};struct pipe p={};dt_dev_pixelpipe_iop_t piece={&d,1,&p};dt_iop_roi_t roi={1,1};process(&m,&piece,&x,&output,&roi,&roi);printf("%.10g\\n",output);}}'''
build('darktable-exposure',header+shim+fn+main,[p],'Exact process manual black0/scale2; no auto-exposure pipeline')
# Actual RawTherapee tone basecurve helpers, external math names mapped to scalar libm; no full RT/filmic claim.
p=old/'RawTherapee-rtengine_curves.h';s=p.read_text();functions=['static inline double basel(','static inline double basel_alt(','static inline double baseu(','static inline double baseu_alt(','static inline double cupper(','static inline double clower(','static inline double cupper2(','static inline double clower2(','static inline double basecurve('];code='\n'.join(extract(s,f) for f in functions)
build('rawtherapee-basecurve',header+'#define xlog log\n#define xexp exp\n#define SQR(x) ((x)*(x))\n'+code+'\nint main(){double x;while(scanf("%lf",&x)==1)printf("%.17g\\n",basecurve(x,2,0,1,.5,.5));}',[p],'Actual CurveFactory basecurve helper; exposure a2 black0 D1 hr/sr.5; not whole engine')
# Actual darktable recursive gaussian CPU function with known structs; independent finite-support candidate is not identical.
p=old/'darktable-src_common_gaussian.c';s=p.read_text();code=extract(s,'static void compute_gauss_params(')+'\n'+extract(s,'void dt_gaussian_blur(')
shim='enum dt_gaussian_order_t{DT_IOP_GAUSSIAN_ZERO,DT_IOP_GAUSSIAN_ONE,DT_IOP_GAUSSIAN_TWO};struct dt_gaussian_t{int width,height,channels;float sigma;dt_gaussian_order_t order;float *buf,*max,*min;};\n'
main='''int main(){int w,h;if(scanf("%d%d",&w,&h)!=2)return 2;vector<float>a(w*h),b(w*h),tmp(w*h);for(float&v:a)if(scanf("%f",&v)!=1)return 3;float hi[4]={2,2,2,2},lo[4]={-1,-1,-1,-1};dt_gaussian_t g={w,h,1,1.f,DT_IOP_GAUSSIAN_ZERO,tmp.data(),hi,lo};dt_gaussian_blur(&g,a.data(),b.data());for(float v:b)printf("%.10g\\n",v);}'''
build('darktable-gaussian',header+shim+code+main,[p],'Exact recursive gaussian sigma1 with clamp[-1,2], scalar one channel')
# Actual RawTherapee finite 3x3 function, matching operator family but independent upstream implementation.
p=A/'references/rt/rtengine/gauss.cc';s=p.read_text();code=extract(s,'template<class T> void gauss3x3 (')
main='''int main(){int w,h;if(scanf("%d%d",&w,&h)!=2)return 2;vector<float>a(w*h),b(w*h);for(float&v:a)if(scanf("%f",&v)!=1)return 3;vector<float*>x(h),y(h);for(int i=0;i<h;i++){x[i]=a.data()+i*w;y[i]=b.data()+i*w;}gauss3x3(x.data(),y.data(),w,h,.25f,.125f,.0625f,.5f,.25f);for(float v:b)printf("%.10g\\n",v);}'''
build('rawtherapee-gauss3',header+'#define RESTRICT __restrict\n'+code+main,[p],'Actual gauss3x3 finite kernel .25/.125/.0625 and boundary .5/.25; not recursive mode')
# darktable grain simplex actual source (no copied code stored in repository).
p=old/'darktable-src_iop_grain.c';s=p.read_text();start=s.index('static const double grad3');end=s.index('static float paper_resp');code=s[start:end]
build('darktable-grain-noise',header+code+'\nint main(){_simplex_noise_init();double x,y;while(scanf("%lf%lf",&x,&y)==2)printf("%.17g\\n",_simplex_2d_noise(x,y,1.0));}',[p],'Exact grain multi-octave simplex substage; no Lab/full grain module claim')
p=A/'references/stb/stb_perlin.h';code=header+'#define STB_PERLIN_IMPLEMENTATION\n#include "'+p.as_posix()+'"\nint main(){float x,y;while(scanf("%f%f",&x,&y)==2)printf("%.10g\\n",stb_perlin_fbm_noise3(x,y,0,2,.5,3));}'
build('stb-independent-noise',code,[p],'Sean Barrett revised Perlin fBm3 independent of darktable port; statistical behavior not same realizations')
(out/'BUILD_IDENTITIES.json').write_text(json.dumps(rows,indent=2)+'\n');print('ALL_BUILT',flush=True)
