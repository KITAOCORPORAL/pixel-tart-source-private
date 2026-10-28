"""Prepare auditable paired patch manifest from explicitly aligned 8-bit RGB scans.
No histogram matching or per-photo artistic grading; manual correspondences only.
"""
import argparse,pathlib,json
import offline as o
import numpy as np
from scipy.ndimage import map_coordinates
from PIL import Image

def homography(src,dst):
 src=np.asarray(src,float);dst=np.asarray(dst,float)
 if src.shape!=dst.shape or src.ndim!=2 or src.shape[1]!=2 or len(src)<4:raise ValueError('>=4 matching non-collinear points required')
 A=[];b=[]
 for (x,y),(u,v) in zip(src,dst):A.extend([[x,y,1,0,0,0,-u*x,-u*y],[0,0,0,x,y,1,-v*x,-v*y]]);b.extend([u,v])
 h,_,rank,_=np.linalg.lstsq(A,b,rcond=None)
 if rank<8:raise ValueError('degenerate point geometry')
 H=np.r_[h,1].reshape(3,3);q=np.c_[src,np.ones(len(src))]@H.T;q=q[:,:2]/q[:,2:];error=np.linalg.norm(q-dst,axis=1)
 return H,float(error.max())

def prepare(config,out):
 config=pathlib.Path(config);meta=json.loads(config.read_text(encoding='utf-8-sig'));records=[]
 for rec in meta['records']:
  r=dict(rec);source=(config.parent/r['reference_path']).resolve();scan=(config.parent/r['scan_path']).resolve()
  x=o.load_srgb(source,r.get('reference_assume_srgb',False));y=o.load_srgb(scan,r.get('scan_assume_srgb',False))
  H,err=homography(r['alignment']['reference_points'],r['alignment']['scan_points'])
  if err>r['alignment']['max_reprojection_pixels']:raise ValueError('registration exceeds predeclared limit')
  # Extra independently chosen check points prevent relying on the four fitted points.
  checks=r['alignment'].get('check_points',[])
  if len(checks)<2:raise ValueError('>=2 independent alignment check points required')
  for c in checks:
   q=H@np.r_[c['reference'],1];q=q[:2]/q[2]
   if np.linalg.norm(q-c['scan'])>r['alignment']['max_reprojection_pixels']:raise ValueError('independent registration check failed')
  # Scan-coordinate binary admission mask: white = valid, excludes border/reflections/scratches.
  mask_path=(config.parent/r['scan_valid_mask']).resolve();mask=np.array(Image.open(mask_path).convert('L'))>127
  if mask.shape!=y.shape[:2]:raise ValueError('valid mask dimensions mismatch')
  normalization=r['normalization']
  if normalization['method']!='exposure-wb-only-predeclared':raise ValueError('normalization policy')
  gain=np.asarray(normalization['wb_rgb'],float)*2**float(normalization['exposure_ev'])
  if gain.shape!=(3,) or np.any(gain<=0) or not np.isfinite(gain).all():raise ValueError('invalid predeclared gain')
  normalized=o.encode(o.decode(x)*gain)
  patches=[]
  for patch in r['sample_patches']:
   cx,cy=patch['center_reference_xy'];radius=int(patch['radius_pixels']);yy,xx=np.mgrid[int(cy)-radius:int(cy)+radius+1,int(cx)-radius:int(cx)+radius+1]
   if radius<1 or xx.min()<0 or yy.min()<0 or xx.max()>=x.shape[1] or yy.max()>=x.shape[0]:raise ValueError('patch outside source')
   coords=np.stack([xx.ravel(),yy.ravel(),np.ones(xx.size)],axis=1)@H.T;sx=coords[:,0]/coords[:,2];sy=coords[:,1]/coords[:,2]
   if not np.isfinite(coords).all() or sx.min()<0 or sy.min()<0 or sx.max()>=y.shape[1]-1 or sy.max()>=y.shape[0]-1:raise ValueError('patch outside registered scan')
   valid=map_coordinates(mask.astype(float),[sy,sx],order=0)>0.5
   if not valid.all():raise ValueError('patch overlaps excluded scan region; choose a different predeclared ROI')
   source_rgb=normalized[yy,xx].reshape(-1,3).mean(axis=0);scan_rgb=np.array([map_coordinates(y[...,c],[sy,sx],order=1).mean() for c in range(3)])
   patches.append({'input':source_rgb.tolist(),'target':scan_rgb.tolist(),'region':patch['region'],'valid':True,'sample_count':int(xx.size),'reference_center':[cx,cy]})
  r.update(reference_path=str(source),scan_path=str(scan),reference_sha256=o.digest(source),scan_sha256=o.digest(scan),valid_mask_sha256=o.digest(mask_path),registration={'method':'manual homography + >=2 independent check points','matrix':H.tolist(),'fit_max_reprojection_pixels':err,'max_allowed_pixels':r['alignment']['max_reprojection_pixels']},patches=patches)
  records.append(r)
 result={'data_type':meta.get('data_type','REAL_PAIRED_FILM_PLUS_FIXED_SCAN'),'domain':o.DOMAIN,'thresholds':meta['thresholds'],'records':records,'preparation_config_sha256':o.digest(config)}
 o.validate_manifest(result);o.dump(out,result)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('config');p.add_argument('output');a=p.parse_args();prepare(a.config,a.output)
