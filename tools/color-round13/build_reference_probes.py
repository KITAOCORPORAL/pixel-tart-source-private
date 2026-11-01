"""Compile exact fixed upstream function bodies in a local-only GPL harness.
No upstream implementation text is stored in this repository.
Adapters cover allocation/types only. Unsupported branches throw, never approximate.
"""
import json,hashlib,subprocess,os,sys,re,tarfile
from pathlib import Path
root=Path(sys.argv[1]);repo=Path(__file__).resolve().parents[2];refs=root/'references';out=root/'reference-build-v2'
if out.exists():raise ValueError('New reference build required')
out.mkdir();compiler=Path('N:/pixart/ColorResearchHome-20261030/deps/toolchain/llvm-mingw-20250402-ucrt-x86_64/bin/clang++.exe');env=os.environ.copy();env['PATH']=str(compiler.parent)+os.pathsep+env['PATH']
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def block(text,anchor):
 start=text.index(anchor);brace=text.index('{',start);depth=1;i=brace+1
 # These selected bodies have no brace-containing string literals; preserve exact source bytes after newline decode.
 while depth:
  if text[i]=='{':depth+=1
  elif text[i]=='}':depth-=1
  i+=1
 return text[start:i]
def write(name,text):(out/name).write_text(text,encoding='utf-8',newline='\n')
for name in ['gauss.h','opthelper.h','rt_math.h','boxblur.h']:(out/name).write_bytes((refs/('rt-'+name)).read_bytes())
# Exact full gauss.cc, scalar CPU build chosen and recorded. No replacement Gaussian.
(out/'gauss.cc').write_bytes((refs/'rt-gauss.cc').read_bytes())
pp=(refs/'rt-procparams.h').read_text();rt=(refs/'rt-ipsharpen.cc').read_text();dt=(refs/'dt-sharpen.c').read_text();algo=(refs/'rt-rt_algo.cc').read_text()
common='''#include <algorithm>\n#include <vector>\n#include <string>\n#include <cmath>\n#include <cstring>\n#include <cstdlib>\n#include <cstdio>\n#include <fstream>\n#include <stdexcept>\n#include <type_traits>\n#include <cstdint>\n#include "gauss.h"\n#include "rt_math.h"\n#define BENCHFUN\n'''
threshold='template<typename T>\n'+block(pp,'class Threshold final')+';'
params='''struct SharpeningParams {bool enabled=true;double contrast=0,blurradius=.2,radius=.7;int amount=70;Threshold<int> threshold{20,80,2000,1200,false};bool edgesonly=false;double edges_radius=1.9;int edges_tolerance=1800;bool halocontrol=false;int halocontrol_amount=85;std::string method="usm";};'''
jagged='''template<class T>struct JaggedArray {std::vector<T> data;std::vector<T*> rows;JaggedArray(size_t w,size_t h):data(w*h),rows(h){for(size_t y=0;y<h;++y)rows[y]=data.data()+y*w;}operator T**(){return rows.data();}};'''
lab='''struct LabImage {int W,H;float** L;};\nstruct ImProcFunctions {double scale=1.;bool multiThread=false;void sharpening(LabImage*,const procparams::SharpeningParams&,bool);void deconvsharpening(float**,float**,const float* const*,int,int,const procparams::SharpeningParams&,double){throw std::runtime_error("rld unsupported by isolated harness");}};'''
blend_helpers='\n'.join(block(algo,a) for a in ['float calcBlendFactor(float val','float tileAverage(','float tileVariance(','float calcContrastThreshold('])
blend_body=block(algo,'void buildBlendMask(')
rtbody='\n'.join([block(rt,'void sharpenHaloCtrl ('),block(rt,'void ImProcFunctions::sharpening (')])
rtpre=common+'''template<class T,class U>void bilateral(T**,T**,U**,int,int,double,int,bool){throw std::runtime_error("bilateral unsupported by isolated harness");}\nnamespace rtengine {\n\nnamespace procparams {\n'''+threshold+'\n'+params+'\n}\n'+jagged+'\n'+lab+'\n'+blend_helpers+'\nvoid buildBlendMask(const float*const*,float**,int,int,float&,bool=false,float** = nullptr);\n'+blend_body+'\n'+rtbody+'\n}\n'
# The scalar math function xlogf is used by disabled autoContrast helper calculations only through normal math? alias needed for selected actual helpers.
rtpre=rtpre.replace('#define BENCHFUN','#define BENCHFUN\nstatic inline float xlogf(float x){return std::log(x);}')
# Stub for a disabled external dependency. The exercised Gaussian has useBoxBlur=false.
rtpre+='namespace rtengine {void boxblur(float**,float**,int,int,int,bool){throw std::runtime_error("boxblur unsupported");}}\n'
# Darktable's exact kernel and process, with only framework allocation/type adapters.
dtpre=common+'''#define restrict\n#define MAXR 12\n#define MIN(a,b) (((a)<(b))?(a):(b))\n#define MAX(a,b) std::max((a),(b))\n#define DT_OMP_FOR()\n#define for_four_channels(c,...) for(int c=0;c<4;++c)\n#define DT_IMGSZ_WIDTH 2\n#define DT_IMGSZ_PERTHREAD 4\n#define DT_DEBUG_ALWAYS 0\nusing dt_aligned_pixel_t=float[4];\nstruct dt_iop_module_t{};struct dt_iop_roi_t{int width,height;float scale;};struct dt_dev_pixelpipe_iop_t{void* data;int colors;float iscale;};\nstruct dt_iop_sharpen_data_t{float radius,amount,threshold;};\nstatic bool dt_iop_have_required_input_format(int,dt_iop_module_t*,int,const void*,void*,const dt_iop_roi_t*,const dt_iop_roi_t*){return true;}\nstatic void dt_iop_image_copy_by_size(void*o,const void*i,int w,int h,int c){memcpy(o,i,size_t(w)*h*c*4);}\nstatic void dt_iop_copy_image_roi(void*o,const void*i,int c,const dt_iop_roi_t*ri,const dt_iop_roi_t*ro){dt_iop_image_copy_by_size(o,i,ro->width,ro->height,c);}\nstatic bool dt_iop_alloc_image_buffers(dt_iop_module_t*,const dt_iop_roi_t*,const dt_iop_roi_t*ro,int,float**p,size_t*n,int){*n=ro->width;*p=(float*)calloc(*n,sizeof(float));return *p;}\nstatic float*dt_calloc_align_float(size_t n){return (float*)calloc(n,sizeof(float));}\nstatic float*dt_get_perthread(float*p,size_t){return p;}\nstatic void dt_free_align(void*p){free(p);}\nstatic void dt_print(int,const char*){}\nstatic void copy_pixel(float*o,const float*i){memcpy(o,i,16);}\n'''+block(dt,'static float *const init_gaussian_kernel(')+'\n'+block(dt,'void process(')+'\n'
# ABI deliberately scalar Lab: input L* [0,100], a*,b* pass through; RT uses internal327.68 scaling.
main='''int main(int argc,char**argv){try{if(argc!=9)throw std::runtime_error("mode input output W H sigma amount parameter");std::string mode=argv[1];int W=atoi(argv[4]),H=atoi(argv[5]);float sigma=atof(argv[6]),amount=atof(argv[7]),parameter=atof(argv[8]);std::vector<float> src(size_t(W)*H*3);std::ifstream in(argv[2],std::ios::binary);in.read((char*)src.data(),src.size()*4);if(!in)throw std::runtime_error("input length");std::vector<float> result=src;\n'''
rtmain='''rtengine::JaggedArray<float> lum(W,H);for(int y=0;y<H;++y)for(int x=0;x<W;++x)((float**)lum)[y][x]=src[3*(y*W+x)]*327.68f;rtengine::LabImage lab{W,H,lum};rtengine::ImProcFunctions ip;rtengine::procparams::SharpeningParams p;p.radius=sigma;p.amount=int(amount*100+.5f);p.contrast=0;p.halocontrol=mode=="rt-halo";p.halocontrol_amount=int(parameter);if(mode=="rt-open"){p.threshold=rtengine::procparams::Threshold<int>(0,0,false);}ip.sharpening(&lab,p,false);for(int y=0;y<H;++y)for(int x=0;x<W;++x)result[3*(y*W+x)]=((float**)lum)[y][x]/327.68f;\n'''
dtmain='''std::vector<float> rgba(size_t(W)*H*4),rgbaout;for(int i=0;i<W*H;++i){for(int c=0;c<3;++c)rgba[4*i+c]=src[3*i+c];rgba[4*i+3]=1;}rgbaout=rgba;dt_iop_sharpen_data_t p{2.5f*sigma,amount,parameter};dt_dev_pixelpipe_iop_t piece{&p,4,1};dt_iop_module_t self;dt_iop_roi_t roi{W,H,1};process(&self,&piece,rgba.data(),rgbaout.data(),&roi,&roi);for(int i=0;i<W*H;++i)for(int c=0;c<3;++c)result[3*i+c]=rgbaout[4*i+c];\n'''
end='''std::ofstream f(argv[3],std::ios::binary);f.write((char*)result.data(),result.size()*4);return f?0:2;}catch(const std::exception&e){fprintf(stderr,"%s\\n",e.what());return 1;}}\n'''
write('rt-probe.cpp',rtpre+main+rtmain+end);write('dt-probe.cpp',dtpre+main+dtmain+end)
results=[]
for kind in ['dt','rt']:
 cmd=[str(compiler),'-O2','-std=gnu++20','-ffp-contract=off','-U__SSE2__',str(out/(kind+'-probe.cpp'))]
 if kind=='rt':cmd +=[str(out/'gauss.cc')]
 cmd +=['-I'+str(out),'-o',str(out/(kind+'-probe.exe'))]
 r=subprocess.run(cmd,capture_output=True,text=True,env=env);(out/(kind+'-build.txt')).write_text(r.stdout+r.stderr);print(kind,r.returncode,r.stderr[-5000:]);results.append({'kind':kind,'command':cmd,'exitCode':r.returncode,'sourceSha256':sha(out/(kind+'-probe.cpp')),'executableSha256':sha(out/(kind+'-probe.exe')) if r.returncode==0 else None})
manifest={'version':'Round13FixedReferenceHarnessV1','compilerSha256':sha(compiler),'results':results,'scope':'Exact upstream process/sharpening/halo/Threshold and real gauss scalar; adapters allocation/Lab struct only. RT contrast=0 explicitly selects actual all-one buildBlendMask branch; no bilateral/rld/preblur. Not complete app pipeline or SIMD parity.','references':[{'file':p.name,'sha256':sha(p)} for p in refs.iterdir() if p.is_file() and p.suffix!='.gz'],'extractedBodies':{'dtProcessSha256':hashlib.sha256(block(dt,'void process(').encode()).hexdigest(),'rtSharpeningSha256':hashlib.sha256(block(rt,'void ImProcFunctions::sharpening (').encode()).hexdigest(),'rtHaloSha256':hashlib.sha256(block(rt,'void sharpenHaloCtrl (').encode()).hexdigest()}}
(repo/'docs/research/color-engine-20261029/home-round13/reference-build-v2.json').write_text(json.dumps(manifest,indent=2));sys.exit(1 if any(r['exitCode'] for r in results) else 0)
