"""Run pinned upstream scalar CPU stages in external GPL harnesses; no source enters product."""
import argparse,base64,hashlib,json,os,re,subprocess
from pathlib import Path
DT='fb0eb997e62c6dd52966cd35911c74afa150e209';RT='db575c6690d30ce6428c0e1383bec8b8f1fb8d21'
EXPECTED={'dt-exposure.c': '2b52c149c5dba1a7c47acdb7f2a18f6482710b8861b64971cd766b22f312af28', 'dt-LICENSE': '8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903', 'rt-improcfun.cc': 'adba83f7a2cdc9351e465239b4a2edadfc36f760765df6dc89b584e84c921f6b', 'rt-curves.cc': '4a8bc2215d5a1ac62120df52a9eef17842e72ebede890a11cc2d34a8053663bb', 'rt-curves.h': 'fee25e75d0dc863cb5cf3fbc37dc8649c0fa8c71466ab9f91f7d8176860db83c', 'rt-LUT.h': '74d3a4e603f23fcea211145b8e6de0eb2d5f073767b390375f590dc58c27dfe2', 'rt-LICENSE': '8c75a9231db8906865e29706ed63aad18fd918c92c15dbefbb13c25fe42850b4'}
def sha(p):
 h=hashlib.sha256()
 with Path(p).open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
def block(s,anchor):
 p=s.index(anchor);q=s.index('{',p);depth=1;i=q+1
 while depth:
  depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[p:i]
