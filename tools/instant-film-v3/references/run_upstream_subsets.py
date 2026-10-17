"""Execute pinned upstream subsets; report domains/behavior, never assert full-module equivalence."""
import pathlib,json,subprocess,time,hashlib,numpy as np,ctypes as C
R=pathlib.Path(__file__).resolve().parents[3];A=R/'artifacts/v3-product';B=A/'native-references';rows=[]
def run(name,data):
 cmd=[str(B/(name+'.exe'))];start=time.perf_counter();p=subprocess.run(cmd,input=data,capture_output=True,text=True,timeout=40);values=np.array([float(v) for v in p.stdout.split()]);return values,dict(engine=name,exitCode=p.returncode,millisecondsIncludingProcessIo=(time.perf_counter()-start)*1000,outputSha256=hashlib.sha256(values.tobytes()).hexdigest(),minimum=float(values.min()) if values.size else None,maximum=float(values.max()) if values.size else None,finite=bool(np.isfinite(values).all()),processPeakMemory='NOT_MEASURED_IN_THIS_DRIVER')
# Neutral/saturated-channel values, negatives and >1 remain explicit. Candidate clamps encoded inputs before its creative response.
values=np.r_[np.linspace(0,1,65),[-.5,-.01,1.2,2.,4.]]
for name in ['darktable-exposure','rawtherapee-basecurve']:
 out,meta=run(name,'\n'.join(map(str,values)));meta.update(node='ExposureOrTone',input=values.tolist(),output=out.tolist(),inputDomain='Linear scalar/individual channels',params='darktable black0 scale2 / RT a2 black0 D1 hr.5 sr.5',maxDifferenceFromLinearExposure=float(abs(out-values*2).max()),expectedEqual='darktable exposure only; RT intentionally rolls upper region, candidate creative toe/shoulder differs');rows.append(meta)
for scene in ['flat','impulse','edge','saturated','negative-overrange']:
 x=np.full((33,37),.18,dtype=np.float32)
 if scene=='impulse':x[:]=0;x[16,18]=1
 if scene=='edge':x[:,:18]=0;x[:,18:]=1
 if scene=='saturated':x=np.tile(np.linspace(0,1,37,dtype=np.float32),(33,1))
 if scene=='negative-overrange':x=np.tile(np.linspace(-.5,2,37,dtype=np.float32),(33,1))
 results=[]
 for name in ['darktable-gaussian','rawtherapee-gauss3']:
  out,meta=run(name,f'37 33\n'+ '\n'.join(map(str,x.ravel())));meta.update(node='Spatial',scene=scene,width=37,height=33,params='dt recursive sigma1 clamp[-1,2] / RT finite3x3 prescribed coefficients',sum=float(out.sum()),mean=float(out.mean()),inputSha256=hashlib.sha256(x.tobytes()).hexdigest(),flatMaximumDrift=float(abs(out-.18).max()) if scene=='flat' else None,scope='Independent blur implementations, not equal kernel/support; finite candidate radius2 has different tail/boundary');rows.append(meta);results.append(out)
 rows.append(dict(node='SpatialBehaviorDifference',scene=scene,maxBetweenReferences=float(abs(results[0]-results[1]).max()),notAnEqualityGate=True))
coords=np.array([(x/37*11.37,y/33*9.11) for y in range(33) for x in range(37)])
for name in ['darktable-grain-noise','stb-independent-noise']:
 out,meta=run(name,'\n'.join(f'{x} {y}' for x,y in coords));again,_=run(name,'\n'.join(f'{x} {y}' for x,y in coords));grid=out.reshape(33,37);meta.update(node='NoiseMaterial',repeatExact=bool(np.array_equal(out,again)),mean=float(out.mean()),std=float(out.std()),adjacentCorrelation=float(np.corrcoef(grid[:,:-1].ravel(),grid[:,1:].ravel())[0,1]),scope='darktable grain simplex octaves vs independent stb Perlin fBm, distributions/support not same realization');rows.append(meta)
report=dict(status='UPSTREAM_SUBSTAGES_EXECUTED_NOT_FULL_MODULE_ADMISSION',compiler='Zig0.13.0 local ignored only',builds=json.loads((B/'BUILD_IDENTITIES.json').read_text()),rows=rows,rawtherapeeGrainNotIndependent=True,secondNoise='stb Perlin Sean Barrett independent implementation, full commit2c980bb59875b0d32144a71867fbdebb2f77cd20; MIT/public-domain choice not copied to product',productionAdmission=False,missing=['Full darktable/RT pipeline parameter equivalence','Real-photo/reference node matrix and mature reference peak memory','Complete noise/grain material statistics and applicable tolerance decision','Full product recipe/UI/export integration remains gated'])
(A/'02-runtime-reference.json').write_text(json.dumps(report,indent=2)+'\n');print(report['status'],len(rows),'observations')
