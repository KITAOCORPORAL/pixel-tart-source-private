"""BT-supported complete ABI path. Outputs and external resources remain local."""
import argparse,ctypes,hashlib,json,os,subprocess,sys,time
from pathlib import Path
import numpy as np
from run_matrix import sha,digest,require

def tree(root):return {str(p.relative_to(root)).replace('\\','/'):sha(p) for p in sorted(root.rglob('*')) if p.is_file()}
def bindings(source):
 sys.path[:0]=[str(source/'engine/tests'),str(source/'engine/tools')]
 from spk_ctypes import Engine
 from windows_lifecycle import OwnedResult
 return Engine,OwnedResult

def reopen(recipepath,output):
 import tifffile
 from PIL import ImageCms
 r=json.loads(recipepath.read_text());require(not output.exists(),'Reopen output exists');source=Path(r['source']);build=Path(r['library']);resources=Path(r['resources']);Engine,OwnedResult=bindings(source)
 require(sha(build)==r['librarySha256'],'Backend mismatch');require(tree(resources)==r['resourceHashes'],'Resources changed');require(sha(recipepath.parent/'input.npy')==r['inputFileSha256'],'Input changed');image=np.load(recipepath.parent/'input.npy');require(digest(image)==r['inputPixelSha256'] and list(image.shape)==r['inputShape'] and bool(np.isfinite(image).all()),'Input identity invalid')
 os.environ.pop('SPK_R5_DECODE_FILE',None)
 with Engine(library=build,resources=resources) as e:
  with e.open(image,r['resolved']) as session:
   require(session.get_params()==r['resolved'],'Reopened resolved parameters differ')
   with OwnedResult(session,'full') as result:out=result.pixels().copy()
 require(digest(out)==r['outputPixelSha256'],'Fresh-process full render differs');require(digest(tifffile.imread(recipepath.parent/'display-rgba16.tif'))==r['outputPixelSha256'],'Saved TIFF16 readback differs');output.mkdir()
 out.astype('<u2').tofile(output/'rgba16.bin');tifffile.imwrite(output/'display-rgba16.tif',out,photometric='rgb',extrasamples='unassalpha',iccprofile=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes());require(np.array_equal(tifffile.imread(output/'display-rgba16.tif'),out),'Reopen TIFF differs')
 answer={'version':'BtFreshProcessReopenV1','freshProcess':True,'resolvedExact':True,'inputIdentity':True,'renderExact':True,'savedAndReopenedTiff16Exact':True,'outputPixelSha256':digest(out),'width':out.shape[1],'height':out.shape[0]};(output/'report.json').write_text(json.dumps(answer,indent=2));print(json.dumps(answer))

