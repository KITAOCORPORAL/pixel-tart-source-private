import pathlib,hashlib,subprocess,json
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/photo-engine';source=A/'references/darktable-filmicrgb.c';s=source.read_text(encoding='utf-8')
def extract(signature):
 start=s.index(signature);b=s.index('{',start);i=b+1;depth=1
 while depth:
  if s[i]=='{':depth+=1
  elif s[i]=='}':depth-=1
  i+=1
 return s[start:i]
header='#include <cmath>\n#include <cstdio>\nusing dt_aligned_pixel_t=float[4];using dt_iop_filmicrgb_curve_type_t=int;enum{DT_FILMIC_CURVE_POLY_4,DT_FILMIC_CURVE_POLY_3,DT_FILMIC_CURVE_RATIONAL};inline float clamp_simd(float x){return fminf(fmaxf(x,0),1); }\n'
wrapper=r'''int main(){float r,g,b;dt_aligned_pixel_t M1={0,0,0,0},M2={1,1,1,0},M3={0,0,0,0},M4={0,0,0,0},M5={0,0,0,0};int type[2]={DT_FILMIC_CURVE_POLY_3,DT_FILMIC_CURVE_POLY_3};while(scanf("%f %f %f",&r,&g,&b)==3){float v[3]={r,g,b};for(int i=0;i<3;i++){float x=log_tonemapping_v2_1ch(fmaxf(v[i],1e-8f),.18f,-8.f,12.f);v[i]=filmic_spline(x,M1,M2,M3,M4,M5,0.f,1.f,type);}printf("%.10g %.10g %.10g\n",v[0],v[1],v[2]);}}'''
p=A/'references/dt_substage.cpp';p.write_text(header+extract('static inline float log_tonemapping_v2_1ch')+'\n'+extract('static inline float filmic_spline(')+'\n'+wrapper,encoding='utf-8');zig=r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/compiler/ziglang/zig.exe';r=subprocess.run([zig,'c++','-O2','-std=c++17',str(p),'-o',str(A/'dt_substage.exe')],text=True,capture_output=True);print(json.dumps(dict(exit=r.returncode,stdout=r.stdout,stderr=r.stderr,upstreamSha256=hashlib.sha256(source.read_bytes()).hexdigest(),scope='actualdarktablelognormalization+identitysplinehelperONLYnotfullmodule',params={'grey':.18,'blackStops':-8,'dynamicRangeStops':12,'spline':'identitylatitude0..1'}),indent=2));raise SystemExit(r.returncode)
