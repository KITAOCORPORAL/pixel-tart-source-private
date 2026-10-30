"""Add one bounded passive node tap only in a clean fixed external checkout."""
from pathlib import Path
import argparse,subprocess,hashlib,json,sys
p=argparse.ArgumentParser();p.add_argument('checkout',type=Path);a=p.parse_args();root=a.checkout.resolve()
if subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()!='c1447c6ab618fa4b1cb11fd2fe12826855007059':raise ValueError('Wrong fixed engine source')
if subprocess.check_output(['git','-C',str(root),'status','--porcelain'],text=True).strip():raise ValueError('Use a new clean detached external checkout')
if (root/'src/RAWSelectionAssistant.Core').exists():raise ValueError('Product source prohibited')
p=root/'engine/src/pipeline/pipeline.cpp';s=p.read_text(encoding='utf-8');start=s.index('bool Pipeline::node_decode_input(');end=s.index('bool Pipeline::node_geometry',start);chunk=s[start:end];chunk=chunk.replace('    return gpu_->dispatch(', '    const bool ok = gpu_->dispatch(',1);chunk=chunk.replace('in.elements(), error);\n}', '''in.elements(), error);
    // Passive bounded research tap; no change to the returned RGB buffer.
    if (ok) if (const char* path=std::getenv("SPK_R5_DECODE_FILE")) {
        std::vector<float> values(out.elements());std::string tap_error;
        if (!gpu_->read(out.buf.get(),0,values.data(),out.bytes(),tap_error)) return false;
        if (FILE* file=std::fopen(path,"wb")) {std::fwrite(values.data(),4,values.size(),file);std::fclose(file);}
        else {error="Cannot write bounded decode research tap";return false;}
    }
    return ok;
}''');s=s[:start]+chunk+s[end:];s=s.replace('#include <chrono>','#include <cstdio>\n#include <cstdlib>\n#include <chrono>');p.write_text(s,encoding='utf-8')
subprocess.run([sys.executable,str(root/'modern_UI/Spektrafilm/Tools/gen-project.py')],capture_output=True,check=True)
print(json.dumps({'file':'engine/src/pipeline/pipeline.cpp','sourceSha256':hashlib.sha256(p.read_bytes()).hexdigest(),'researchOnly':True}))
