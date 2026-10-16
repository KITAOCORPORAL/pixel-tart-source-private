import pathlib,sys,importlib.util,json,hashlib
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/photo-engine';sys.path.insert(0,str(A/'python-deps'));import numpy as np,tifffile
from PIL import Image
spec=importlib.util.spec_from_file_location('photo',R/'tools/photo-engine/photo_srgb_workflow.py');m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);dll=pathlib.Path(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/lcms/Library/bin/lcms2.dll');profile=A/'standard-srgb.icc';source=pathlib.Path(r'D:/AI AGENT/kitao-controlled-icc-20261015/artifacts/controlled-icc/raw/TNAN8886.CR3.rt-true.tif');out=A/'photo-suite';out.mkdir(exist_ok=True);reports=[]
reports.append(dict(case='authorizedCanonFullTIFF',**m.run(source,out/'canon-full',dll,profile)))
a=tifffile.imread(source);jpeg=A/'canon-photo.jpg';Image.fromarray(np.rint(a.astype(float)/257).astype(np.uint8)).save(jpeg,quality=100,subsampling=0,icc_profile=profile.read_bytes());reports.append(dict(case='CanonPhotographicJPEGDerivative',**m.run(jpeg,out/'canon-jpeg',dll,profile)))
# Synthetic code truth, not camera calibration.
colors=np.array([[0,0,0],[32768]*3,[65535]*3,[65535,0,0],[0,0,65535],[42000,23000,14000]],np.uint16);test=np.repeat(np.repeat(colors[None,:,:],32,axis=0),32,axis=1);p=A/'truth-srgb.tif';tifffile.imwrite(p,test,photometric='rgb',extratags=[(34675,'B',len(profile.read_bytes()),profile.read_bytes(),False)]);reports.append(dict(case='knownGraySaturatedSkinCodes',**m.run(p,out/'truth',dll,profile)))
# Independent sRGB EOTF oracle vsLCMSworkingfloat.
x=np.ascontiguousarray(test.astype(np.float32)/65535);engine=m.C.CDLL(str(dll));linear=(R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc').read_bytes();work=m.convert(x,profile.read_bytes(),linear,engine);truth=np.where(x.astype(float)<=.04045,x.astype(float)/12.92,((x.astype(float)+.055)/1.055)**2.4);independentMax=float(np.max(np.abs(work-truth)));assert independentMax<.0001
rejections=[]
for name,kwargs in [('missing-profile',{}),('unsupported-linear',{'icc_profile':linear})]:
 p=A/(name+'.jpg');Image.fromarray(np.zeros((16,16,3),np.uint8)).save(p,**kwargs)
 try:m.run(p,out/name,dll,profile);raise AssertionError('Expectedreject')
 except ValueError as e:rejections.append(dict(case=name,reason=str(e)))
(A/'photo-suite-results.json').write_text(json.dumps(dict(cases=reports,independentEotfMaxAbs=independentMax,rejections=rejections),indent=2));print(json.dumps(dict(cases=[dict(case=r['case'],width=r['width'],height=r['height'],precision=r['inputPrecision'],JpegMae=r['JpegMae'],JpegPsnr=r['JpegPsnr'],sinkPixelsOutside=r['sinkPixelsOutside']) for r in reports],independentEotfMaxAbs=independentMax,rejections=rejections),indent=2))
