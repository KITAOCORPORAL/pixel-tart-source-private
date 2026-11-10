from pathlib import Path
import tempfile,json,numpy as np
from metrics import compare
with tempfile.TemporaryDirectory(prefix='stable-metrics-') as temp:
 root=Path(temp);a=np.zeros((520,520,3),dtype='<u2');b=a.copy();b[0,0,0]=65535;b[10,500,1]=17;b[100,100,2]=3;f=root/'a.u16';g=root/'b.u16';f.write_bytes(a.tobytes());g.write_bytes(b.tobytes());r=compare(f,g,520,520);assert r['regions']['whole']['changedChannels']==3 and r['regions']['whole']['changedPixels']==3;assert r['maxDifference']==dict(x=0,y=0,channel=0,first=0,second=65535,signedDelta=65535);assert r['firstDifference']==r['maxDifference'];assert r['bounds']==dict(left=0,right=500,top=0,bottom=100);assert r['regions']['outerBorder8']['changedPixels']==1;assert r['regions']['tileSeamBand8ExcludingBorder']['changedPixels']==1;assert r['regions']['interiorNonSeam']['changedPixels']==1;assert r['regions']['whole']['sumAbsU16']==65555;assert compare(f,f,520,520)['bitExact'];g.write_bytes(b'bad');
 try:compare(f,g,520,520);raise AssertionError('Truncation accepted')
 except ValueError:pass
print(json.dumps(dict(checks=10,scope='exact integer/dimensions/channel-vs-pixel/max-coordinates/border-and-seam partition/zero/truncation',status='PASS',rawCalls=0)))
