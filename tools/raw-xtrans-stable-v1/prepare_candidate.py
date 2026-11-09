# Exact fixed-source candidate generation; no decoder invocation or compilation.
from pathlib import Path
import argparse,hashlib,json,difflib,re
BASE='1c4aa7362d4059ea3952dbc9a09de73f13721aa18f65d2445d027e68352045af'
OBSERVER='cf4616cc0f437d28e52b6131b5e241ee2da96e587c8b86f4d62c76f58e8c84fc'
VERSION='XTransStableInputSeparateRgbV1'
def sha(b):return hashlib.sha256(b).hexdigest()
def change(source):
 raw=source.encode();need=lambda v,m:None if v else (_ for _ in ()).throw(ValueError(m))
 need(sha(raw) in [BASE,OBSERVER],'Fixed upstream or reviewed observer xtrans bytes required; newline drift refused')
 def once(old,new):
  nonlocal source
  need(source.count(old)==1,'Ambiguous/missing anchor: '+old[:70]);source=source.replace(old,new)
 once('#include "../../internal/dcraw_defs.h"','#include "../../internal/dcraw_defs.h"\n#include <limits>')
 anchor='  size_t buffer_size = LIBRAW_AHD_TILE * LIBRAW_AHD_TILE * (ndir * 11 + 6);'
 prepare='''  // @candidate XTransStableInputSeparateRgbV1: diagnostic opt-in build only.
  // Serial green-bound preparation above is complete. Keep image read-only
  // until the unchanged OpenMP region has joined; preserve all source slots.
  const size_t output_width = size_t(width), output_height = size_t(height);
  const size_t size_limit = (std::numeric_limits<size_t>::max)();
  if (!output_width || output_height > size_limit / output_width ||
      output_width * output_height > size_limit / (3 * sizeof(ushort)))
    throw LIBRAW_EXCEPTION_ALLOC;
  const size_t output_pixels = output_width * output_height;
  struct XTransOutputGuardV1 {
    LibRaw *owner;
    ushort (*pixels)[3];
    ~XTransOutputGuardV1() { if (pixels) owner->free(pixels); }
  } output = {this, (ushort(*)[3])malloc(output_pixels * 3 * sizeof(ushort))};
  // Seed every RGB output pixel, including the untouched outside region.
  // Thus no unwritten allocation/padding becomes input to border handling.
  for (size_t p = 0; p < output_pixels; ++p)
    memcpy(output.pixels[p], image[p], 3 * sizeof(ushort));
  const ushort (*const stable_image)[4] = image;
  ushort (*const final_rgb)[3] = output.pixels;

'''
 once(anchor,prepare+anchor)
 once('firstprivate(buffers, allhex, passes, sgrow, sgcol, ndir) shared(dir)','firstprivate(buffers, allhex, passes, sgrow, sgcol, ndir, stable_image, final_rgb) shared(dir)')
 start=source.index('    for (int top = 3;');end=source.index('    free_omp_buffers(',start);worker=source[start:end];original_worker=worker
 need(worker.count('image[')==2 and worker.count('image + row * width + col')==2,'Shared image access inventory drift')
 worker=worker.replace('memcpy(rgb[0][row - top][col - left], image[row * width + col], 6);','memcpy(rgb[0][row - top][col - left], stable_image[row * width + col], 6);')
 worker=worker.replace('ushort (*pix)[4] = image + row * width + col;','const ushort (*pix)[4] = stable_image + row * width + col;')
 worker=worker.replace('ushort(*pix)[4] = image + row * width + col;','const ushort(*pix)[4] = stable_image + row * width + col;')
 worker=worker.replace('FORC3 image[(row + top) * width + col + left][c] = avg[c] / avg[3];','FORC3 final_rgb[(row + top) * width + col + left][c] = avg[c] / avg[3];')
 need(re.search(r'\bimage\b',worker) is None,'Worker still accesses mutable image')
 inverse=worker.replace('stable_image','image').replace('final_rgb[(row + top) * width + col + left]','image[(row + top) * width + col + left]').replace('const ushort (*pix)[4]','ushort (*pix)[4]').replace('const ushort(*pix)[4]','ushort(*pix)[4]')
 need(inverse==original_worker,'Worker equations/control/order changed')
 source=source[:start]+worker+source[end:]
 once('    free_omp_buffers(buffers, buffer_count);','''    free_omp_buffers(buffers, buffer_count);

    // The implicit parallel-for join above is the only commit boundary.
    // Preserve slot 3 (green upper-bound state) and the original border order.
    for (size_t p = 0; p < output_pixels; ++p)
      memcpy(image[p], output.pixels[p], 3 * sizeof(ushort));
    free(output.pixels);
    output.pixels = 0;''')
 return source,{'workerOriginalSha256':sha(original_worker.encode()),'workerInverseNormalizedExact':True,'workerCandidateSha256':sha(worker.encode()),'remainingMutableImageWorkerUses':0,'sharedReadAliases':3,'outputStoreSites':1,'originalSchedule':'parallel for schedule(dynamic)','originalPassesAndFormulasPreserved':True}
def main():
 p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--enable-candidate',choices=[VERSION],required=True);a=p.parse_args()
 if a.output.exists():raise ValueError('New output root required, never overwrite')
 output=a.output.resolve();repository=Path(__file__).resolve().parents[2];sourceparent=a.source.resolve().parent
 if output==repository or repository in output.parents or output==sourceparent or sourceparent in output.parents:raise ValueError('Candidate output must stay outside research Git checkout and input source directory')
 checkout_raw=a.source.read_bytes();raw=checkout_raw.replace(b'\r\n',b'\n');new,proof=change(raw.decode('utf-8'));a.output.mkdir(parents=True,exist_ok=False);target=a.output/'xtrans_demosaic.cpp';target.write_bytes(new.encode());patch=''.join(difflib.unified_diff(raw.decode().splitlines(True),new.splitlines(True),fromfile='a/src/demosaic/xtrans_demosaic.cpp',tofile='b/src/demosaic/xtrans_demosaic.cpp'))
 (a.output/'stable-input-v1.patch').write_text(patch,encoding='utf-8',newline='\n')
 manifest={'version':VERSION,'defaultOff':True,'isolation':'Standalone diagnostic build only; no product registration','inputSource':str(a.source.resolve()),'inputSha256':sha(raw),'checkoutBytesSha256':sha(checkout_raw),'checkoutNewlineNormalization':'CRLF to LF explicitly, locked canonical fullfileSHA required','candidateSha256':sha(target.read_bytes()),'patchSha256':sha(patch.encode()),'proof':proof,'extraBytesPerPixel':6,'real7752x5184ExtraRequestedBytes':7752*5184*6,'sourceOwner':'Original image kept stable after serial prep; compact independentRGB3 committed afterjoin before border8','memoryScope':'6N requestedbytes plus one libraw_memmgr extra_bytes allocation overhead; memmgr is ownership tracking, NOT a hard memory budget','numericalRuns':0}
 (a.output/'GENERATION.json').write_text(json.dumps(manifest,indent=2)+'\n');print(json.dumps(manifest,indent=2))
if __name__=='__main__':main()
