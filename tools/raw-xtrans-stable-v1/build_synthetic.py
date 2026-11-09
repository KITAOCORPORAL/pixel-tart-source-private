# Build exact upstream XTrans, CIELab and border methods in a no-RAW storage surrogate.
from pathlib import Path
import argparse,subprocess,os,json,hashlib,re,sys
from prepare_candidate import change,BASE

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def block(s,anchor):
 a=s.index(anchor);b=s.index('{',a);i=b+1;depth=1
 while depth:
  depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[a:i]
def main():
 p=argparse.ArgumentParser();p.add_argument('--source-root',type=Path,required=True);p.add_argument('--candidate',type=Path,required=True);p.add_argument('--compiler',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();here=Path(__file__).resolve().parent
 if a.output.exists():raise ValueError('New build directory required')
 a.output.mkdir();out=a.output;env=os.environ.copy();env['PATH']=str(a.compiler.parent)+os.pathsep+env['PATH'];commands=[]
 def read(n):return (a.source_root/n).read_bytes().replace(b'\r\n',b'\n').decode()
 original=read('src/demosaic/xtrans_demosaic.cpp');expected,_=change(original);candidate=a.candidate.read_text();assert expected==candidate,'Candidate does not match fixed V1 generator'
 source_hashes={n:hashlib.sha256(read(n).encode()).hexdigest() for n in ['src/demosaic/xtrans_demosaic.cpp','src/demosaic/ahd_demosaic.cpp','src/demosaic/misc_demosaic.cpp','src/utils/utils_libraw.cpp','src/tables/colorconst.cpp','internal/defines.h','libraw/libraw_alloc.h']}
 defines=read('internal/defines.h').splitlines();macros='\n'.join(x for x in defines if re.match(r'#define (FORC\(|FORC3 |FORC4 |FORCC |SQR\(|MIN\(|MAX\(|LIM\(|CLIP\(|ABS\()',x))
 prefix='#include "synthetic_support.hpp"\n#define LIBRAW_USE_OPENMP 1\n#define LIBRAW_AHD_TILE 512\n'+macros+'\n'
 # Exact upstream supporting algorithms, allocator only is surrogate.
 utils=read('src/utils/utils_libraw.cpp');misc=read('src/demosaic/misc_demosaic.cpp');ahd=read('src/demosaic/ahd_demosaic.cpp');constants=read('src/tables/colorconst.cpp');common='\n'.join([block(ahd,'void LibRaw::cielab('),block(misc,'void LibRaw::border_interpolate(').replace('{','{\n  ++borderCalls;',1),block(utils,'char** LibRaw::malloc_omp_buffers('),block(utils,'void LibRaw::free_omp_buffers(')])
 const_text='\n'.join(re.search(pattern,constants,re.S).group(0) for pattern in [r'const double LibRaw_constants::xyz_rgb\[3\]\[3\] = .*?;',r'const float LibRaw_constants::d65_white\[3\] = .*?;'])
 include='#include "../../internal/dcraw_defs.h"'
 baseline=original.replace(include,'')
 oracle=original.replace(include,'').replace('LibRaw::xtrans_interpolate','LibRaw::oracle_tile')
 top='    for (int top = 3; top < height - 19; top += LIBRAW_AHD_TILE - 16)\n    {';left='        for (int left = 3; left < width - 19; left += LIBRAW_AHD_TILE - 16)\n        {'
 assert oracle.count(top)==oracle.count(left)==1
 oracle=oracle.replace(top,top+'\n        if (top != tileTop) continue;').replace(left,left+'\n            if (left != tileLeft) continue;').replace('    border_interpolate(8);','    // Tile oracle: defer original border8 until all tile outputs are assembled.')
 cand=candidate.replace(include,'').replace('LibRaw::xtrans_interpolate','LibRaw::candidate_interpolate')
 # Test-only phase hooks are outside workers; candidate math has no per-tile hook.
 cand=cand.replace('  size_t buffer_size =','  captureStable();\n  size_t buffer_size =',1).replace('    free_omp_buffers(buffers, buffer_count);','    free_omp_buffers(buffers, buffer_count);\n    verifyStable();',1).replace('    border_interpolate(8);','    borderAfterCommit = stableChecked;\n    border_interpolate(8);')
 generated=prefix+const_text+'\n'+common+'\n'+baseline+'\n'+oracle+'\n'+cand
 (out/'algorithms.cpp').write_text(generated,encoding='utf-8',newline='\n');(out/'synthetic_support.hpp').write_bytes((here/'synthetic_support.hpp').read_bytes());(out/'synthetic_main.cpp').write_bytes((here/'synthetic_main.cpp').read_bytes());(out/'results').mkdir()
 cmd=[str(a.compiler),'-std=c++17','-O2','-fopenmp','-Wno-unknown-pragmas',str(out/'algorithms.cpp'),str(out/'synthetic_main.cpp'),'-I'+str(out),'-o',str(out/'synthetic.exe')];commands.append(cmd);r=subprocess.run(cmd,env=env,capture_output=True,text=True);(out/'compile.log').write_text(r.stdout+r.stderr);print('BUILD',r.returncode,r.stderr[-6000:],flush=True)
 report={'version':'StableInputV1SyntheticBuildV1','compiler':str(a.compiler),'compilerSha256':sha(a.compiler),'commands':commands,'compileExitCode':r.returncode,'generatedAlgorithmSha256':sha(out/'algorithms.cpp'),'sourceHashes':source_hashes,'scope':'Extracted exact upstream functions; surrogate storage/allocator, synthetic CFA only. Not LibRaw DLL/ABI/defaultdecoder build, not MSVC or VCOMP parity.'}
 if r.returncode:
  (out/'BUILD_RECEIPT.json').write_text(json.dumps(report,indent=2));sys.exit(r.returncode)
 # Real LibRaw headers, not surrogate class: verify candidate also compiles syntactically.
 cmd=[str(a.compiler),'-std=c++17','-fopenmp','-DLIBRAW_USE_OPENMP','-fsyntax-only','-I'+str(a.source_root),'-I'+str(a.source_root/'src/demosaic'),str(a.candidate)];commands.append(cmd);q=subprocess.run(cmd,env=env,capture_output=True,text=True);(out/'real-headers-syntax.log').write_text(q.stdout+q.stderr);report['actualLibRawHeaderSyntaxExitCode']=q.returncode;report['executableSha256']=sha(out/'synthetic.exe');report['libompSha256']=sha(a.compiler.parent/'libomp.dll');(out/'BUILD_RECEIPT.json').write_text(json.dumps(report,indent=2)+'\n');print('REAL_HEADERS_SYNTAX',q.returncode,q.stderr[-2000:],flush=True)
 if q.returncode:sys.exit(q.returncode)
if __name__=='__main__':main()
