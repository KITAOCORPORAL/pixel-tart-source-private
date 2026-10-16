"""Independent known matrix/TRC profile oracle; only reads three exact research profile files."""
import pathlib,json,struct,numpy as np
r=pathlib.Path('artifacts/followup/04-display-reference-fixed');d=json.loads((r/'DISPLAY_REFERENCE.json').read_text())
def read(blob):
 n=struct.unpack_from('>I',blob,128)[0];t={blob[132+12*i:136+12*i]:struct.unpack_from('>II',blob,136+12*i) for i in range(n)}
 def matrix():
  cols=[]
  for k in [b'rXYZ',b'gXYZ',b'bXYZ']:
   o,s=t[k];assert blob[o:o+4]==b'XYZ ';cols.append([struct.unpack_from('>i',blob,o+8+4*i)[0]/65536 for i in range(3)])
  return np.array(cols).T
 curves=[]
 for k in [b'rTRC',b'gTRC',b'bTRC']:
  o,s=t[k];assert blob[o:o+4]==b'curv';count=struct.unpack_from('>I',blob,o+8)[0]
  curves.append(('gamma',struct.unpack_from('>H',blob,o+12)[0]/256) if count==1 else ('table',np.array(struct.unpack_from('>'+str(count)+'H',blob,o+12))/65535))
 return matrix(),curves
sm,sc=read((r/'source.private.icc').read_bytes());out=[]
for index,row in enumerate(d['rows'],1):
 dm,dc=read((r/f'display{index}.private.icc').read_bytes());values=np.array(row['inputRgb16']).reshape(-1,3)/65535
 linear=np.empty_like(values)
 for c,(kind,curve) in enumerate(sc):linear[:,c]=values[:,c]**curve if kind=='gamma' else np.interp(values[:,c],np.linspace(0,1,len(curve)),curve)
 dest=linear@sm.T@np.linalg.inv(dm).T;codes=np.empty_like(dest)
 for c,(kind,curve) in enumerate(dc):codes[:,c]=np.clip(dest[:,c],0,1)**(1/curve) if kind=='gamma' else np.interp(np.clip(dest[:,c],0,1),curve,np.linspace(0,1,len(curve)))
 expected=np.rint(codes*65535).astype(int).reshape(-1);lcms=np.array(row['lcmsRgb16']);wic=np.array(row['wicRgb16']);out.append(dict(device=row['device'],independentProfileCodes=expected.tolist(),maximumVsLcms=int(abs(expected-lcms).max()),maximumVsWic=int(abs(expected-wic).max()),limitVsLcms=16,status='PASSED_KNOWN_MATRIX_TRC_SUBSET' if abs(expected-lcms).max()<=16 else 'FAILED',scope='Profile PCS XYZ matrices already D50; no repeated chad; source actual tableTRC and target actual gamma, not ideal sRGB formula; not arbitrary ICC'))
result=dict(status='PASSED_KNOWN_MATRIX_TRC_SUBSET' if all(x['status'].startswith('PASSED') for x in out) else 'FAILED',rows=out,productionEnabled=False,physicalScreen='NOT_RUN')
pathlib.Path('artifacts/followup/04-independent-profile-oracle.json').write_text(json.dumps(result,indent=2)+'\n');print(result['status'],[(x['maximumVsLcms'],x['maximumVsWic']) for x in out])
