import pathlib,sys,json,importlib.util
R=pathlib.Path(__file__).resolve().parents[2];A=R/'artifacts/wpf-icc-gates';sys.path.insert(0,r'D:/AI AGENT/kitao-validation-closure-20261015/artifacts/validation-closure/python-deps');import numpy as np,tifffile
spec=importlib.util.spec_from_file_location('v2',R/'tools/wpf-icc-gates/extended_v2.py');v2=importlib.util.module_from_spec(spec);spec.loader.exec_module(v2)
profile=R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc';work=R/'tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc';display=pathlib.Path(r'E:/Kitao-Worktrees/kitao-icc-reliability-20261015/artifacts/icc-reliability/photo-bounded/display-srgb.icc');dll=pathlib.Path(r'D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/lcms/Library/bin/lcms2.dll');icc=profile.read_bytes();inputs=A/'v2-inputs';inputs.mkdir(exist_ok=True)
scenes={}
gray=np.linspace(-.25,2,257,dtype=np.float32);scenes['gray']=np.repeat(np.tile(gray[None,:,None],(16,1,1)),3,axis=2)
colors=np.array([[1.5,-.1,.2],[0,1.2,0],[2,.1,.1],[.1,.2,2],[1,0,1]],np.float32);scenes['saturated']=np.repeat(np.repeat(colors[None,:,:],32,axis=0),32,axis=1)
skin=np.array([[.65+d,.35+d/2,.22+d/3] for d in np.linspace(-.15,.15,129)],np.float32);scenes['skin-neighbor']=np.repeat(skin[None,:,:],16,axis=0)
high=np.array([[v,v*.9,v*.7] for v in np.linspace(.8,4,129)],np.float32);scenes['highlight']=np.repeat(high[None,:,:],16,axis=0)
reports=[]
for name,data in scenes.items():
 p=inputs/(name+'.tif');tifffile.imwrite(p,data,photometric='rgb',extratags=[(34675,'B',len(icc),icc,False)]);report=v2.run(p,A/('v2-'+name),dll,work,profile,display);reports.append({'case':name,**report})
source=pathlib.Path(r'D:/AI AGENT/kitao-controlled-icc-20261015/artifacts/controlled-icc/canon-linear.tif');reports.append({'case':'authorizedCanonFullRange',**v2.run(source,A/'v2-canon-fullrange',dll,work,profile,display)})
(A/'v2-suite.json').write_text(json.dumps(reports,indent=2));print(json.dumps([{'case':r['case'],'working':r['working'],'beforeMapping':r['beforeMapping'],'pixelsClipped':r['pixelsClipped'],'jpegMae':r['jpegMae'],'jpegPsnr':r['jpegPsnr']} for r in reports],indent=2))
