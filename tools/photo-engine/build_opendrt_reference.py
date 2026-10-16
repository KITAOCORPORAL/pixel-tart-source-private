"""Compile upstream GPL reference locally for scalar STILL RGB samples only; never ships/copies into product."""
import pathlib,re,subprocess,json,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/photo-engine';source=A/'references/OpenDRT.dctl';s=source.read_text();assert hashlib.sha256(source.read_bytes()).hexdigest()=='645ba89745691742c5403f8de5964b5934cd1c59bf06fb1f1a1f6b45f54830a1'
def ui(m):
 line=m.group(0);args=line[len('DEFINE_UI_PARAMS('):-1].split(',');name=args[0].strip();default=args[3].strip();v={'in_gamut':'5','in_oetf':'0','display_encoding_preset':'1'}.get(name,default);return 'static '+('int' if 'DCTLUI_COMBO_BOX' in line or 'DCTLUI_CHECK_BOX' in line else 'float')+' '+name+'='+v+';'
s=re.sub(r'^DEFINE_UI_PARAMS\(.*\)$',ui,s,flags=re.M)
header=r'''#include <cmath>
#include <cstdio>
#include <algorithm>
#define __DEVICE__ inline
#define __CONSTANT__ static const
#define __RESOLVE_VER_MAJOR__ 0
#define __RESOLVE_VER_MINOR__ 0
using float3=__attribute__((ext_vector_type(3))) float;
using float2=__attribute__((ext_vector_type(2))) float;
inline float3 make_float3(float a,float b,float c){return (float3){a,b,c};}
inline float2 make_float2(float a,float b){return (float2){a,b};}
#define _powf std::pow
#define _sqrtf std::sqrt
#define _fmaxf std::fmax
#define _fminf std::fmin
#define _exp2f std::exp2
#define _expf std::exp
#define _exp10f(x) std::pow(10.0f,(x))
#define _logf std::log
#define _fabs std::fabs
#define _fmod std::fmod
#define _log2f std::log2
#define _atan2f std::atan2
'''
footer=r'''int main(){float r,g,b;while(scanf("%f %f %f",&r,&g,&b)==3){float3 v=transform(1,1,0,0,r,g,b);printf("%.10g %.10g %.10g\n",v.x,v.y,v.z);}return 0;}'''
p=A/'references/opendrt_scalar.cpp';p.write_text(header+s+footer);zig=r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/compiler/ziglang/zig.exe';r=subprocess.run([zig,'c++','-O2','-std=c++17',str(p),'-o',str(A/'opendrt_scalar.exe')],text=True,capture_output=True);print(json.dumps({'exit':r.returncode,'stdout':r.stdout,'stderr':r.stderr,'upstreamSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'generatedLocalOnlySha256':hashlib.sha256(p.read_bytes()).hexdigest(),'params':{'input':'linearRec709/D65','look':'Standard','tonescale':'lookdefault','display':'sRGB2.2power/Rec709','peakNits':100},'scope':'localstillRGBscalarcompatibility; no Resolve/video/Log/FFmpegworkflow'},indent=2));raise SystemExit(r.returncode)
