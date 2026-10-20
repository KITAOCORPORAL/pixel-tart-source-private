import pathlib,json,zipfile,hashlib,argparse,shutil
p=argparse.ArgumentParser();p.add_argument('--delivery',required=True);p.add_argument('--package',required=True);p.add_argument('--reports',required=True);a=p.parse_args();d=pathlib.Path(a.delivery).resolve();pkg=pathlib.Path(a.package).resolve();reports=pathlib.Path(a.reports).resolve();manifest=[]
labels=['FULL_CORE','FULL_WPF','FINAL_CORE','FINAL_PUBLISHED_MODULES','FULL_DOCUMENTS','RESEARCH_CANDIDATE_TESTS','FULL_DPI_CONTRACTS','CORE_CLOSABLE_CONDITIONS','REPRO_COMPARISON_FAILURE','FIXED_SPECIALTIES','RECOVERY_AND_DIALOG_SPECIALTIES','FINAL_R3_FRESH_OUTPUT_RECHECK']+['FINAL_WPF_SHARD_'+str(i) for i in range(4)]
allowed={'summary.json','discovery.json','runsettings.xml','stdout.log','stderr.log','results.trx'}
archive=reports/'RAW_TEST_EVIDENCE.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for label in labels:
  for f in sorted((d/label).rglob('*')):
   if not f.is_file() or f.is_symlink() or f.name not in allowed:continue
   # Only driver records and direct per-execution evidence, never runtime/db/photos.
   rel=f.relative_to(d/label)
   if len(rel.parts)>3:continue
   data=f.read_bytes();arc=label+'/'+rel.as_posix();z.writestr(arc,data);manifest.append({'path':arc,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
 for f in sorted(d.glob('*.log')):
  data=f.read_bytes();z.writestr('command-logs/'+f.name,data);manifest.append({'path':'command-logs/'+f.name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
(reports/'RAW_TEST_EVIDENCE_MANIFEST.json').write_text(json.dumps({'files':manifest,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'excludes':'No runtime, db, private photos, generated images, DLLs or secrets'},ensure_ascii=False,indent=2),encoding='utf-8')
for f in reports.iterdir():
 if f.is_file():dest=pkg/'reports'/f.name;dest.parent.mkdir(exist_ok=True);shutil.copy2(f,dest)
zip_path=d/'Guangjing-FullScope-Diagnostic-final-win-x64.zip';files=[]
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for f in sorted(pkg.rglob('*')):
  if not f.is_file() or f.is_symlink():continue
  rel=f.relative_to(pkg)
  if 'UserData' in rel.parts or 'RAWSelectionAssistant.IsolatedRuntime' in rel.parts:continue
  data=f.read_bytes();arc=pkg.name+'/'+rel.as_posix();z.writestr(arc,data);files.append({'path':arc,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
  # Each file is byte-verified below after closing the ZIP.
with zipfile.ZipFile(zip_path) as z:
 for row in files:
  if hashlib.sha256(z.read(row['path'])).hexdigest()!=row['sha256']:raise RuntimeError('zip readback mismatch '+row['path'])
result={'zip':str(zip_path),'bytes':zip_path.stat().st_size,'sha256':hashlib.sha256(zip_path.read_bytes()).hexdigest(),'files':len(files),'verifiedReadback':True,'userDataIncluded':False,'setup':False}
(d/'ZIP_IDENTITY.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');(d/'ZIP_FILES.json').write_text(json.dumps(files,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(result,ensure_ascii=False))
