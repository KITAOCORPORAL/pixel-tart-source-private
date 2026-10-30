"""Apply only inside an explicit isolated external research checkout; never product src.
No upstream source/resource is vendored. See COMPANY_ROUND4_REPORT and licensing gate.
"""
from pathlib import Path
import argparse,subprocess,hashlib,json,shutil
p=argparse.ArgumentParser();p.add_argument('source',type=Path);group=p.add_mutually_exclusive_group();group.add_argument('--trace',action='store_true');group.add_argument('--instrument',action='store_true');a=p.parse_args();root=a.source.resolve()
if subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()!='c1447c6ab618fa4b1cb11fd2fe12826855007059':raise ValueError('Wrong external source')
if subprocess.check_output(['git','-C',str(root),'status','--porcelain'],text=True).strip():raise ValueError('Use a fresh isolated external checkout')
if (root/'src/RAWSelectionAssistant.Core').exists():raise ValueError('Not an external engine checkout')
if not a.trace:
 f=root/'engine/src/shaders/vulkan/spk_cctf_decode.comp';s=f.read_text(encoding='utf-8');start=s.index('            // Keep alpha/beta');end=s.index('            return breakpoint > value',start)
 original=s
 s=s[:start]+'''            // Research SpektraBtJoinFloatV1: freeze the original CPU/float
            // compatibility join (0x3da66555) rather than re-evaluating pow
            // in a driver-sensitive branch condition. Rounded BT constants
            // are discontinuous: a one-ULP branch drift is not a small error.
            float alpha = 1.099;
            float breakpoint = uintBitsToFloat(0x3da66555u);
'''+s[end:]
 if a.instrument:
  s=original.replace('        default: return value;', '''        case 6u: { float alpha=1.099, beta=0.018; return alpha*pow(beta,0.45)-(alpha-1.0); }
        case 7u: { float alpha=1.099, beta=0.018; float bp=alpha*pow(beta,0.45)-(alpha-1.0); return bp>value ? 0.0 : 1.0; }
        default: return value;''')
 f.write_text(s)
 shutil.copyfile(Path(__file__).with_name('bt_probe.cpp'),root/'engine/tests/bt_probe.cpp')
 with (root/'engine/CMakeLists.txt').open('a') as stream:stream.write('\nadd_executable(spk_bt_probe tests/bt_probe.cpp)\ntarget_include_directories(spk_bt_probe PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src")\ntarget_link_libraries(spk_bt_probe PRIVATE spektralab_engine)\n')
else:
 f=root/'engine/src/pipeline/pipeline.cpp';s=f.read_text(encoding='utf-8');s=s.replace('#include <chrono>','#include <cstdio>\n#include <cstdlib>\n#include <chrono>');pos=s.index('// --- the two matrix')
 s=s[:pos]+'''// Research-only passive stage readback. Output buffers are untouched.
static void research_tap(gpu::Gpu* gpu,const Image& image,const char* stage) {
 const char* root=std::getenv("SPK_RESEARCH_TRACE");if(!root||!image.valid())return;
 static unsigned serial=0;std::vector<float> pixels(image.elements());std::string error;
 if(!gpu->read(image.buf.get(),0,pixels.data(),image.bytes(),error))return;
 char path[2048];std::snprintf(path,sizeof path,"%s/%04u-%s-%u-%u-%u.f32",root,serial++,stage,image.h,image.w,image.c);
 if(FILE* f=std::fopen(path,"wb")){std::fwrite(pixels.data(),4,pixels.size(),f);std::fclose(f);}
}
'''+s[pos:]
 for segment,table in [('film_segment','kFilmStages'),('print_segment','kPrintStages')]:
  start=s.index('bool Pipeline::'+segment);front=s[:start];back=s[start:];back=back.replace('        cur = next;',f'        research_tap(gpu_,next.cur,{table}[i].name);\n        cur = next;',1);s=front+back
 s=s.replace("                    // The halo's precondition", "                    research_tap(gpu_,next.cur,stages[k].name);\n                    // The halo's precondition")
 s=s.replace('if (!(this->*stages[i].run)(chain, next, error)) return false;','if (!(this->*stages[i].run)(chain, next, error)) return false;research_tap(gpu_,next.cur,stages[i].name);')
 s=s.replace('SPK_NODE(node_expose_log(in.cur, next, error));','SPK_NODE(node_expose_log(in.cur, next, error));research_tap(gpu_,next,"log_exposure");').replace('SPK_NODE(node_film_curves(out.log_e_film, cmy, error));','SPK_NODE(node_film_curves(out.log_e_film, cmy, error));research_tap(gpu_,cmy,"film_curves_exact");');f.write_text(s,encoding='utf-8')
subprocess.run([__import__('sys').executable,str(root/'modern_UI/Spektrafilm/Tools/gen-project.py')],check=True,capture_output=True)
print(json.dumps({'researchOnly':True,'mode':'trace' if a.trace else 'instrument' if a.instrument else 'SpektraBtJoinFloatV1','file':str(f),'sourceSha256':hashlib.sha256(f.read_bytes()).hexdigest()}))
