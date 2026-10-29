import sys,json,hashlib
import numpy as np
from pathlib import Path

# Float coefficients frozen from LibRaw 0.21.1 colorconst.cpp; multiplication
# deliberately models its float accumulation, not an ICC profile.
P=np.array([[.529317,.330092,.140588],[.098368,.873465,.028169],[.016879,.117663,.865457]],dtype=np.float64)
def matrix(rgb):
    m=np.zeros((3,3),dtype=np.float32)
    for i in range(3):
        for j in range(3):
            for k in range(3): m[i,j]=np.float32(np.float64(m[i,j])+P[i,k]*np.float64(rgb[k,j]))
    return m

def calc(x,m):
    x=np.asarray(x,dtype=np.float32)
    return np.add(np.add(x[...,0:1]*m[:,0],x[...,1:2]*m[:,1],dtype=np.float32),x[...,2:3]*m[:,2],dtype=np.float32)

def selftest():
    m=np.array([[1,-.5,0],[0,1,0],[0,0,2]],np.float32)
    x=np.array([[0,0,0],[1,3,0],[65535,0,40000],[11,1,0]],np.float32)
    y=calc(x,m); q=np.clip(np.trunc(y),0,65535).astype(np.uint16)
    assert np.array_equal(q,np.array([[0,0,0],[0,3,0],[65535,0,65535],[10,1,0]],np.uint16))
    assert y[1,0]==-.5 and y[2,2]==80000 and y[3,0]==10.5
    # Original LibRaw scale loop converts the float product to int before CLIP.
    assert np.array_equal(np.clip(np.trunc(np.array([0,10,4000],np.float32)*np.float32(20)),0,65535),[0,200,65535])
    return {"status":"PASS","input":x.tolist(),"matrix":m.tolist(),"float":y.tolist(),"uint16":q.tolist()}

def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        while a:=f.read(4194304):h.update(a)
    return h.hexdigest()

