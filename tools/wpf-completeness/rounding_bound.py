"""A conditional arithmetic proof for the frozen 3x3 matrix model, not a universal LCMS epsilon."""
import pathlib,struct,json,sys
from fractions import Fraction as Q
R=pathlib.Path(__file__).resolve().parents[2];sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps');import numpy as np

def matrix(p):
 b=p.read_bytes();tags={}
 for i in range(struct.unpack_from('>I',b,128)[0]):s,o,n=struct.unpack_from('>4sII',b,132+i*12);tags[s]=o
 return [[Q(struct.unpack_from('>i',b,tags[s]+8+j*4)[0],65536) for s in [b'rXYZ',b'gXYZ',b'bXYZ']] for j in range(3)]
def inv(m):
 a=[r[:]+[Q(int(i==j)) for j in range(3)] for i,r in enumerate(m)]
 for i in range(3):
  if a[i][i]==0:
   k=next(k for k in range(i+1,3) if a[k][i]);a[i],a[k]=a[k],a[i]
  v=a[i][i];a[i]=[x/v for x in a[i]]
  for j in range(3):
   if i!=j:v=a[j][i];a[j]=[x-v*y for x,y in zip(a[j],a[i])]
 return [r[3:] for r in a]
def mul(a,b):return [[sum((a[i][k]*b[k][j] for k in range(3)),Q(0)) for j in range(3)] for i in range(3)]
s=matrix(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc');w=matrix(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc');a=mul(inv(w),s);b=mul(inv(s),w);identity=mul(b,a);assert identity==[[Q(int(i==j)) for j in range(3)] for i in range(3)]
# Exact fractions for rounded float32 coefficients, with 3 multiplies + 2 sums <=gamma5.
af=[[Q(float(np.float32(float(x)))) for x in row] for row in a];bf=[[Q(float(np.float32(float(x)))) for x in row] for row in b];u=Q(1,2**24);g5=5*u/(1-5*u)
# x is finitefloat32 in[0,1], nooverflow/subnormalassumed. Half multiplication is exact in this range.
ae=[sum(abs(af[i][j]-a[i][j]) for j in range(3))+g5*sum(abs(v) for v in af[i]) for i in range(3)];wmax=[sum(abs(v) for v in row) for row in a]
bounds=[]
for i in range(3):
 propagated=sum(abs(bf[i][j])*ae[j]/2 for j in range(3));coefficient=sum(abs(bf[i][j]-b[i][j])*wmax[j]/2 for j in range(3));arithmetic=g5*sum(abs(bf[i][j])*(wmax[j]+ae[j])/2 for j in range(3));bounds.append(propagated+coefficient+arithmetic)
print(json.dumps(dict(model='exactICCfixed16matrices -> rationalinverse/composition -> roundcoefficientstofloat32 -> two scalar3termfloat32matrixops +exacthalf',exactRationalRoundtripIdentity=True,unitRoundoff=str(u),gamma5=str(g5),firstStageAbsoluteBounds=[float(x) for x in ae],outputAbsoluteBounds=[float(x) for x in bounds],outputBoundsIn16bitCodes=[float(x*65535) for x in bounds],proofConditions=['same frozen source/target profile','x exact finitefloat32 0..1','no nonlinear stages','3mul2add perrow IEEEroundtonearest','normalvalues/nooverflow','EV-1exact scaling'],notProofOfLcmsInternalCoefficientConstruction=True,productionEpsilonAdded=False,decision='conditionalindependentmatrixmodelonly; LCMSactualmaxbelowboundisvalidationnotengineproof; doNOTsnapallnegative',CalibrationEligible=False),indent=2))
