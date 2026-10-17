"""Independent pre-admission check; current candidate intentionally fails existing gates.
Never changes product parameters or relaxes thresholds to obtain a pass.
"""
import argparse, hashlib, json, pathlib, sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'artifacts/v3-corrections/reference-python'))
import numpy as np
import colour
import PyOpenColorIO as ocio

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--input',type=pathlib.Path,required=True);parser.add_argument('--output',type=pathlib.Path,required=True);args=parser.parse_args()
 if colour.__version__!='0.4.7' or ocio.__version__!='2.4.2':raise RuntimeError('Pinned reference mismatch')
 raw=args.input.read_text(encoding='utf-8-sig');rows=json.loads(raw[raw.index('['):]);measured=[];colour_max=0;ocio_max=0
 for row in rows:
  rgb=np.array([row['input'][k] for k in ['X','Y','Z']]);got=np.array([row['output'][k] for k in ['X','Y','Z']]);ev=row['ev']
  if not np.isfinite(rgb).all() or not np.isfinite(got).all():raise ValueError('Non-finite probe')
  expected=np.clip(colour.models.eotf_inverse_sRGB(colour.models.eotf_sRGB(rgb)*2**ev),0,1)
  decode=ocio.ExponentWithLinearTransform(gamma=[2.4,2.4,2.4,1],offset=[.055,.055,.055,0],negativeStyle=ocio.NEGATIVE_LINEAR)
  encode=ocio.ExponentWithLinearTransform(gamma=[2.4,2.4,2.4,1],offset=[.055,.055,.055,0],negativeStyle=ocio.NEGATIVE_LINEAR,direction=ocio.TRANSFORM_DIR_INVERSE)
  g=2**ev;matrix=ocio.MatrixTransform(matrix=[g,0,0,0,0,g,0,0,0,0,g,0,0,0,0,1]);group=ocio.GroupTransform([decode,matrix,encode])
  cpu=ocio.Config.CreateRaw().getProcessor(group).getOptimizedCPUProcessor(ocio.OPTIMIZATION_NONE);other=np.clip(cpu.applyRGB(rgb.tolist()),0,1)
  c=float(abs(got-expected).max());o=float(abs(got-other).max());colour_max=max(colour_max,c);ocio_max=max(ocio_max,o);measured.append(dict(ev=ev,input=rgb.tolist(),colourMax=c,ocioMax=o))
 result=dict(status='PASS' if colour_max<=2e-10 and ocio_max<=2e-6 else 'FAIL',colourMaximum=colour_max,ocioMaximum=ocio_max,colourLimit=2e-10,ocioLimit=2e-6,colourVersion=colour.__version__,ocioVersion=ocio.__version__,probeSha256=hashlib.sha256(args.input.read_bytes()).hexdigest(),rows=measured)
 args.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2));return 0 if result['status']=='PASS' else 1
if __name__=='__main__':raise SystemExit(main())
