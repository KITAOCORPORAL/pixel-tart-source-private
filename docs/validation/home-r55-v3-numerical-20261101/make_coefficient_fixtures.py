"""Original deterministic baseline-JPEG coefficient fixtures. No external engine code."""
from pathlib import Path
import struct,json,hashlib
root=Path(__import__('sys').argv[1]);root.mkdir(parents=True,exist_ok=True)
def marker(code,data):return b'\xff'+bytes([code])+struct.pack('>H',len(data)+2)+data
def jpeg(name,subsampling,channel,u,amplitude):
 assert amplitude==0 or 64<=amplitude<=127,'Fixed Huffman fixture category7 requires amplitude64..127'
 w,h=4096,64
 # Quantization=1, DC category0 only. AC EOB=0, coefficient1(size7)=10, coefficient3(size7)=110.
 dht=bytes([0])+bytes([1]+[0]*15)+bytes([0])+bytes([0x10])+bytes([1,1,1]+[0]*13)+bytes([0,0x07,0x57])
 out=b'\xff\xd8'+marker(0xdb,bytes([0])+bytes([1]*64))+marker(0xc0,bytes([8])+struct.pack('>HH',h,w)+bytes([3,1,0x22 if subsampling==2 else 0x11,0,2,0x11,0,3,0x11,0]))+marker(0xc4,dht)+marker(0xda,bytes([3,1,0,2,0,3,0,0,63,0]))
 bits=[]
 def block(c):
  bits.append('0') # zero DC difference
  if c==channel and amplitude:
   bits.append('10' if u==1 else '110');bits.append(format(amplitude,'07b'))
  bits.append('0') # EOB
 yh,yv=(2,2)if subsampling==2 else(1,1)
 for my in range(h//(8*yv)):
  for mx in range(w//(8*yh)):
   for c in range(3):
    for _ in range(yh*yv if c==0 else 1):block(c)
 b=''.join(bits);b+='1'*((-len(b))%8);raw=int(b,2).to_bytes(len(b)//8,'big').replace(b'\xff',b'\xff\x00');out+=raw+b'\xff\xd9';p=root/(name+'.jpg');p.write_bytes(out);return {'name':name,'sha256':hashlib.sha256(out).hexdigest(),'width':w,'height':h,'sampling':subsampling,'component':channel,'u':u,'amplitude':amplitude,'quantization':1,'profile':False,'orientation':False,'provenance':'Original coefficient-defined deterministic JPEG, not a photograph'}
rows=[jpeg('dc-444',0,0,0,0),jpeg('y-ac1-444',0,0,1,80),jpeg('y-ac3-444',0,0,3,80),jpeg('cb-ac3-420',2,1,3,80),jpeg('cr-ac1-420',2,2,1,80)]
(root/'FIXTURES.json').write_text(json.dumps(rows,indent=2));print(json.dumps(rows,indent=2))
