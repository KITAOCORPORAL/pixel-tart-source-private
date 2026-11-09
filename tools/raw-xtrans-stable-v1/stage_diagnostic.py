# Copy a pinned prepared observer into a NEW external diagnostic source, apply V1.
# No compiler/native/GPU calls; original source and old ledgers remain untouched.
from pathlib import Path,PurePosixPath
import sys,json,hashlib,subprocess,tarfile,io,argparse
from prepare_candidate import change,VERSION
COMMIT='cccb97647fcee56801fa68231fa8a38aa8b52ef7'
HEADER='ce42b2efa23806da4f1edabbad4901c1a836ff319133507bac6e4315b0d1f718'
def sha(b):return hashlib.sha256(b).hexdigest()
def need(c,m):
 if not c:raise ValueError(m)
def main():
 p=argparse.ArgumentParser();p.add_argument('--upstream',type=Path,required=True);p.add_argument('--reviewed-observer',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--enable-candidate',choices=[VERSION],required=True);a=p.parse_args();repo=Path(__file__).resolve().parents[2]
 need(not a.output.exists(),'New source destination required')
 output=a.output.resolve()
 for protected in [a.upstream.resolve(),a.reviewed_observer.resolve(),repo.resolve()]:need(output!=protected and protected not in output.parents,'Candidate destination overlaps protected source/researchroot')
 need(subprocess.check_output(['git','-C',str(a.upstream),'rev-parse','HEAD'],text=True).strip()==COMMIT,'Fixed upstream checkout required')
 baseline=json.loads((repo/'docs/research/raw-reopen-001-contract-20261104/h1-window-20261109/BASELINE_BINDING.json').read_text());modified={};headerdirs=set()
 for item in baseline['generatedSourceFiles']:
  rel=item['path'];data=(a.reviewed_observer/rel).read_bytes().replace(b'\r\n',b'\n');need(sha(data)==item['generatedSha256'],'Reviewed diagnostic source drift: '+rel);modified[rel]=data;headerdirs.add(str(Path(rel).parent).replace('\\','/'))
 headers={}
 for directory in headerdirs:
  name=directory+'/rr001_internal.h';data=(a.reviewed_observer/name).read_bytes().replace(b'\r\n',b'\n');need(sha(data)==HEADER,'Observer header drift');headers[name]=data
 old=modified['src/demosaic/xtrans_demosaic.cpp'];new,proof=change(old.decode());modified['src/demosaic/xtrans_demosaic.cpp']=new.encode()
 archive=subprocess.check_output(['git','-C',str(a.upstream),'archive',COMMIT]);tar=tarfile.open(fileobj=io.BytesIO(archive));entries=[]
 for member in tar.getmembers():
  name=PurePosixPath(member.name);need(not name.is_absolute() and '..' not in name.parts and not member.issym() and not member.islnk(),'Unsafe source archive entry');entries.append(member)
 a.output.mkdir(parents=True,exist_ok=False)
 for member in entries:
  if member.isfile():
   dest=a.output/member.name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(tar.extractfile(member).read())
 for name,data in {**modified,**headers}.items():dest=a.output/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
 files=[{'path':x.relative_to(a.output).as_posix(),'sha256':sha(x.read_bytes())} for x in sorted(a.output.rglob('*')) if x.is_file()]
 receipt={'version':VERSION,'status':'CANDIDATE_DIAGNOSTIC_SOURCE_ONLY_NOT_BUILT','sourceBase':COMMIT,'observerBase':'701306788438c1d295541479ea0b1359e7cc85d8','observerHeaderSha256':HEADER,'baselineObserverFilesMatched':len(modified),'candidateXtransSha256':sha(new.encode()),'candidateProof':proof,'files':files,'nativeBuild':0,'RAW':0,'productIntegration':False,'defaultOff':True,'budgetOldConsumedSlotsTouched':False,'warning':'Existing Build-Isolated originalByteRestoration guard is not applicable to intentional candidate; do not weaken it. Use candidate-only build in this new root.'}
 (a.output/'CANDIDATE_PREPARATION.json').write_text(json.dumps(receipt,indent=2)+'\n');print(json.dumps({'prepared':str(a.output),'candidateSha256':sha(new.encode()),'files':len(files),'nativeCalls':0}))
if __name__=='__main__':main()
