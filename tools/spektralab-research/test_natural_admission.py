"""Focused failure controls. Engine factory must never run for rejected first inputs."""
import copy,json,tempfile,unittest,types,sys,os,struct
from pathlib import Path
import numpy as np
from natural_admission import *
from natural_case import evaluate
class AdmissionTests(unittest.TestCase):
 def setUp(self):
  self.t=tempfile.TemporaryDirectory();self.root=Path(self.t.name);self.source=Path('D:/AI AGENT/tmp/color-engine-company-20261030/source2');self.input=self.root/'A.npy';np.save(self.input,np.zeros((3,4,3),'<f4'));self.lib=self.root/'backend.dll';self.lib.write_bytes(b'fixed');self.res=self.root/'resources';self.res.mkdir();(self.res/'one').write_bytes(b'one');info=npy_info(self.input);stats=scan(self.input,info);self.entry={'path':'A.npy','fileSha256':sha(self.input),'shape':info['shape'],'route':'A','orientation':1,'domain':DOMAINS['A'],'bridgeVersion':'ExistingResearchD65ToD50V1','pixelSha256':stats['pixelSha256'],'bytes':info['fileBytes']};self.manifest=self.root/'manifest.json';self.m={'version':'NaturalAdmissionManifestV2','sourceSha256':'1'*64,'productionReportSha256':'2'*64,'orientation':1,'inputRoot':str(self.root),'bridge':{'version':'ExistingResearchD65ToD50V1','extraWB':False,'displayMappingOnA':False},'backend':{'sha256':sha(self.lib)},'resources':resources(self.res),'files':[self.entry],'preparationIdentity':{'sourceSha256':'1'*64}};self.m['bridge'].update({k:np.eye(3).tolist() for k in ['P','sRGBtoXYZD65','Bradford','engineProPhotoD50toXYZ','combinedMatrix']});self.m['bridge'].update(sourceWhiteXY=[.3127,.329],destinationWhiteXY=[.3457,.3585]);self.calls=0
 def tearDown(self):self.t.cleanup()
 def write(self):self.manifest.write_text(json.dumps(self.m));return sha(self.manifest)
 def reject(self,fragment,route='A',budget=True):
  digest=self.write();a=types.SimpleNamespace(output=self.root/'new',output_root=self.root,input=self.input,mode='case',route=route,manifest=self.manifest,manifest_sha=digest,library=self.lib,resources=self.res,source=self.source,striped=False,rows=7,film='kodak_portra_400',paper='kodak_portra_endura',strip_budget=0)
  def forbidden(**kw):self.calls+=1;raise AssertionError('engine called')
  r=evaluate(a,forbidden,None,{'admitted':budget});self.assertFalse(r['success']);self.assertFalse(r['engineCreated']);self.assertFalse(r['uploaded']);self.assertEqual(self.calls,0);self.assertIn(fragment,r['primaryError']['message']);self.assertTrue((a.output/'report.json').exists());self.assertFalse((a.output/'rgba16.bin').exists())
 def test_modified_input(self):a=np.load(self.input);a[0,0,0]=1;np.save(self.input,a);self.reject('modified')
 def test_ab_swap(self):self.entry['route']='B';self.entry['domain']=DOMAINS['B'];self.reject('swap')
 def test_wrong_size(self):np.save(self.input,np.zeros((4,4,3),'<f4'));self.reject('dimensions')
 def test_wrong_channels(self):np.save(self.input,np.zeros((3,4,4),'<f4'));self.reject('channels')
 def test_wrong_dtype(self):np.save(self.input,np.zeros((3,4,3),'<f8'));self.reject('float32')
 def test_nonfinite(self):a=np.zeros((3,4,3),'<f4');a[1,1,0]=np.nan;np.save(self.input,a);self.entry['fileSha256']=sha(self.input);self.reject('Nonfinite')
 def test_missing_manifest_field(self):del self.m['sourceSha256'];self.reject('Missing')
 def test_duplicate_entry(self):self.m['files'].append(copy.deepcopy(self.entry));self.reject('Duplicate')
 def test_unknown_route(self):self.reject('Unknown route',route='C')
 def test_resource_changed(self):(self.res/'one').write_bytes(b'changed');self.reject('Resource')
 def test_budget_refused(self):self.reject('GPU budget refused',budget=False)
 def test_truncated_file(self):self.input.write_bytes(self.input.read_bytes()[:-4]);self.reject('length')
 def test_duplicate_json_key(self):self.assertRaises(ValueError,lambda:json.loads('{"a":1,"a":2}',object_pairs_hook=pairs))
 def test_missing_bridge(self):del self.m['bridge']['P'];self.reject('Missing bridge')
 def test_duplicate_resource(self):self.m['resources'].append(copy.deepcopy(self.m['resources'][0]));self.reject('Duplicate resource')
 def test_manifest_seal_mismatch(self):
  d=self.write();self.assertRaisesRegex(ValueError,'manifest hash',validate,self.manifest,'0'*64,self.input,'A',self.lib,self.res,8*1024**3,{'admitted':True})
 def test_output_source_protection(self):self.assertRaises(ValueError,output_path,SOURCE_ROOT/'new')
 def test_cpu_budget(self):d=self.write();self.assertRaisesRegex(ValueError,'CPU',validate,self.manifest,d,self.input,'A',self.lib,self.res,100,{'admitted':True})
 def test_block_size(self):np.save(self.input,np.zeros((133,12,3),'<f4'));i=npy_info(self.input);s=scan(self.input,i,7);self.assertLessEqual(s['maximumFiniteBooleanBytes'],7*12*3)
 def test_open_exception_and_cleanup_error(self):
  digest=self.write();a=types.SimpleNamespace(output=self.root/'failed',output_root=self.root,input=self.input,mode='case',route='A',manifest=self.manifest,manifest_sha=digest,library=self.lib,resources=self.res,source=self.source,striped=False,rows=7,film='kodak_portra_400',paper='kodak_portra_endura',strip_budget=0)
  class Failing:
   def memory_report(self):return {}
   def open(self,*args):raise RuntimeError('original open failure')
   def close(self):raise IOError('secondary cleanup failure')
  result=evaluate(a,lambda **kw:Failing(),None,{'admitted':True});self.assertFalse(result['success']);self.assertIn('original open failure',result['primaryError']['message']);self.assertIn('secondary cleanup failure',result['cleanupErrors'][0]['error']);self.assertTrue((a.output/'report.json').exists())
 def test_cancel_waits_render_before_release(self):
  import time,threading
  from natural_case import render_joined
  calls=[]
  class Result:
   def close(self):calls.append('result-free')
  def render(session,tier):time.sleep(.03);calls.append('render-done');return Result()
  r=render_joined(render,None,None);self.assertEqual(calls,['render-done']);r.close();self.assertEqual(calls,['render-done','result-free'])
 def test_interrupt_joins_then_frees(self):
  import threading,time,unittest.mock as mock
  from natural_case import render_joined
  events=[];real_join=threading.Thread.join;fired=[False]
  def interrupted_join(t,timeout=None):
   if not fired[0]:fired[0]=True;raise KeyboardInterrupt('injected cancel')
   return real_join(t,timeout)
  class Cancel:
   argtypes=None
   def __call__(self,*args):events.append('cancel-request')
  engine=types.SimpleNamespace(_lib=types.SimpleNamespace(spk_cancel=Cancel()));session=types.SimpleNamespace(_handle=123)
  class Result:
   def close(self):events.append('result-free')
  def render(*args):time.sleep(.02);events.append('render-done');return Result()
  with mock.patch.object(threading.Thread,'join',interrupted_join):
   with self.assertRaises(KeyboardInterrupt):render_joined(render,session,engine)
  self.assertEqual(events,['cancel-request','render-done','result-free'])
 def test_failure_report_error_preserves_primary(self):
  import unittest.mock as mock
  digest=self.write();a=types.SimpleNamespace(output=self.root/'report-fault',output_root=self.root,input=self.input,mode='case',route='A',manifest=self.manifest,manifest_sha=digest,library=self.lib,resources=self.res,source=self.source,striped=False,rows=7,film='kodak_portra_400',paper='kodak_portra_endura',strip_budget=0)
  class Failing:
   def memory_report(self):return {}
   def open(self,*args):raise RuntimeError('primary error retained')
   def close(self):pass
  with mock.patch('natural_case.atomic_json',side_effect=OSError('disk fault')):
   with self.assertRaisesRegex(RuntimeError,'primary error retained'):evaluate(a,lambda **kw:Failing(),None,{'admitted':True})
 def test_cleanup_preserves_primary(self):
  class Bad:
   def close(self):raise IOError('cleanup fault')
  from natural_case import cleanup
  self.assertEqual(len(cleanup([('result',Bad()),('session',Bad()),('engine',Bad())])),3)
if __name__=='__main__':unittest.main()