def run(root,output,supplement=False):
 import shutil,tifffile
 from PIL import ImageCms
 require(subprocess.check_output(['git','-C',str(root/'source2'),'rev-parse','HEAD'],text=True).strip()=='c1447c6ab618fa4b1cb11fd2fe12826855007059','Wrong fixed source');require(not subprocess.check_output(['git','-C',str(root/'source2'),'status','--porcelain'],text=True).strip(),'Original source dirty');require(not output.exists(),'Output exists');output.mkdir();Engine,OwnedResult=bindings(root/'source2');profile=ImageCms.ImageCmsProfile(ImageCms.createProfile('sRGB')).tobytes();report={'version':'BtCompletePathV1','gpu':'GTX1650 driver572.70','sourceCommit':'c1447c6ab618fa4b1cb11fd2fe12826855007059','scope':'External research only; no product registration','cases':[],'variants':{}}
 variants={'original':(root/'build-vulkan/engine/spektrafilm_engine.dll',root/'resources-original',None),'patched':(root/'build-patch/engine/spektrafilm_engine.dll',root/'resources-patch',None),'original-os':(root/'build-vulkan/engine/spektrafilm_engine.dll',root/'resources-original',root/'original-os-frozen.spv'),'patched-os':(root/'build-patch/engine/spektrafilm_engine.dll',root/'resources-patch',root/'patched-os-frozen.spv')}
 bp=np.array(0x3da66555,np.uint32).view(np.float32);neighbors=np.arange(0x3da6654f,0x3da6655c,dtype=np.uint32).view(np.float32)
 values=np.concatenate([[-.25,0,.018,.081],neighbors,[.18,.5,1,2]]).astype(np.float32);join=np.broadcast_to(values[None,:,None],(7,len(values),3)).copy();gradient=np.broadcast_to(np.linspace(-.01,1.2,257,dtype=np.float32)[None,:,None],(9,257,3)).copy();gradient[:,128,:]=np.array([neighbors[5],neighbors[4],neighbors[7]],np.float32)
 shapes={'join':join,'gradient':gradient}
 if supplement:
  # Same join content at a different height; mixed primaries including negative/>1.
  saturated=np.array([[0,0,0],[1,0,0],[0,1,0],[0,0,1],[1,1,0],[0,1,1],[1,0,1],[2,-.1,.18],[-.25,2,.5],[neighbors[5],0,1],[1,neighbors[5],0],[0,1,neighbors[5]]],np.float32)
  shapes={'joinheight1':join[:1].copy(),'saturated':np.broadcast_to(saturated[None,:,:],(3,len(saturated),3)).copy()}
 pairs=[('kodak_portra_400','kodak_portra_endura'),('kodak_ektar_100','kodak_ultra_endura')]
 for label,(library,base,shader) in variants.items():
  res=output/('resources-'+label);shutil.copytree(base,res)
  if shader:shutil.copyfile(shader,res/'vulkan/spk_cctf_decode.spv')
  report['variants'][label]={'librarySha256':sha(library),'resourceHashes':tree(res),'decodeShaderSha256':sha(res/'vulkan/spk_cctf_decode.spv'),'compilerFlags':'-V --target-env vulkan1.0'+(' -Os' if shader else '')}
  with Engine(library=library,resources=res) as engine:
   for space in ['ITU-R BT.709','ITU-R BT.2020']:
    for name,image in shapes.items():
     for film,paper in pairs:
      key=space[-3:]+'-'+name+'-'+film;dest=output/label/key;dest.mkdir(parents=True);params={'film_stock':film,'print_stock':paper,'grain_active':False,'glare_active':False,'halation_active':False,'dir_couplers_active':False,'auto_exposure':False,'input_color_space':space,'input_cctf_decoding':True,'output_color_space':'sRGB','output_cctf_encoding':True,'exposure_compensation_ev':0,'density_curve_gamma':1}
      start=time.perf_counter()
      with engine.open(image,params) as session:
       resolved=session.get_params()
       for k,v in params.items():require(resolved[k]==v,'Requested parameter not applied '+k)
       with OwnedResult(session,'full') as result:out=result.pixels().copy();stride=int(result.result.row_stride_px)
      np.save(dest/'input.npy',image);out.astype('<u2').tofile(dest/'rgba16.bin');tifffile.imwrite(dest/'display-rgba16.tif',out,photometric='rgb',extrasamples='unassalpha',iccprofile=profile);require(np.array_equal(tifffile.imread(dest/'display-rgba16.tif'),out),'TIFF roundtrip')
      r={'version':'BtFullEngineSavedRecipeV1','source':str(root/'source2'),'library':str(library),'librarySha256':sha(library),'resources':str(res),'resourceHashes':tree(res),'inputFileSha256':sha(dest/'input.npy'),'inputPixelSha256':digest(image),'inputShape':list(image.shape),'resolved':resolved,'outputPixelSha256':digest(out),'outputShape':list(out.shape),'rowStridePixels':stride,'rgba16FileSha256':sha(dest/'rgba16.bin'),'tiff16FileSha256':sha(dest/'display-rgba16.tif')};(dest/'recipe.json').write_text(json.dumps(r,indent=2))
      child=subprocess.run([sys.executable,str(Path(__file__).resolve()),'reopen',str(dest/'recipe.json'),str(dest/'reopen')],capture_output=True,text=True);require(child.returncode==0,child.stderr);entry={'variant':label,'case':key,'space':space,'input':name,'film':film,'resolved':resolved,'inputPixelSha256':r['inputPixelSha256'],'outputPixelSha256':r['outputPixelSha256'],'shape':list(out.shape),'reopen':json.loads(child.stdout),'wallMsIncludingReopen':(time.perf_counter()-start)*1000};report['cases'].append(entry)
 comparisons=[]
 for key in [x['case'] for x in report['cases'] if x['variant']=='original']:
  image=np.load(output/'original'/key/'input.npy');w,h=image.shape[1],image.shape[0]
  for left,right in [('original','patched'),('original-os','patched-os'),('original','original-os'),('patched','patched-os')]:
   l=np.fromfile(output/left/key/'rgba16.bin',dtype='<u2').reshape(h,w,4);r=np.fromfile(output/right/key/'rgba16.bin',dtype='<u2').reshape(h,w,4);delta=r.astype(np.int32)-l.astype(np.int32);mask=np.any(delta[:,:,:3]!=0,axis=2);ys,xs=np.nonzero(mask);values=np.unique(image[mask].view(np.uint32)) if mask.any() else [];comparisons.append({'case':key,'left':left,'right':right,'changedPixels':int(mask.sum()),'changedRgbChannels':int((delta[:,:,:3]!=0).sum()),'maxAbsU16':int(np.abs(delta).max()),'meanAbsRgbU16':float(np.abs(delta[:,:,:3]).mean()),'changedXColumns':np.unique(xs).tolist(),'changedYRows':np.unique(ys).tolist(),'changedInputChannelBits':[f'0x{int(v):08x}' for v in values],'bbox':None if not len(xs) else [int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())],'alphaChanged':bool((delta[:,:,3]!=0).any())})
 report['comparisons']=comparisons;(output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({'cases':len(report['cases']),'comparisons':[(c['case'],c['changedPixels'],c['maxAbsU16']) for c in comparisons if c['left']=='original-os']}))
if __name__=='__main__':
 if sys.argv[1]=='reopen':reopen(Path(sys.argv[2]),Path(sys.argv[3]))
 else:run(Path(sys.argv[1]),Path(sys.argv[2]),'--supplement' in sys.argv[3:])
