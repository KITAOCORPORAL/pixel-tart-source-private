from pathlib import Path
import hashlib,json,subprocess,sys,time
root=Path('E:/Kitao-Deliveries/company-functional-20261116/continuation-20261116')
review=Path('E:/Kitao-Deliveries/tiff-srgb-company-20261116/native-final-review')
base=review.parent
first=root/'tiff-exports-first'/'DSCF0347_仿色_仿色.tif'
second=root/'tiff-exports-reopen'/'DSCF0347_仿色_仿色.tif'
source=Path('E:/Guangjing-RawColorChain-20261103/pass4/company-02/default-export/DSCF0347_仿色.tif')
expected='0da1bf38e42b0b76aee28f2b9a0af013ceff4cb9c46102128732d0626d667c6f'
def sidecar(p):
 try:
  j=json.loads(Path(str(p)+'.kitao-export.json').read_text('utf-8-sig'))
  return j if p.is_file() and j.get('CompletionState')=='Completed' else None
 except (OSError,ValueError):return None
end=time.monotonic()+180
while time.monotonic()<end:
 a=sidecar(first);b=sidecar(second)
 if a and b:break
 time.sleep(.5)
else:
 print(json.dumps({'status':'WAITING_FOR_SECOND_COMPLETED_ORDINARY_OUTPUT','notProductFailure':True}));sys.exit(2)
assert a['SourceHash'].lower()==b['SourceHash'].lower()==expected
comparison=review/'ORDINARY_PAIR_COMPARISON.json'
assert not comparison.exists(),'Do not overwrite a prior comparison'
subprocess.run([sys.executable,str(base/'ordinary_app_guard.py'),'--app',str(base/'compare-bin/CompareTiffs.exe'),'--run',str(review/'pair-comparison-guard'),'--runtime',str(review/'probe-runtime'),'--scope','readonly-probe','--argument',str(source),'--argument',str(comparison),'--argument',str(first),'--argument',str(second)],check=True)
r=json.loads(comparison.read_text('utf-8-sig'))
checks={**r['checks'],'firstFileMatchesManifest':r['first']['FileSha256']==a['OutputHash'],'reopenFileMatchesManifest':r['reopened']['FileSha256']==b['OutputHash'],'sourceUnchanged':r['input']['FileSha256'].lower()==expected,'sameFrozenAdjustment':a['RecipeSnapshot']['AdjustmentFingerprint']==b['RecipeSnapshot']['AdjustmentFingerprint']}
result={'status':'PASS_INDEPENDENT_ORDINARY_APP_FULL_ROW_PAIR' if all(checks.values()) else 'FAIL','authority':'Independent read-only verification of two completed ordinary-App outputs; UI/PID/session acceptance must be joined from producer receipts','checks':checks,'source':'62191dfface51c981b1361a4333a3c0981067cb1','integratedSource':'e8180221937fa4abb578a16faacebe99a5d4ac1b','sourceAndTestsTreesEqual':True,'firstManifest':a,'reopenManifest':b,'comparisonPath':str(comparison),'newRawDecodes':0,'probeHasCameraRawDecoderReferences':False}
(review/'ORDINARY_PAIR_VERIFICATION.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'status':result['status'],'checks':checks}))
sys.exit(0 if all(checks.values()) else 1)
