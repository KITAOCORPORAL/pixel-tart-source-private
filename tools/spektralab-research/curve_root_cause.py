"""Bounded R5 cause analysis using existing taps; no resource edits or smoothing."""
import argparse,json,hashlib,subprocess,sys
from pathlib import Path
import numpy as np,scipy,scipy.version
from scipy.interpolate import make_interp_spline
from scipy.special import ndtr
from run_matrix import sha,digest,require
p=argparse.ArgumentParser();p.add_argument('research',type=Path);p.add_argument('probe',type=Path);p.add_argument('output',type=Path);a=p.parse_args();a.output.mkdir(exist_ok=False)
root=a.research;require(subprocess.check_output(['git','-C',str(root/'source2'),'rev-parse','HEAD'],text=True).strip()=='c1447c6ab618fa4b1cb11fd2fe12826855007059','Wrong source commit');require(not subprocess.check_output(['git','-C',str(root/'source2'),'status','--porcelain'],text=True).strip(),'Original source dirty');resources=root/'resources-original';old=json.loads((Path(__file__).resolve().parents[2]/'docs/research/color-engine-20261029/company-round4/neutral-declines.json').read_text())
report={'version':'CurveRootCauseV1','resourceModified':False,'candidateFix':'NONE; preserve shipped data','sourceCommit':'c1447c6ab618fa4b1cb11fd2fe12826855007059','references':{'numpy':{'version':np.__version__,'commit':np.version.git_revision,'entry':'numpy.interp; numpy/_core/src/multiarray/compiled_base.c:arr_interp','license':'BSD-3-Clause'},'scipy':{'version':scipy.__version__,'commit':scipy.version.git_revision,'entry':'scipy.interpolate.make_interp_spline(k=1) BSpline.__call__; scipy.special.ndtr for diagnostic CDF','license':'BSD-3-Clause'}},'probeSha256':sha(a.probe),'cases':[]}
for film in ['kodak_portra_400','kodak_ektar_100']:
 profile=resources/'profiles'/f'{film}.json';j=json.loads(profile.read_text());require(sha(profile)==sha(root/'source2/engine/resources/profiles'/f'{film}.json'),'Runtime profile differs from pinned source');x=np.asarray(j['data']['log_exposure']);raw=np.asarray(j['data']['density_curves']);normal=raw-raw.min(axis=0);trace=root/('trace2-'+film)
 def tap(name):
  files=list(trace.glob('*-'+name+'-17-1025-3.f32'));require(len(files)==1,'missing/duplicate old tap '+name);return np.fromfile(files[0],dtype='<f4').reshape(17,1025,3),files[0]
 log,logfile=tap('log_exposure');filmout,filmfile=tap('film_curves_exact');oldcase=next(c for c in old['cases'] if c['film']==film)
 for f in [logfile,filmfile]:require(sha(f)==next(x['sha256'] for x in oldcase['stages'] if x['path']==f.name),'Archived tap changed')
 indices=sorted({v for e in oldcase['declines'] for v in [e['index'],e['index']+1]});observed=log[8,indices].copy();mids=(x[:-1]+x[1:])/2
 stress=np.array([[-10,-4,-3],[-2,-1,0],[.18,1,2],[4,5,10],[-3,4,0],[4,-3,4]],dtype=np.float32)
 queries=np.concatenate([observed,np.broadcast_to(x[:,None],(len(x),3)),np.broadcast_to(mids[:,None],(len(mids),3)),stress]).astype('<f4');queryfile=a.output/(film+'-queries.f32');queries.tofile(queryfile);nativefile=a.output/(film+'-native.json');subprocess.run([str(a.probe),str(resources),film,str(queryfile),str(nativefile),'1'],check=True,capture_output=True);native=json.loads(nativefile.read_text());actual=np.asarray(native['actual']).reshape(-1,3);xx=np.asarray(native['x32']).reshape(-1,3);yy=np.asarray(native['y32']).reshape(-1,3);inv=np.asarray(native['inv32']).reshape(-1,3)
 require(np.array_equal(xx,np.broadcast_to(x[:,None],xx.shape).astype(np.float32)),'baked x mismatch');require(np.array_equal(yy,normal.astype(np.float32)),'baked y mismatch')
 # Same uploaded rounded table for both references; endpoints use explicit common constant extension.
 numpy=np.column_stack([np.interp(queries[:,c],xx[:,c],yy[:,c]) for c in range(3)])
 scipy_linear=np.column_stack([make_interp_spline(xx[:,c],yy[:,c],k=1)(np.clip(queries[:,c],xx[0,c],xx[-1,c])) for c in range(3)])
 cpu32=np.empty_like(queries);rows=[];outside=0
 for row,q in enumerate(queries):
  for c,v in enumerate(q):
   k=int(np.clip(np.searchsorted(xx[:,c],v,side='right')-1,0,len(x)-2));fraction=np.float32(np.float32(v-np.float32(xx[k,c]))*np.float32(inv[k,c]));y0,y1=np.float32(yy[k,c]),np.float32(yy[k+1,c]);calc=np.float32(y0+np.float32(fraction*np.float32(y1-y0)))
   if v<=xx[0,c]:calc=np.float32(yy[0,c])
   if v>=xx[-1,c]:calc=np.float32(yy[-1,c])
   cpu32[row,c]=calc
   if xx[0,c]<v<xx[-1,c] and not min(yy[k,c],yy[k+1,c])<=actual[row,c]<=max(yy[k,c],yy[k+1,c]):outside+=1
 raw64=np.column_stack([np.interp(queries[:,c],x,normal[:,c]) for c in range(3)])
 raw_scipy64=np.column_stack([make_interp_spline(x,normal[:,c],k=1)(np.clip(queries[:,c],x[0],x[-1])) for c in range(3)])
 # Only real affected neighborhoods in committed report; all query buffers remain isolated.
 position={index:n for n,index in enumerate(indices)}
 for edge in oldcase['declines']:
  i=edge['index'];channels=[]
  for c in range(3):
   li,ri=position[i],position[i+1];l,r=queries[li,c],queries[ri,c];kl=int(np.clip(np.searchsorted(xx[:,c],l,side='right')-1,0,len(x)-2));kr=int(np.clip(np.searchsorted(xx[:,c],r,side='right')-1,0,len(x)-2));lo=min(kl,kr);hi=max(kl,kr)+1
   channels.append({'channel':c,'logQuery':[float(l),float(r)],'resourceIndices':[lo,hi],'resourceNeighbourhood':[{'index':k,'logExposure':float(x[k]),'rawDensity':float(raw[k,c]),'normalisedF32':float(yy[k,c])} for k in range(max(0,lo-1),min(len(x),hi+2))],'actualFloat':[float(actual[li,c]),float(actual[ri,c])],'numpyFloat64':[float(numpy[li,c]),float(numpy[ri,c])],'scipyLinearFloat64':[float(scipy_linear[li,c]),float(scipy_linear[ri,c])],'deltaActual':float(actual[ri,c]-actual[li,c]),'deltaReference':float(numpy[ri,c]-numpy[li,c]),'deltaRawDoubleTable':float(raw64[ri,c]-raw64[li,c]),'rawDoubleReference':[float(raw64[li,c]),float(raw64[ri,c])],'oldTraceExact':bool(actual[li,c]==float(filmout[8,i,c]) and actual[ri,c]==float(filmout[8,i+1,c]))})
  rows.append({'outputIndex':i,'outputChannel':edge['channel'],'outputU16':edge['outputU16'],'channels':channels})
 model=j['data']['density_curves_model'];centers=np.asarray(model['centers']);amps=np.asarray(model['amplitudes']);sigmas=np.asarray(model['sigmas']);fitted=np.stack([np.sum(amps[c]*ndtr((x[:,None]-centers[c])/sigmas[c]),axis=1) for c in range(3)],axis=1);cppmodel=np.asarray(native['modelCdfDiagnosticOnly']).reshape(-1,3)
 sample={ 'film':film,'profilePath':str(profile),'profileSha256':sha(profile),'existingTrace':[{'name':logfile.name,'sha256':sha(logfile)},{'name':filmfile.name,'sha256':sha(filmfile)}],'queryCount':len(queries),'querySha256':sha(queryfile),'nativeLocalReportSha256':sha(nativefile),'rawTable':{'shape':list(raw.shape),'xStrictlyIncreasing':bool((np.diff(x)>0).all()),'rangePerChannel':[raw.min(axis=0).tolist(),raw.max(axis=0).tolist()],'negativeAdjacentCountPerChannel':(np.diff(raw,axis=0)<0).sum(axis=0).tolist(),'maxDownwardStepPerChannel':np.diff(raw,axis=0).min(axis=0).tolist(),'normalisationPreservesDifferences':bool(np.allclose(np.diff(raw,axis=0),np.diff(normal,axis=0),rtol=0,atol=5e-16))},'referenceErrors':{'rawDoubleNumpyVsScipyMaxAbs':float(np.max(np.abs(raw64-raw_scipy64))),'tableRoundingVsRawDoubleMaxAbs':float(np.max(np.abs(raw64-numpy))),'numpyVsScipyMaxAbs':float(np.max(np.abs(numpy-scipy_linear))),'gpuVsNumpyMaxAbs':float(np.max(np.abs(actual-numpy))),'cpu32VsGpuMaxAbs':float(np.max(np.abs(cpu32-actual))),'affectedGpuVsRawDoubleMaxAbs':float(np.max(np.abs(actual[:len(indices)]-raw64[:len(indices)]))),'affectedGpuVsRoundedTableFloat64MaxAbs':float(np.max(np.abs(actual[:len(indices)]-numpy[:len(indices)]))),'observedGpuVsOriginalTraceMaxAbs':float(np.max(np.abs(actual[:len(indices)]-filmout[8,indices]))),'linearInterpolationOutsideBracketCount':outside},'sublayerDiagnosticOnly':{'summedLayerNegativeStepsPerChannel':(np.diff(np.asarray(j['data']['density_curves_layers']).sum(axis=1),axis=0)<0).sum(axis=0).tolist(),'layersSumVsCdfMaxAbs':float(np.max(np.abs(np.asarray(j['data']['density_curves_layers']).sum(axis=1)-fitted))),'runtimeWithGrainOffUsesLayers':False},'modelDiagnosticOnly':{'runtimeFilmUsesModel':False,'allAmplitudesPositive':bool((amps>0).all()),'allSigmasPositive':bool((sigmas>0).all()),'cdfNegativeStepsPerChannel':(np.diff(fitted,axis=0)<0).sum(axis=0).tolist(),'cppEvaluatorVsScipyNdtrMaxAbs':float(np.max(np.abs(cppmodel-fitted))),'sampledTableMinusFittedMaxAbsPerChannel':np.max(np.abs(raw-fitted),axis=0).tolist(),'notProposedAsFix':True},'affectedOutputNeighbourhoods':rows }
 report['cases'].append(sample)
 require(sample['referenceErrors']['observedGpuVsOriginalTraceMaxAbs']==0,'probe failed to reproduce existing trace')
 require(outside==0,'interpolation outside resource bracket')
(a.output/'report.json').write_text(json.dumps(report,indent=2));print(json.dumps({'cases':[(c['film'],c['rawTable']['negativeAdjacentCountPerChannel'],c['referenceErrors']) for c in report['cases']]}))
