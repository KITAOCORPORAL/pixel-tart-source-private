"""Generate separately versioned natural-photo admission; original Canon entry untouched."""
from pathlib import Path
import argparse,json,hashlib,subprocess,re
p=argparse.ArgumentParser();p.add_argument('frozen_stream_source',type=Path);p.add_argument('fresh_source',type=Path);p.add_argument('fixtures',type=Path);p.add_argument('--trace',action='store_true');a=p.parse_args();root=a.fresh_source.resolve()
if subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()!='c1447c6ab618fa4b1cb11fd2fe12826855007059' or subprocess.check_output(['git','-C',str(root),'status','--porcelain'],text=True).strip():raise ValueError('Fresh pinned source required')
files=['engine/src/pipeline/engine.cpp','engine/src/pipeline/pipeline.cpp','engine/src/pipeline/pipeline.hpp','engine/src/gpu/gpu.hpp','engine/src/gpu/vulkan_gpu.cpp','engine/windows_exports.def','engine/CMakeLists.txt','API-SPEC-callable-render-service.md']
frozen=json.loads((Path(__file__).resolve().parents[1]/'cross-device-diagnostic/expected-stream-source.json').read_text())
if subprocess.check_output(['git','-C',str(a.frozen_stream_source),'rev-parse','HEAD'],text=True).strip()!=frozen['baseCommit']:raise ValueError('Wrong frozen stream base')
for rel in files:
 data=(a.frozen_stream_source/rel).read_bytes();normal=data
 if rel=='engine/src/pipeline/engine.cpp':
  text=data.decode('utf-8');matches=re.findall(r'const auto allowed=std::filesystem::weakly_canonical\(std::filesystem::path\("([^"\n]+)"\)\)',text)
  if len(matches)!=1:raise ValueError('Cannot uniquely identify compiled authorized root')
  normal=data.replace(matches[0].encode(),frozen['companyNativeRoot'].encode())
 if hashlib.sha256(normal).hexdigest()!=frozen['files'][rel]:raise ValueError('Frozen stream source drift: '+rel)
for rel in files:(root/rel).write_bytes((a.frozen_stream_source/rel).read_bytes())
manifest=json.loads((a.fixtures/'manifest.json').read_text());sources=manifest['sources'];
if manifest['version']!='NaturalProxyAdmissionV1' or not 1<=len(sources)<=6 or any(max(x['proxyShape'][:2])>1200 for x in sources):raise ValueError('Unsupported natural admission')
admission=json.loads((Path(__file__).parent/'photo-preparation/sources-v1.json').read_text());allowed={x['id']:x for x in admission['entries']}
if admission['version']!='NaturalCoverageAdmissionV1' or len({x['id'] for x in sources})!=len(sources):raise ValueError('Duplicate/mismatched new photo contract')
for item in sources:
 if item['id'] not in allowed or item['sha256']!=allowed[item['id']]['sha256'] or item['path']!=allowed[item['id']]['path'] or item['storedOrientation']!=1 or item['inputDomain']!='standard ProPhoto RGB D50 linear float32; negatives/>1 retained':raise ValueError('Natural source/domain not in new independent contract')
 f=a.fixtures/item['file']
 if not f.resolve().is_relative_to(a.fixtures.resolve()) or hashlib.sha256(f.read_bytes()).hexdigest()!=item['fileSha256']:raise ValueError('Natural proxy file changed')
 import numpy as np
 data=np.load(f,allow_pickle=False)
 if list(data.shape)!=item['proxyShape'] or data.dtype!=np.dtype('<f4') or not np.isfinite(data).all() or hashlib.sha256(data.tobytes()).hexdigest()!=item['pixelSha256']:raise ValueError('Natural proxy pixel identity changed')
