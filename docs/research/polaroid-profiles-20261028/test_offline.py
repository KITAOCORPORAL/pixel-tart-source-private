import pathlib,json,copy,subprocess,sys,os,shutil
import offline as o
import numpy as np
R=o.ROOT;results={}
def check(name,fn):
 try:fn();results[name]={'status':'PASS'}
 except Exception as e:results[name]={'status':'FAIL','error':str(e)}
def assert_(v,msg='assertion failed'):
 if not v:raise AssertionError(msg)
rng=np.random.default_rng(20261028);rgb=np.r_[rng.random((4096,3)),np.repeat(np.linspace(0,1,257)[:,None],3,axis=1),o.cube_grid(2).reshape(-1,3)]
o.dump(R/'results/reference-input.json',rgb.tolist())
dotnet=os.environ.get('KITAO_DOTNET') or (str(R.parents[1]/'.dotnet/dotnet.exe') if (R.parents[1]/'.dotnet/dotnet.exe').exists() else shutil.which('dotnet'))
subprocess.run([str(dotnet),str(R/'v3-reference/bin/Release/net10.0/Reference.dll'),str(R/'results/reference-input.json'),str(R/'results/reference-csharp.json')],check=True)
ref=json.loads((R/'results/reference-csharp.json').read_text(encoding='utf8'));diff={}
for k,bw in [('modern-color',False),('modern-bw',True)]:diff[k]=float(np.max(np.abs(np.array(ref[k])-o.v3_response(rgb,bw))))
o.dump(R/'results/existing-v3-csharp-parity.json',{'kind':'linked existing CandidateTone.cs; minimal recipe type adapter only; not full application','samples':len(rgb),'max_abs_error':diff,'status':'PASS' if max(diff.values())<2e-14 else 'FAIL'})
check('linked_Csharp_baseline_parity',lambda:assert_(max(diff.values())<2e-14))
check('strength_0_exact',lambda:assert_(np.array_equal(o.candidate_response(rgb,amount=0),rgb)))
check('all_neutrals_achromatic',lambda:assert_(np.ptp(o.candidate_response(np.repeat(np.linspace(0,1,4097)[:,None],3,axis=1)),axis=1).max()<1e-12))
check('nan_rejected',lambda:assert_(_reject(lambda:o.candidate_response([[np.nan,0,0]])))) if False else None
def reject(fn):
 try:fn()
 except (ValueError,KeyError):return True
 return False
check('nan_rejected',lambda:assert_(reject(lambda:o.candidate_response([[np.nan,0,0]]))))
check('wide_unconverted_rejected',lambda:assert_(reject(lambda:o.candidate_response([[-.1,1.5,0]]))))
from PIL import Image
Image.new('RGB',(2,2)).save(R/'results/untagged-test.png')
check('no_implicit_missing_icc',lambda:assert_(reject(lambda:o.load_srgb(R/'results/untagged-test.png'))))
check('strength_range_rejected',lambda:assert_(reject(lambda:o.candidate_response(rgb,amount=1.2))))
# Synthetic self-test only: different scene/batch IDs, never film data or accuracy evidence.
records=[];regions=['gray','skin','skin_highlight','highlight','red_orange','purple','blue_green','shadow','backlight']
for j,split in enumerate(['development','validation','heldout']):
 x=np.r_[np.repeat(np.linspace(0,1,33)[:,None],3,axis=1),rng.random((180,3))];y=o.candidate_response(x)
 patches=[{'input':a.tolist(),'target':b.tolist(),'region':'gray' if i<33 else regions[1+(i%8)],'valid':True} for i,(a,b) in enumerate(zip(x,y))]
 rec={k:'synthetic-only-'+str(j) for k in o.REQUIRED};rec.update(id=split,split=split,generation='SYNTHETIC_NOT_FILM',auto_enhancement=False,rights={'fit_allowed':True,'source':'first-party-generated-math-fixture'},normalization={'method':'exposure-wb-only-predeclared','exposure_ev':0,'wb_rgb':[1,1,1]},patches=patches)
 rec.update(paper='synthetic-common-paper',scanner_icc_sha256='synthetic-fixed-scanner');records.append(rec)
manifest={'data_type':'SYNTHETIC_TOOL_SELFTEST_NOT_PHOTOGRAPHIC_MEASUREMENT','thresholds':{reg:5 for reg in ['all',*regions]},'records':records}
o.dump(R/'results/synthetic-fixture.json',manifest)
check('manifest_valid_synthetic',lambda:o.validate_manifest(manifest))
for key in ['scene_id','batch_id','parent_capture_id','reference_sha256','scan_sha256']:
 bad=copy.deepcopy(manifest);bad['records'][1][key]=bad['records'][0][key]
 check('reject_leakage_'+key,lambda b=bad:assert_(reject(lambda:o.validate_manifest(b))))
bad=copy.deepcopy(manifest);bad['records'][1]['rights']['fit_allowed']=False
check('reject_uncleared_rights',lambda:assert_(reject(lambda:o.validate_manifest(bad))))
bad=copy.deepcopy(manifest);bad['records'][1]['auto_enhancement']=True
check('reject_auto_scan_enhancement',lambda:assert_(reject(lambda:o.validate_manifest(bad))))
check('fit_three_models_and_holdout',lambda:o.calibrate(R/'results/synthetic-fixture.json',R/'results/synthetic-fit'))
check('cube_roundtrip_exact_nodes',lambda:assert_(np.max(np.abs(o.cube_sample(o.cube_read(R/'profiles/polaroid-modern-color-style-smooth-1.cube'),o.cube_grid(33))-o.candidate_response(o.cube_grid(33))))<6e-11))
a=o.candidate_response(rgb);check('deterministic_repeat',lambda:assert_(np.array_equal(a,o.candidate_response(rgb))))
# Reopen profile and .cube, not a product project roundtrip.
idx=json.loads((R/'profiles/profile-index.json').read_text(encoding='utf8'))
check('frozen_profile_hashes',lambda:assert_(all(o.digest(R/x['path'])==x['model_sha256'] and o.digest(R/x['cube'])==x['cube_sha256'] for x in idx)))
o.dump(R/'results/tool-tests.json',{'scope':'offline tool only; no desktop/device acceptance','checks':results,'pass':sum(v['status']=='PASS' for v in results.values()),'fail':sum(v['status']=='FAIL' for v in results.values())})
print(json.dumps(results,indent=2));sys.exit(any(v['status']=='FAIL' for v in results.values()))

