"""Targeted read-only analysis checks, not a repeated renderer acceptance suite."""
import csv,json,sys,tempfile,unittest
from pathlib import Path
import numpy as np
sys.path.insert(0,str(Path(__file__).resolve().parent))
from analyze_existing import reflect_index,fir,blob_arrays,metrics,read,sha,load_stages
from analyze_paper import curve,constants,paper_chain
REPO=Path(__file__).resolve().parents[2];ROOT=Path('N:/pixart/ColorResearchHome-20261030');LOCAL=ROOT/'round12-cause-v2';E=REPO/'docs/research/color-engine-20261029/home-round12'

class AnalyticSemantics(unittest.TestCase):
 def test_reflect_repeats_edge_not_mirror(self):
  self.assertEqual(reflect_index(np.arange(-3,7),4).tolist(),[2,1,0,0,1,2,3,3,2,1])
 def test_one_pixel_reflect(self):
  self.assertTrue((reflect_index(np.arange(-5,6),1)==0).all())
 def test_fir_uses_neighbors_in_both_axes(self):
  image=np.zeros((5,5,3),np.float32);image[2,2]=1;blur,w=fir(image)
  self.assertEqual(len(w),5);self.assertGreater(float(blur[1,2,0]),0);self.assertGreater(float(blur[2,1,0]),0);self.assertGreater(float(blur[0,0,0]),0)
 def test_fir_boundary_uses_global_reflection(self):
  image=np.zeros((3,3,3),np.float32);image[0,0]=1;actual,w=fir(image)
  expected=np.float32(np.float32(w[1]+w[2])*np.float32(w[1]+w[2]))
  self.assertEqual(float(actual[0,0,0]),float(expected))
 def test_endpoint_categories_disjoint(self):
  values=np.array([-1.,-1e-8,0.,1e-8,1./65535],np.float32);q=np.floor(np.clip(values,0,1)*np.float32(65535)+np.float32(.5)).astype(np.uint16);zero=q==0
  self.assertEqual([int((zero&(values<0)).sum()),int((zero&(values==0)).sum()),int((zero&(values>0)).sum())],[2,1,1]);self.assertEqual(int(q[-1]),1)
 def test_curve_constant_endpoints(self):
  k={'print/curve_x':np.repeat(np.array([-3.,0.,4.])[:,None],3,axis=1).ravel(),'print/curve_y':np.repeat(np.array([0.,1.,2.])[:,None],3,axis=1).ravel(),'print/curve_inv':np.repeat(np.array([1/3,1/4])[:,None],3,axis=1).ravel()};q=np.array([[[-5.,-3.,5.],[0.,-1.5,2.]]],np.float32);result=curve(q,k)
  np.testing.assert_array_equal(result,np.array([[[0,0,2],[1,.5,1.5]]],np.float32))
 def test_blob_rejects_wrong_version(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t)/'bad';p.write_bytes(b'BAD!'+(1).to_bytes(4,'little')+bytes(8))
   with self.assertRaises(ValueError):blob_arrays(p)

class SavedEvidence(unittest.TestCase):
 @classmethod
 def setUpClass(cls):
  cls.cause=read(E/'cause-analysis-v1.json');cls.paper=read(E/'paper-cause-v1.json');cls.finite=read(REPO/'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json')
 def test_clean_selection_partition_exact(self):
  c=self.cause['A']['classification']['cleanSelected11562'];self.assertEqual(c['pixelsWithAnyZero'],11562);self.assertEqual(c['zeroComponents'],22326);self.assertEqual(c['negativeClampedComponents']+c['exactFloatZeroComponents']+c['positiveRoundedToZeroComponents'],c['zeroComponents']);self.assertEqual([c['negativeClampedComponents'],c['exactFloatZeroComponents'],c['positiveRoundedToZeroComponents']],[22322,0,4])
 def test_coordinate_table_and_neighborhood_actual_bytes(self):
  with (LOCAL/'nex5r-zero-channels.csv').open() as handle:rows=list(csv.DictReader(handle))
  with np.load(LOCAL/'nex5r-selected-neighborhoods.npz') as archive:neighborhood={name:archive[name] for name in archive.files}
  s=load_stages(ROOT,self.finite,'sonynex5r');self.assertEqual(len(rows),22326);coords=neighborhood['coordinates_yxc'];self.assertEqual(len(set(map(tuple,coords))),22326);np.testing.assert_array_equal(neighborhood['scan_finish'],s['print_scan_finish'][tuple(coords.T)]);np.testing.assert_array_equal(neighborhood['encoded'],s['print_output'][tuple(coords.T)])
  for i in [0,len(rows)//2,len(rows)-1]:
   y,x,c=map(int,coords[i]);row=rows[i];self.assertEqual([int(row['x']),int(row['y']),int(row['channel'])],[x,y,c]);self.assertEqual(float(row['print_output']),float(s['print_output'][y,x,c]));ys=neighborhood['source_y'][i];xs=neighborhood['source_x'][i];np.testing.assert_array_equal(neighborhood['linear_5x5'][i],s['print_linear'][np.ix_(ys,xs,[c])][:,:,0])
 def test_u16_quantization_exact_all_nex5r_pixels(self):
  s=load_stages(ROOT,self.finite,'sonynex5r');shape=s['print_output'].shape;observed=np.fromfile(ROOT/'round11/natural-coverage-run1/sonynex5r/whole.rgba16',dtype='<u2').reshape(shape[:2]+(4,));pred=np.floor(np.clip(s['print_output'],0,1)*np.float32(65535)+np.float32(.5)).astype(np.uint16);np.testing.assert_array_equal(pred,observed[:,:,:3])
 def test_roi_fixed_and_no_zero_exposure(self):
  self.assertEqual(self.paper['roiXYWH'],[0,500,300,250]);a=self.cause['B']['densityZeroAttribution'];self.assertEqual(a['roiZeroComponents'],19567);self.assertEqual(a['roiBelowOrEqualLowerBoundComponents'],19567);self.assertEqual(a['roiZeroNotAtLowerBound'],0);self.assertEqual(a['roiFilmExposureZero'],0);self.assertEqual(a['roiFilmExposureNegative'],0)
 def test_no_cross_domain_rgb_clipping_claim(self):
  d=self.cause['domains'];self.assertIn('not RGB',d['film_log_and_curves']);self.assertIn('XYZ',d['print_spectral']);self.assertIn('linear sRGB',d['print_linear']);self.assertTrue(self.paper['displayYComparison']['notOptimizationTarget'])
 def test_hidden_defaults_bound_to_same_resolved(self):
  identity=read(E/'cpu-setup-identity.json');self.assertTrue(identity['resolvedMatchesFrozen']);self.assertEqual(sha(LOCAL/'cpu-constants.dat'),identity['dataSha256']);k=constants(LOCAL);self.assertEqual(k['hidden'][:5].tolist(),[0,.7,.7,0,0]);self.assertEqual(self.paper['fixedPublicResolvedCount'],99)
 def test_paper_not_out_of_table(self):
  self.assertEqual(self.paper['paperProfileDomain']['outOfLowComponents'],0);self.assertEqual(self.paper['paperProfileDomain']['outOfHighComponents'],0);self.assertGreater(self.paper['zeroFilmDensityChain']['relativeY'],0)
 def test_zero_gpu_and_raw_scope(self):
  self.assertEqual(self.cause['gpuDispatchCalls'],0);self.assertEqual(self.cause['rawDecodeCalls'],0);self.assertEqual(self.cause['full20MPCalls'],0);self.assertEqual(self.paper['gpuCalls'],0)
if __name__=='__main__':unittest.main()
