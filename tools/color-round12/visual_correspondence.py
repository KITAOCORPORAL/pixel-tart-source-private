"""Saved-pixel correspondence only; preserves neighborhoods and original ROI."""
from pathlib import Path
import sys,json,csv
import numpy as np
from PIL import Image,ImageDraw
sys.path.insert(0,str(Path(__file__).resolve().parent))
from analyze_existing import read,need,sha,dump,load_stages,reflect_index,fir
root,out,summary=map(Path,sys.argv[1:4]);repo=Path(__file__).resolve().parents[2];finite=read(repo/'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json');s=load_stages(root,finite,'sonynex5r');h,w,_=s['print_output'].shape;selected=np.load(out/'nex5r-selected-zero-pixels.npy');rgba=np.fromfile(root/'round11/natural-coverage-run1/sonynex5r/whole.rgba16',dtype='<u2').reshape(h,w,4);q=rgba[:,:,:3];after_path=root/'round11/natural-coverage-run1/sonynex5r/after.png';img=np.asarray(Image.open(after_path).convert('RGB'));need(np.array_equal(img,np.rint(q/257).astype(np.uint8)),'Displayed after image not bound to saved output');coords=np.argwhere((q==0)&selected[:,:,None]);ys=reflect_index(coords[:,0,None]+np.arange(-2,3),h);xs=reflect_index(coords[:,1,None]+np.arange(-2,3),w);patches=s['print_linear'][ys[:,:,None],xs[:,None,:],coords[:,2,None,None]];np.savez_compressed(out/'nex5r-selected-neighborhoods.npz',coordinates_yxc=coords,source_y=ys,source_x=xs,linear_5x5=patches,scan_finish=s['print_scan_finish'][tuple(coords.T)],encoded=s['print_output'][tuple(coords.T)],quantized=q[tuple(coords.T)])
value=s['print_output'][tuple(coords.T)];records=[]
for cause,mask in [('negative_clamp',value<0),('positive_quantization',value>0)]:
 indexes=np.flatnonzero(mask)
 if not len(indexes):continue
 # descriptive first and most-negative, not a new evaluation sample or tuned ROI
 picks=list(dict.fromkeys([int(indexes[0]),int(indexes[np.argmin(value[indexes])])]))
 for i in picks:
  y,x,c=map(int,coords[i]);record={'cause':cause,'xyc':[x,y,c],'linearRGB':s['print_linear'][y,x].tolist(),'scanFinishRGB':s['print_scan_finish'][y,x].tolist(),'encodedRGB':s['print_output'][y,x].tolist(),'rgba16':rgba[y,x].tolist(),'sourceY':ys[i].tolist(),'sourceX':xs[i].tolist(),'neighborhood5x5':patches[i].tolist()};records.append(record)
dump(out/'nex5r-neighborhood-examples.json',records)
# Fixed flower ROI plus a single declared magnification of the previously established region.
x,y,bw,bh=270,400,300,250;tile=Image.new('RGB',(920,300),'#222');draw=ImageDraw.Draw(tile);original=Image.open(root/'round11/natural-coverage-run1/sonynex5r/before.png').convert('RGB').crop((x,y,x+bw,y+bh));current=Image.fromarray(img[y:y+bh,x:x+bw]);overlay=np.array(current);overlay[selected[y:y+bh,x:x+bw]]=[255,0,255]
for label,im,xx in [('Original production display',original,0),('Frozen PortraA output',current,310),('Same output + zero-channel mask',Image.fromarray(overlay),620)]:draw.text((xx+5,6),label,fill='white');tile.paste(im,(xx,30))
tile.save(out/'nex5r-fixed-correspondence.png')
# Dark points versus zero-channel mask in same already-reviewed coordinates.
mask=selected[y:y+bh,x:x+bw];zero3=(q[y:y+bh,x:x+bw]==0).all(2);pre=s['print_linear'][y:y+bh,x:x+bw];post=s['print_scan_finish'][y:y+bh,x:x+bw];minlocal=patches.min((1,2));center=patches[:,2,2];finish=s['print_scan_finish'][tuple(coords.T)];statistics={'version':'Round12PixelVisualCorrespondenceV1','imageHash':sha(after_path),'imageBoundToU16Rounding':True,'fixedFlowerRoi':[x,y,bw,bh],'selectedPixelsInRoi':int(mask.sum()),'rgbBlackPixelsInRoi':int(zero3.sum()),'preSharpenMinAtFinalBlackPixels':pre[zero3].min(0).tolist(),'preSharpenMaxAtFinalBlackPixels':pre[zero3].max(0).tolist(),'localNeighborhoodFileHash':sha(out/'nex5r-selected-neighborhoods.npz'),'allSelectedZeroComponents':len(coords),'finishBelowAllInputNeighbors':int((finish<minlocal).sum()),'centerBelowNeighborhoodMean':int((center<patches.mean((1,2))).sum()),'interpretation':'Observed zero masks coincide with dark-side high-contrast flower/leaf edges; black appearance needs three zero channels, not one. This is occurrence attribution, not automatic aesthetic/error verdict.'}
# ROI density-zero channels can overlap already-clipped production footprint; report rather than hide.
r=load_stages(root,finite,'canonr6');prior=np.load(root/'round11/natural-coverage-run1/canonr6/proxy-prior-endpoint-support.npy');z=r['film_log_and_curves'][500:750,:300]==0;support=prior[500:750,:300];statistics['r6PriorLossOverlap']={'roiPixels':75000,'priorEndpointFootprintPixels':int(support.sum()),'zeroDensityComponentsWithPriorFootprint':int((z&support[:,:,None]).sum()),'zeroDensityComponentsWithoutPriorFootprint':int((z&~support[:,:,None]).sum()),'allZeroDensityPixelsWithPriorFootprint':int((z.all(2)&support).sum()),'allZeroDensityPixelsWithoutPriorFootprint':int((z.all(2)&~support).sum()),'interpretation':'Saved proxy values are distinct even where master footprint includes endpoints. Loss before saved proxy is not reconstructed or ruled out.'}
dump(summary,statistics);print(json.dumps(statistics,indent=2))