def analyze(root):
    meta=json.loads((root/'metadata.json').read_text(encoding='utf-8-sig')); sz=meta['sizes'];h,w=sz['IHeight'],sz['IWidth'];n=h*w
    if (sz['Flip'],sz['TopMargin'],sz['LeftMargin'],sz['RawPitch'],meta['cblack']) != (0,0,0,sz['RawWidth']*2,[0,0,0,0,0,0]):raise ValueError('Only observed simple D800 layout supported')
    stages={x['name']:x for x in meta['stages']}; rgb=np.array(stages['matrix_before']['RgbCam'],np.float32).reshape(3,4)[:,:3];m=matrix(rgb)
    a=np.memmap(root/'matrix_before.u16',dtype='<u2',mode='r',shape=(n,4));b=np.memmap(root/'matrix_after.u16',dtype='<u2',mode='r',shape=(n,4));o=np.memmap(root/'output.u16',dtype='<u2',mode='r',shape=(n,3));s0=np.memmap(root/'scale_before.u16',dtype='<u2',mode='r',shape=(h,w,4));s1=np.memmap(root/'scale_after.u16',dtype='<u2',mode='r',shape=(h,w,4));raw=np.memmap(root/'cfa.u16',dtype='<u2',mode='r',shape=(sz['RawHeight'],sz['RawWidth']))
    gain=np.array(stages['scale_after']['PreMul'],np.float32);scale=np.array([np.float32(float(g)*65535/meta['Maximum']) for g in gain]); filters=meta['filters']; candidate_path=root/'candidate-float32.rgb'; candidate_hash=hashlib.sha256();totals={'negative':[0]*3,'overflow':[0]*3,'fractional_in_range':[0]*3,'predicted_vs_native_mismatch':[0]*3,'float_vs_double_max_abs':[0.0]*3,'matrix_after_vs_output_mismatch':[0]*3,'scale_predicted_vs_native_mismatch':0,'scale_above_65535':0,'matrix_out_of_range_pixels':0,'matrix_out_of_range_with_same_site_scale_clip':0,'matrix_max_excess_codes':[0.0]*3,'max_inrange_quantization_codes':[0.0]*3,'cfa_at_or_above_metadata_white':0,'cfa_matches_before_scale':0}; examples={};
    for y0 in range(0,h,32):
        y1=min(h,y0+32);i=y0*w;j=y1*w; x=np.asarray(a[i:j,:3]);native=np.asarray(b[i:j,:3]);value=calc(x,m); payload=(value/np.float32(65535)).astype('<f4',copy=False).tobytes();candidate_hash.update(payload);pred=np.clip(np.trunc(value),0,65535).astype(np.uint16);ref=x.astype(np.float64)@m.astype(np.float64).T
        for c in range(3):
            v=value[:,c];totals['negative'][c]+=int((v<0).sum());totals['overflow'][c]+=int((v>65535).sum());totals['fractional_in_range'][c]+=int(((v>=0)&(v<=65535)&(v!=np.trunc(v))).sum());totals['predicted_vs_native_mismatch'][c]+=int((pred[:,c]!=native[:,c]).sum());totals['float_vs_double_max_abs'][c]=max(totals['float_vs_double_max_abs'][c],float(np.max(np.abs(v.astype(np.float64)-ref[:,c]))));totals['matrix_after_vs_output_mismatch'][c]+=int((native[:,c]!=o[i:j,c]).sum());totals['matrix_max_excess_codes'][c]=max(totals['matrix_max_excess_codes'][c],float(np.maximum(v-65535,0).max()),float(np.maximum(-v,0).max())); mask=(v>=0)&(v<=65535);totals['max_inrange_quantization_codes'][c]=max(totals['max_inrange_quantization_codes'][c],float(np.max(np.where(mask,np.abs(v-native[:,c]),0))))
        rows=np.arange(y0,y1)[:,None];cols=np.arange(w)[None,:]; channel=((filters >> ((((rows<<1)&14)+(cols&1))<<1))&3).astype(np.intp); channel=np.where((channel==1)&((rows&1)==1),3,channel);before=np.take_along_axis(s0[y0:y1],channel[:,:,None],axis=2)[:,:,0];after=np.take_along_axis(s1[y0:y1],channel[:,:,None],axis=2)[:,:,0];sensor=np.asarray(raw[y0:y1,:w]);scaled=before.astype(np.float32)*scale[channel];sp=np.clip(np.trunc(scaled),0,65535).astype(np.uint16);totals['scale_predicted_vs_native_mismatch']+=int((sp!=after).sum());totals['scale_above_65535']+=int((scaled>65535).sum());totals['cfa_at_or_above_metadata_white']+=int((sensor>=meta['Maximum']).sum());totals['cfa_matches_before_scale']+=int((sensor==before).sum());out_of_range=((value<0)|(value>65535)).any(axis=1);totals['matrix_out_of_range_pixels']+=int(out_of_range.sum());totals['matrix_out_of_range_with_same_site_scale_clip']+=int((out_of_range & (scaled.reshape(-1)>65535)).sum())
        for key,condition in [('negative',value<0),('overflow',value>65535),('fractional',(value>=0)&(value<=65535)&(value!=np.trunc(value)))]:
            if key in examples or not condition.any():continue
            idx,c=map(int,np.argwhere(condition)[0]); yy=y0+idx//w;xx=idx%w;cx=xx;cy=yy-y0;examples[key]={'x':xx,'y':yy,'channel':c,'cfa_channel':int(channel[cy,cx]),'cfa_value':int(sensor[cy,cx]),'cfa_at_metadata_white':bool(sensor[cy,cx]>=meta['Maximum']),'scale_before':int(before[cy,cx]),'scale_preclip_float':float(scaled[cy,cx]),'scale_after':int(after[cy,cx]),'demosaic_rgb':x[idx].tolist(),'matrix_float':value[idx].tolist(),'matrix_uint16':native[idx].tolist(),'output_uint16':o[i+idx].tolist(),'neighborhood_saturation':'unknown; demosaic depends on neighborhood'}
    return {'candidate':{'version':'MatrixFloatOutputResearchV1','file':None,'sha256':candidate_hash.hexdigest(),'domain':'linear LibRaw ProPhoto D65 matrix float output divided by 65535; can be negative or >1; no tone map; no product integration'},'probe':'MatrixComparisonV1','sourceSha256':meta['sourceSha256'],'dimensions':[w,h],'coordinate':'x right, y down; unrotated raw processing; zero margins for this sample','matrix':m.tolist(),'statistics':totals,'examples':examples,'localBufferSha256':{p.name:sha(p) for p in root.glob('*.u16')},'productBaselineSampleMismatches':meta['productBaselineSampleMismatches'],'reference':{'name':'NumPy','version':np.__version__,'method':'float64 matrix calculation on the same float32 coefficients and demosaic buffer; independent arithmetic check, not independent RAW processing','matureRawEngine':'NOT_RUN'},'stageObservations':meta['stages'],'rawMetadata':{'size':sz,'filters':filters,'black':meta['Black'],'white':meta['Maximum'],'cblack':meta['cblack'],'cameraWb':meta['CamMul'],'scaleMultipliers':scale.tolist()},'scientificLimits':'Metadata white is an operational threshold, not independently verified sensor saturation; no inference from a single CFA pixel about demosaic neighborhood. Double reference uses same coefficients and buffer, not an independent RAW engine.'}
if __name__=='__main__':
    result={'synthetic':selftest()}
    if len(sys.argv)>1:
        if len(sys.argv)!=2:raise SystemExit('Usage: analyze.py [stage_directory]')
        result['natural']=analyze(Path(sys.argv[1]))
    print(json.dumps(result,indent=2,allow_nan=False))






