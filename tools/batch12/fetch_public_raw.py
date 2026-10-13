#!/usr/bin/env python3
"""Fetch ONE licensed Cube++ archive member using validated HTTP ranges, ZIP CRC and pinned SHA-256.
Standard library only. Never downloads the 200GB dataset; never places RAW in Git automatically.
Usage: python fetch_public_raw.py --output /absolute/local-fixtures/00_0010.CR2
"""
import argparse, hashlib, json, pathlib, struct, urllib.request, zlib
URL='https://zenodo.org/api/records/4153431/files/source_CR2_0.zip/content'
SIZE=13796343517
MEMBER='Cube++/auxiliary/source/CR2/00_0010.CR2'
SHA='cd7492209f002050f24e4ae4c2365f8702145d917f4979e2456aeddb33aebc6f'
def fetch(a,b):
    request=urllib.request.Request(URL,headers={'Range':f'bytes={a}-{b}'})
    with urllib.request.urlopen(request,timeout=120) as response:
        if response.status!=206 or response.headers.get('Content-Range')!=f'bytes {a}-{b}/{SIZE}':
            raise ValueError('Range unsupported or archive changed; refusing full download')
        value=response.read(b-a+2)
        if len(value)!=b-a+1:raise ValueError('Truncated/oversized range')
        return value

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--output',required=True);args=parser.parse_args();output=pathlib.Path(args.output).resolve()
    if output.exists():
        if hashlib.sha256(output.read_bytes()).hexdigest()==SHA:print('Existing pinned sample verified',output);return
        raise ValueError('Refusing to overwrite mismatched file')
    tail=fetch(SIZE-65536,SIZE-1);position=tail.rfind(b'PK\x06\x06')
    if position<0:raise ValueError('Expected ZIP64 archive')
    end=struct.unpack_from('<4sQ2H2L4Q',tail,position);cd=fetch(end[-1],end[-1]+end[-2]-1);p=0;selected=None
    while p<len(cd):
        entry=struct.unpack_from('<4s6H3L5H2L',cd,p)
        if entry[0]!=b'PK\x01\x02':raise ValueError('Invalid central directory')
        name=cd[p+46:p+46+entry[10]].decode('utf-8');extra=cd[p+46+entry[10]:p+46+entry[10]+entry[11]]
        v=dict(method=entry[4],crc=entry[7],compressed=entry[8],size=entry[9],offset=entry[16]);q=0
        while q<len(extra):
            tag,length=struct.unpack_from('<HH',extra,q);payload=extra[q+4:q+4+length];q+=4+length
            if tag==1:
                pos=0
                for key in ['size','compressed','offset']:
                    if v[key]==0xffffffff:v[key]=struct.unpack_from('<Q',payload,pos)[0];pos+=8
        if name==MEMBER:
            if selected is not None:raise ValueError('Duplicate member')
            selected=v
        p+=46+entry[10]+entry[11]+entry[12]
    if selected is None:raise ValueError('Missing member')
    v=selected;header=struct.unpack('<4s5H3L2H',fetch(v['offset'],v['offset']+29))
    if header[0]!=b'PK\x03\x04' or header[2]&1:raise ValueError('Invalid/encrypted local header')
    start=v['offset']+30+header[-2]+header[-1];compressed=fetch(start,start+v['compressed']-1)
    if v['method'] not in (0,8):raise ValueError('Unsupported compression')
    data=zlib.decompress(compressed,-15) if v['method']==8 else compressed
    if len(data)!=v['size'] or zlib.crc32(data)!=v['crc'] or hashlib.sha256(data).hexdigest()!=SHA:raise ValueError('Integrity mismatch')
    output.parent.mkdir(parents=True,exist_ok=True)
    with output.open('xb') as file:file.write(data)
    print('Verified',output,len(data),SHA)
if __name__=='__main__':main()
