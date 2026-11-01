"""Offline contract checks of frozen stage outputs, no new GPU calls."""
import unittest,sys,json,hashlib,subprocess
from pathlib import Path
import numpy as np
repo=Path(__file__).resolve().parents[2];e=repo/'docs/research/color-engine-20261029/home-round13';root=Path('N:/pixart/ColorResearchHome-20261030/round13-unsharp-v1')
def read(p):return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
class FrozenAB(unittest.TestCase):
 @classmethod
 def setUpClass(cls):cls.n=read(e/'candidate-natural.json');cls.s=read(e/'candidate-synthetic.json');cls.r=read(e/'reference-numerical.json');cls.review=read(e/'ab-review.json')
 def test_unique_candidate_off_and_recipe_binding(self):
  d=read(e/'candidate-definition.json');p=read(e/'evaluation-plan.json');self.assertEqual(d['version'],'SharedIncrementEnvelopeUnsharpV1');self.assertFalse(d['enabledByDefault']);self.assertEqual(sha(e/'candidate-definition.json'),p['candidateDefinitionSha256']);self.assertEqual(self.n['definitionSha256'],p['candidateDefinitionSha256'])
 def test_reference_exact_bodies_builds(self):
  r=read(e/'reference-build-v2.json');self.assertEqual([v['exitCode'] for v in r['results']],[0,0]);self.assertEqual(len(self.r['records']),45);self.assertTrue(self.r['allFinite']);self.assertTrue(self.r['a_b_passThrough'])
 def test_reference_readback_hashes(self):
  for v in self.r['records']:
   command=v['command'];self.assertEqual(sha(command[2]),v['inputSha256']);self.assertEqual(sha(command[3]),v['outputSha256'])
 def test_default_old_resources_unchanged(self):
  b=read(e/'candidate-build-v3.json')
  for name,digest in b['originalResourceHashes'].items():self.assertEqual(sha(root/'candidate-build-v3/resources'/name),digest)
 def test_whole_stream_data_exact_not_report_only(self):
  for mode,batch in [('synthetic',self.s),('natural',self.n)]:
   for rec in batch['records']:
    whole=root/('candidate-'+mode)/(rec['id']+'-whole')
    for p in rec['parity']:
     other=root/('candidate-'+mode)/(rec['id']+'-r'+str(p['rows']));self.assertEqual((whole/p['file']).read_bytes(),(other/p['file']).read_bytes())
 def test_frozen_legacy_matches_saved(self):
  finite=read(repo/'docs/research/color-engine-20261029/home-round11/natural-stage-finite.json')
  for rec in self.n['records']:
   case=next(c for c in finite['cases'] if c['id']==rec['id']);whole=root/'candidate-natural'/(rec['id']+'-whole')
   for stage,file in [('print_scan_finish','legacy.f32'),('print_output','legacy-encoded.f32')]:self.assertEqual(sha(whole/file),next(s for s in case['stages'] if s['stage']==stage)['sha256'])
   old=root.parent/'round11/natural-coverage-run1'/rec['id']/'whole.rgba16';self.assertEqual(sha(old),sha(whole/'legacy.rgba16'))
 def test_candidate_global_envelope_real_data(self):
  for mode,batch in [('synthetic',self.s),('natural',self.n)]:
   for rec in batch['records']:
    shape=rec['shape'];source=root/('candidate-'+mode)/(rec['id']+'-input.f32') if mode=='synthetic' else next((root.parent/'round11/natural-stage-finite-v1'/rec['id']).glob('*-print_linear-*.f32'));v=np.fromfile(source,dtype='<f4').reshape(shape);b=np.fromfile(root/('candidate-'+mode)/(rec['id']+'-whole/candidate.f32'),dtype='<f4').reshape(shape);pad=np.pad(v,((2,2),(2,2),(0,0)),mode='symmetric');lo=v.copy();hi=v.copy();h,w,_=shape
    for y in range(5):
     for x in range(5):lo=np.minimum(lo,pad[y:y+h,x:x+w]);hi=np.maximum(hi,pad[y:y+h,x:x+w])
    self.assertTrue(np.isfinite(b).all());self.assertTrue(((b>=lo)&(b<=hi)).all())
 def test_negative_above_one_not_globally_clamped(self):
  rec=next(r for r in self.s['records'] if r['id']=='negative_above_range');b=np.fromfile(root/'candidate-synthetic/negative_above_range-whole/candidate.f32',dtype='<f4');self.assertLess(float(b.min()),0);self.assertGreater(float(b.max()),1);self.assertGreater(rec['response']['candidateNegativeComponents'],0)
 def test_flat_identity_and_real_detail_not_turned_off_globally(self):
  flat=next(r for r in self.s['records'] if r['id']=='flat');self.assertEqual(flat['response']['modifiedPixels'],0);noise=next(r for r in self.s['records'] if r['id']=='noise');self.assertGreater(noise['response']['linearYStdCandidate'],noise['response']['linearYStdOriginal']);self.assertGreater(next(r for r in self.s['records'] if r['id']=='detail')['response']['linearYStdCandidate'],next(r for r in self.s['records'] if r['id']=='detail')['response']['linearYStdOriginal'])
 def test_pure_step_impulse_tradeoff_retained(self):
  for name in ['step','impulse']:
   rec=next(r for r in self.s['records'] if r['id']==name);self.assertEqual(rec['response']['candidateEqualsOriginalPixels'],rec['shape'][0]*rec['shape'][1])
 def test_nex5r_reduced_zeros_not_only_acceptance(self):
  n=next(r for r in self.n['records'] if r['id']=='sonynex5r');self.assertEqual(n['response']['legacyAnyZeroPixels'],11563);self.assertEqual(n['response']['candidateAnyZeroPixels'],0);self.assertEqual(self.review['productAcceptance'],'NOT_ADMITTED');self.assertEqual(self.review['independentValidation'],'NOT_RUN_DEVELOPMENT_ONLY')
 def test_lifetime_all_calls(self):
  for batch in [self.s,self.n]:
   for rec in batch['records']:
    for run in rec['runs']:self.assertEqual(run['deviceLiveAfter'],0);self.assertEqual(run['persistentAfter'],0)
 def test_saved_reopen_and_tiff16_actual(self):
  import tifffile
  for rec in self.n['records']:
   name=rec['id'];shape=rec['shape'];whole=root/'candidate-natural'/(name+'-whole');b=np.fromfile(whole/'candidate.f32',dtype='<f4').reshape(shape);np.testing.assert_array_equal(np.load(root/'review-v1'/(name+'-candidate-reopen.npy')),b);q=np.fromfile(whole/'candidate.rgba16',dtype='<u2').reshape(shape[:2]+[4]);np.testing.assert_array_equal(tifffile.imread(root/'review-v1'/(name+'-candidate.tif')),q[:,:,:3])
 def test_default_off_gate_before_gpu(self):
  command=[sys.executable,'-B','-c',"import sys,runpy;sys.path.insert(0,'N:/pixart/ColorResearchHome-20261030/deps/python-frozen');sys.argv=sys.argv[1:];runpy.run_path(sys.argv[0],run_name='__main__')",str(repo/'tools/color-round13/run_candidate_ab.py'),str(root),'natural'];r=subprocess.run(command,capture_output=True,text=True);self.assertNotEqual(r.returncode,0);self.assertIn('default-off',r.stderr)
 def test_scale_and_company_not_approved(self):
  self.assertIn('ratio1',self.review['scale']);ad=read(e/'r6-admission.json');self.assertTrue(ad['doesNotChangePixels']);self.assertEqual(ad['candidateCountThisBatch'],1)
if __name__=='__main__':unittest.main()