syntheticSource='';approved=','.join('"'+x['pixelSha256']+'"' for x in sources)
f=root/'engine/src/pipeline/engine.cpp';s=f.read_text();start=s.index('// SpektraStreamingResearchV1: private synchronous row ABI.');copy=s[start:];copy=copy.replace('SpektraStreamingResearchV1','NaturalPhotographyStreamV1').replace('spk_stream_research_v1','spk_stream_natural_photo_v1').replace('R8Read','DRead').replace('R8Consume','DConsume').replace('R8Poll','DPoll')
# Private helper names remain shared from prior translation-unit injection; remove duplicate helper namespace.
ns=copy.index('namespace {');ex=copy.index('extern "C" __declspec(dllexport)');copy=copy[:ns]+copy[ex:]
copy=copy.replace('"sourceSha256","inputPixelSha256"','"sourceSha256","inputPixelSha256"')
pairs=' || '.join('(request.at("sourceSha256").as_string()=="'+x['sha256'].lower()+'" && request.at("inputPixelSha256").as_string()=="'+x['pixelSha256']+'" && width=='+str(x['proxyShape'][1])+' && height=='+str(x['proxyShape'][0])+')' for x in sources)
copy=copy.replace('request.at("sourceSha256").as_string()=="b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401"&&request.at("inputPixelSha256").as_string().size()==64','('+pairs+')')
needle='  r8_need(request.at("backendSha256").as_string()==r8_self_hash(),"Backend self identity changed");'
copy=copy.replace(needle,'  const std::vector<std::string> synthetic_hashes={'+approved+'}; r8_need(std::find(synthetic_hashes.begin(),synthetic_hashes.end(),request.at("inputPixelSha256").as_string())!=synthetic_hashes.end(),"Natural proxy not admitted");\n'+needle)
copy=copy.replace('const char* resources_dir,const char* request_json','const char* resources_dir,const char* request_json')
# Use existing callback typedefs, preserving C layout; no fictional natural source identity.
copy=copy.replace('DRead read_rows,DConsume consume,DPoll poll','R8Read read_rows,R8Consume consume,R8Poll poll')
s+='\n'+copy;f.write_text(s)
f=root/'engine/windows_exports.def';f.write_text(f.read_text()+'\n    spk_stream_natural_photo_v1\n')
if a.trace:
 f=root/'engine/src/pipeline/pipeline.cpp';s=f.read_text();s=s.replace('#include <chrono>','#include <chrono>\n#include <filesystem>\n#include <fstream>\n#include <cstdlib>\n#include <cstdio>')
 pos=s.index('// --- the two matrix');tap=r'''
// Passive small-image readback only. Does not change any source/output buffer.
static void synthetic_stage_tap(gpu::Gpu* gpu,const Image& image,const char* stage) {
 const char* root=std::getenv("SPK_SYNTHETIC_STAGE_DIR");if(!root||!image.valid())return;
 static unsigned serial=0;std::vector<float> data(image.elements());std::string error;
 if(!gpu->read(image.buf.get(),0,data.data(),image.bytes(),error))throw std::runtime_error("Diagnostic tap read failed");
 char name[256];std::snprintf(name,sizeof name,"%04u-%s-%u-%u-%u.f32",serial++,stage,image.h,image.w,image.c);
 std::ofstream out(std::filesystem::path(root)/name,std::ios::binary);out.write((const char*)data.data(),data.size()*4);if(!out)throw std::runtime_error("Diagnostic tap write failed");
}
''';s=s[:pos]+tap+s[pos:]
 for segment,table,count in [('film_segment','kFilmStages','kFilmStageCount'),('print_segment','kPrintStages','kPrintStageCount')]:
  begin=s.index('bool Pipeline::'+segment);end=s.index('\n}',begin);b=s[begin:end];b=b.replace('        cur = next;',f'        synthetic_stage_tap(gpu_,next.cur,{table}[i].name);\n        cur = next;');s=s[:begin]+b+s[end:]
 f.write_text(s)
# Preserve the base natural source whitelist verbatim; only the NEW entry admits synthetic identity.
check=(root/'engine/src/pipeline/engine.cpp').read_text();first=check[check.index('extern "C" __declspec(dllexport) int spk_stream_research_v1'):check.index('// NaturalPhotographyStreamV1:')];
if 'b7d358517db0cd5b5c54215aa089a5a30c79234aef50e9381931128493e33401' not in first:raise ValueError('Natural whitelist accidentally modified')
report={'version':'NaturalPhotographyGeneratedSourceV1','trace':a.trace,'syntheticSourceIdentity':syntheticSource,'fixtureManifestSha256':hashlib.sha256((a.fixtures/'manifest.json').read_bytes()).hexdigest(),'naturalGuardPreserved':True,'sourceHashes':{rel:hashlib.sha256((root/rel).read_bytes()).hexdigest() for rel in files}};Path(str(root)+'.generation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))

