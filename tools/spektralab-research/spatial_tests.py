import argparse,ctypes,json,os,sys,time,subprocess,threading
from pathlib import Path
import numpy as np
from run_matrix import sha,digest,require,timed

def main():
 ap=argparse.ArgumentParser();ap.add_argument('checkout',type=Path);ap.add_argument('build',type=Path);ap.add_argument('resources',type=Path);ap.add_argument('output',type=Path);a=ap.parse_args();a.output.mkdir(exist_ok=False);sys.path.insert(0,str(a.checkout/'engine/tests'));sys.path.insert(0,str(a.checkout/'engine/tools'))
 from spk_ctypes import Engine,EngineError,SpkResult
 from windows_lifecycle import OwnedResult
 e=Engine(library=a.build/'engine/spektrafilm_engine.dll',resources=a.resources);base={'film_stock':'kodak_portra_400','print_stock':'kodak_portra_endura','grain_active':False,'glare_active':False,'halation_active':False,'auto_exposure':False,'input_color_space':'ProPhoto RGB','input_cctf_decoding':False,'output_color_space':'sRGB','output_cctf_encoding':True};report={'version':'SpektraSpatialV1','cases':[],'seed':'No fixed grain seed parameter in C ABI. Halation deterministic; stochastic grain cannot be exact across renders.'}
 def render(image,params):
  with e.open(image,{**base,**params}) as s:
   actual=s.get_params()
   for key,value in params.items():require(actual[key]==value,'Parameter mismatch '+key)
   with OwnedResult(s) as r:return r.pixels().copy(),r.info()
 def diff(x,y):
  d=x.astype(np.int32)-y.astype(np.int32);return {'changedPixels':int(np.any(d!=0,axis=2).sum()),'maxU16':int(np.abs(d).max()),'meanAbsU16':float(np.abs(d).mean())}
 for h,w in [(129,193),(31,17),(257,385)]:
  image=np.ones((h,w,3),np.float32)*.18;image[:max(1,h//8),:max(1,w//8)]=4;image[h//2:h//2+3,w//2:w//2+3]=8
  quiet,info=render(image,{});case={'size':[w,h],'quiet':info,'tests':{}};report['cases'].append(case)
  for name,params in [('halation_zero',{'halation_active':True,'halation_amount':0,'halation_scatter_amount':0}),('halation_one',{'halation_active':True,'halation_amount':1}),('halation_two',{'halation_active':True,'halation_amount':2}),('grain_zero',{'grain_active':True,'grain_amount':0}),('grain_one',{'grain_active':True,'grain_amount':1})]:
   x,info=render(image,params);y,_=render(image,params);case['tests'][name]={'resolvedDelta':params,'result':info,'vsQuiet':diff(x,quiet),'repeat':diff(x,y),'edgeMaxU16':int(np.abs(x[:4].astype(int)-quiet[:4].astype(int)).max())};
   if name.startswith('halation') or name=='grain_zero':require(np.array_equal(x,y),'Deterministic spatial repeated render differs')
   if name in ('halation_zero','grain_zero'):require(np.array_equal(x,quiet),'Zero strength not identity')
   if name=='halation_one':
    for rows in (1,7,17):
     stripe,_=render(image,{**params,'striped':True,'strip_rows':rows});d=diff(x,stripe);case['tests'][f'halation_stripe_{rows}']=d
  (a.output/'report.json').write_text(json.dumps(report,indent=2))
 # Invalid input and unimplemented parameter contracts: actual errors, not assumptions.
 errors={}
 for label,image,params in [('invalid_channels',np.zeros((3,3,2),np.float32),{}),('bad_film',np.zeros((3,3,3),np.float32),{'film_stock':'nonexistent'}),('unknown_seed',np.zeros((3,3,3),np.float32),{'grain_seed':20261029})]:
  try:
   s=e.open(image,{**base,**params});s.close();errors[label]='UNEXPECTED_ACCEPTANCE'
  except Exception as ex:errors[label]=str(ex)
 report['errors']=errors;report['memoryFinal']=e.memory_report();e.close();(a.output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({'cases':len(report['cases']),'output':str(a.output)}))
if __name__=='__main__':main()