def main():
 p=argparse.ArgumentParser();p.add_argument('--local-root',type=Path,required=True);p.add_argument('--compiler',type=Path,required=True);a=p.parse_args();root=a.local_root.resolve();refs=root/'references';refs.mkdir(exist_ok=True,parents=True);build=root/'reference-build';build.mkdir(exist_ok=True)
 entries=[]
 for repo,commit,path,name in [('darktable-org/darktable',DT,'src/iop/exposure.c','dt-exposure.c'),('darktable-org/darktable',DT,'LICENSE','dt-LICENSE'),('RawTherapee/RawTherapee',RT,'rtengine/improcfun.cc','rt-improcfun.cc'),('RawTherapee/RawTherapee',RT,'rtengine/curves.cc','rt-curves.cc'),('RawTherapee/RawTherapee',RT,'rtengine/curves.h','rt-curves.h'),('RawTherapee/RawTherapee',RT,'rtengine/LUT.h','rt-LUT.h'),('RawTherapee/RawTherapee',RT,'LICENSE','rt-LICENSE')]:
  dest=refs/name
  if not dest.exists():
   r=subprocess.run(['gh','api',f'repos/{repo}/contents/{path}?ref={commit}'],capture_output=True,text=True,timeout=60);r.check_returncode();dest.write_bytes(base64.b64decode(json.loads(r.stdout)['content']))
  if sha(dest)!=EXPECTED[name]:raise ValueError('Pinned reference SHA mismatch: '+name)
  entries.append(dict(repository=repo,commit=commit,path=path,sha256=sha(dest),bytes=dest.stat().st_size))
 dt=(refs/'dt-exposure.c').read_text();rt=(refs/'rt-improcfun.cc').read_text();curves=(refs/'rt-curves.cc').read_text();hdr=(refs/'rt-curves.h').read_text();lut=(refs/'rt-LUT.h').read_text()
 common=r'''#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <vector>
#include <algorithm>
#include <fstream>
#include <stdexcept>
#include <iostream>
using std::log1p; using std::max; using std::min;
'''
 dtfn=block(dt,'void process(')
 dtpre=r'''#define restrict __restrict
#define DT_OMP_FOR_SIMD(...)
struct dt_iop_module_t {};
struct dt_iop_exposure_data_t {float black,scale;};
struct Pipe { struct {float processed_maximum[3];} dsc;};
struct dt_dev_pixelpipe_iop_t {void* data; int colors; Pipe* pipe;};
struct dt_iop_roi_t {int width,height;};
void _process_common_setup(dt_iop_module_t*,dt_dev_pixelpipe_iop_t*){}
'''
 dtmain=r'''int main(int argc,char**v){if(argc!=5)return 2;float ev=std::stof(v[3]);int n=std::stoi(v[4]);std::vector<float> in(n*3),out(n*3);std::ifstream src(v[1],std::ios::binary);if(!src.read((char*)in.data(),in.size()*4))return 3;dt_iop_exposure_data_t d{0,std::exp2(ev)};Pipe pipe{};dt_dev_pixelpipe_iop_t piece{&d,3,&pipe};dt_iop_roi_t roi{n,1};process(nullptr,&piece,in.data(),out.data(),&roi,&roi);std::ofstream(v[2],std::ios::binary).write((char*)out.data(),out.size()*4);}
'''
 op=block(lut,'    template<typename U = T, typename V,')
 rtpre=r'''#define MAXVALF 65535.f
#define LUT_CLIP_BELOW 1
#define LUT_CLIP_ABOVE 2
inline double xlog(double x){return std::log(x);}
struct LUTf {
 using T=float;std::vector<float> storage;float*data;int maxs=65534;float maxsf=65534;unsigned int upperBound=65535,clip=1;
 LUTf():storage(65536),data(storage.data()){}
 float& operator[](int i){return storage.at(i);}
 void setClip(int x){clip=x;}
 void makeConstant(float v,unsigned int end=0){end=end==0?65536:end;end=std::min(end,65536u);for(unsigned int i=0;i<end;i++)data[i]=v;}
'''+op+'\n};\nstruct CurveFactory {\n'+block(hdr,'    static inline float hlcurve(')+'\n};\n'
 start=curves.index('    hlCurve.setClip(');cond=curves.index('    if (comp <= 0.0f)',start);whole=block(curves,'    if (comp <= 0.0f)');end=cond+len(whole);elsepos=curves.index(' else {',end-1);elseblock=block(curves[elsepos:],' else {');factory=curves[start:end]+elseblock
 # else block above is literal original: do not substitute formulas or smooth reference outputs.
 rtfn=block(rt,'void highlightToneCurve(')
 rtmain=r'''LUTf makeCurve(float a,double ecomp,double hlcompr,double hlcomprthresh){LUTf hlCurve;
'''+factory+r'''
return hlCurve;}
int main(int argc,char**v){if(argc!=6)return 2;float ev=std::stof(v[3]);int n=std::stoi(v[4]);double hc=std::stod(v[5]);float gain=std::exp2(ev);auto lut=makeCurve(gain,ev,hc,50);float comp=(max(0.f,ev)+1)*hc/100;float shoulder=(65536.f/max(1.f,gain))*.25f+.1f;std::vector<float> rgb(n*3),r(n),g(n),b(n);std::ifstream src(v[1],std::ios::binary);if(!src.read((char*)rgb.data(),rgb.size()*4))return 3;for(int i=0;i<n;i++){r[i]=rgb[3*i]*65535;g[i]=rgb[3*i+1]*65535;b[i]=rgb[3*i+2]*65535;}highlightToneCurve(lut,r.data(),g.data(),b.data(),0,1,0,n,n,gain,comp,65536-shoulder);for(int i=0;i<n;i++){rgb[3*i]=r[i]/65535;rgb[3*i+1]=g[i]/65535;rgb[3*i+2]=b[i]/65535;}std::ofstream(v[2],std::ios::binary).write((char*)rgb.data(),rgb.size()*4);}
'''
 records=[]
 a.compiler=a.compiler.resolve();env=os.environ.copy();env['PATH']=str(a.compiler.parent)+os.pathsep+env['PATH']
 for name,text in [('dt',common+dtpre+dtfn+dtmain),('rt',common+rtpre+rtfn+rtmain)]:
  src=build/(name+'.cpp');src.write_text(text);exe=build/(name+'.exe');cmd=[str(a.compiler),'-std=c++17','-O2','-ffp-contract=off','-U__SSE2__',str(src),'-o',str(exe)];r=subprocess.run(cmd,env=env,capture_output=True,text=True);(build/(name+'-compile.txt')).write_text(r.stdout+r.stderr);r.check_returncode();records.append(dict(name=name,command=cmd,sourceSha256=sha(src),exeSha256=sha(exe)))
 report=dict(frozenVersions={'darktable':'4.8.1','RawTherapee':'5.11'},version='PortraitHighlightReferencesV1',scope='Original isolated scalar CPU stages, NOT complete darktable/RawTherapee applications. Pixel space supplied explicitly; no RAW decode, ICC pipeline, filmic or export reference.',sources=entries,build=records,compilerSha256=sha(a.compiler),extracted=dict(dtProcess=hashlib.sha256(dtfn.encode()).hexdigest(),rtHighlightToneCurve=hashlib.sha256(rtfn.encode()).hexdigest(),rtCurveFactory=hashlib.sha256(factory.encode()).hexdigest()),config=dict(dt=dict(black=0,deflicker=False,biasCompensation=False),rt=dict(brightness=0,black=0,contrast=0,hlcompr=[0,60],hlcomprthresh=50,autoExposure=False,rawReconstruction=False,scalar=True,internalScale=65535)))
 (root/'reference-manifest.json').write_text(json.dumps(report,indent=2));print(json.dumps(records,indent=2))
if __name__=='__main__':main()
