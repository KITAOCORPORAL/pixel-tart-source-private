import offline as o,prepare_pairs as p,json,numpy as np,pathlib
from PIL import Image
r=o.ROOT;folder=r/'results/preparation-selftest';folder.mkdir(exist_ok=True);fixture=json.loads((r/'results/synthetic-fixture.json').read_text(encoding='utf8'));records=[];rng=np.random.default_rng(1)
for j,rec in enumerate(fixture['records']):
 x=np.zeros((128,128,3));patches=[]
 for i in range(64):
  row,col=divmod(i,8);c=np.repeat(i/31,3) if i<32 else rng.random(3);x[row*16:(row+1)*16,col*16:(col+1)*16]=c;patches.append({'center_reference_xy':[col*16+8,row*16+8],'radius_pixels':2,'region':'gray' if i<32 else 'skin'})
 o.save_rgb(folder/f'reference-{j}.png',x);o.save_rgb(folder/f'scan-{j}.png',o.candidate_response(x));Image.new('L',(128,128),255).save(folder/f'mask-{j}.png')
 z=dict(rec);z.pop('patches');z.update(reference_path=f'reference-{j}.png',scan_path=f'scan-{j}.png',scan_valid_mask=f'mask-{j}.png',sample_patches=patches,alignment={'reference_points':[[0,0],[127,0],[0,127],[127,127]],'scan_points':[[0,0],[127,0],[0,127],[127,127]],'check_points':[{'reference':[32,32],'scan':[32,32]},{'reference':[96,96],'scan':[96,96]}],'max_reprojection_pixels':.01});records.append(z)
config={'data_type':'SYNTHETIC_PREPARATION_TEST_NOT_REAL_FILM','thresholds':{'all':5},'records':records};o.dump(folder/'input.json',config);p.prepare(folder/'input.json',folder/'prepared.json');o.calibrate(folder/'prepared.json',folder/'fit')
H,e=p.homography([[0,0],[10,0],[0,10],[10,10]],[[3,4],[13,4],[3,14],[13,14]])
assert np.max(np.abs(H-np.array([[1,0,3],[0,1,4],[0,0,1]])))<1e-12
Image.new('L',(128,128),0).save(folder/'mask-0.png');rejected=False
try:p.prepare(folder/'input.json',folder/'should-not-exist.json')
except ValueError:rejected=True
assert rejected
Image.new('L',(128,128),255).save(folder/'mask-0.png')
o.dump(r/'results/preparation-tests.json',{'status':'PASS','checks':['ICC tagged image conversion','identity homography + independent check points','nonzero translation homography','valid ROI patch extraction','binary exclusion mask rejection','manifest generation','three low-complexity fits with heldout reporting'],'data':'SYNTHETIC ONLY; does not count as actual film pairing','real_pairs_test':'NOT_RUN'})
print('PREPARATION TOOL SELFTEST PASS')
