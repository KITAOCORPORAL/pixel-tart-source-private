import hashlib,json,math
from pathlib import Path
NATIVE='F500C0732FEB21B188D5B52CEA05FD824D5B3C8016EB2CA68D8312ACC9F914B9'
MANAGED='6DF698647C79DF05E754072ED393A02315D780AF7287C455D765A767DF93D4A3'
SOURCES={'3A0C1672ABDA62CC26349C088CB87A268EF0E5B46B1F4CCC18C99EE936E447B5':4095,'593E1B0EE73EF7E699D73465E3BD8222AD0ED79BEB69E9265E440756D4B50A1A':16383}
STAGES={'scale_before','scale_after','matrix_before','matrix_after'}
DOMAIN='LibRaw 0.21.1 ProPhoto D65 linear matrix values divided by 65535; negative and >1 allowed'
def sha(path):
    h=hashlib.sha256()
    with Path(path).open('rb') as f:
        for chunk in iter(lambda:f.read(4194304),b''):h.update(chunk)
    return h.hexdigest()
def require(condition,message):
    if not condition: raise ValueError(message)
def read_json(path):
    def pairs(items):
        result={}
        for key,value in items:
            require(key not in result,'Duplicate JSON key: '+key);result[key]=value
        return result
    def reject(value):raise ValueError('Nonfinite JSON value: '+value)
    return json.loads(Path(path).read_text(encoding='utf-8-sig'),object_pairs_hook=pairs,parse_constant=reject)
def validate_stage(root):
    root=Path(root); m=read_json(root/'metadata.json'); s=m['sizes'];w=s['IWidth'];h=s['IHeight']
    require(m.get('manifestVersion')=='StageManifestV2' and m.get('sourceUnchanged') is True and m.get('productBaselineSampleMismatches')==0,'Unverified stage manifest')
    require(m.get('nativeHash')==NATIVE and m.get('managedHash')==MANAGED and m.get('architecture')=='X64','Untrusted DLL or architecture')
    require(m.get('sourceSha256') in SOURCES and m.get('Maximum')==SOURCES[m['sourceSha256']],'Untrusted source or white level')
    require(m.get('make','').strip()=='Nikon' and m.get('model','').strip()=='D800' and m.get('filters')==2492765332,'Unsupported camera or CFA')
    require((s['RawWidth'],s['RawHeight'],w,h,s['TopMargin'],s['LeftMargin'],s['Flip'],s['RawPitch'],m['cblack'])==(7424,4924,7378,4924,0,0,0,14848,[0,0,0,0,0,0]),'Unsupported layout')
    require(m['output']=={'Width':w,'Height':h,'Bits':16,'Channels':3},'Unsupported output format')
    stages=m['stages'];require(len(stages)==4 and {x['name'] for x in stages}==STAGES,'Missing or duplicate stages')
    require(all(x['width']==w and x['height']==h and x['layout']=='uint16 RG B G2, interleaved' for x in stages),'Stage dimensions or domain mismatch')
    require(all(len(x.get('RgbCam',[]))==12 and all(isinstance(v,(int,float)) and math.isfinite(v) for v in x['RgbCam']) and len(x.get('PreMul',[]))==4 and all(isinstance(v,(int,float)) and math.isfinite(v) and v>=0 for v in x['PreMul']) for x in stages),'Invalid stage matrix/WB')
    require(all(x.get('Black')==0 and x.get('Maximum')==m['Maximum'] and x.get('cblack')==[0,0,0,0,0,0] for x in stages),'Stage black/white mismatch')
    require(all(x['RgbCam']==stages[0]['RgbCam'] for x in stages),'Stage camera matrix changed')
    lengths={'cfa.u16':s['RawHeight']*s['RawPitch'],**{x+'.u16':w*h*8 for x in STAGES},'output.u16':w*h*6};files=m['files'];require(len(files)==len(lengths) and {x['name'] for x in files}==set(lengths),'Missing or duplicate files')
    for item in files:
        name=item['name'];p=root/name
        require(p.is_file() and item['length']==lengths[name] and p.stat().st_size==lengths[name],f'File length mismatch: {name}')
        require(sha(p)==item['sha256'].lower(),f'Hash mismatch: {name}')
    return m
def validate_candidate(path,m):
    path=Path(path); side=read_json(Path(str(path)+'.json'));s=m['sizes']
    require(side.get('schema')=='MatrixFloatOutputResearchV1' and side.get('width')==s['IWidth'] and side.get('height')==s['IHeight'] and side.get('channels')=='RGB' and side.get('storage')=='little-endian float32 interleaved','Candidate format mismatch')
    require(side.get('inputDomain')=='post-demosaic, post-WB camera RGB uint16' and side.get('outputDomain')==DOMAIN and side.get('icc')=='none; no ICC embedded','Candidate domain mismatch')
    require(side.get('sourceSha256')==m['sourceSha256'] and path.is_file() and path.stat().st_size==s['IWidth']*s['IHeight']*12 and sha(path)==side.get('sha256','').lower(),'Candidate source/length/hash mismatch')
    import numpy as np
    with path.open('rb') as f:
        for block in iter(lambda:f.read(4194304),b''):
            require(np.isfinite(np.frombuffer(block,dtype='<f4')).all(),'Nonfinite candidate samples')
    return side
