import argparse,json,shutil,sys
from pathlib import Path
from run_matrix import sha,require
ap=argparse.ArgumentParser();ap.add_argument('checkout',type=Path);ap.add_argument('build',type=Path);ap.add_argument('output',type=Path);a=ap.parse_args();require(not a.output.exists(),'Output exists');shutil.copytree(a.build/'engine/resources',a.output)
for f in (a.checkout/'engine/resources_product/profiles').glob('*.json'):shutil.copy2(f,a.output/'profiles'/f.name)
source=a.checkout/'engine/resources_product/neutral_print_filters.json';target=a.output/'neutral_print_filters.json';filters=json.loads(target.read_text());filters.update(json.loads(source.read_text()));target.write_text(json.dumps(filters,indent=2))
manifest={'version':'SpektraResearchOverlayV1','changed':'Add product data profiles and merge neutral filters; original tracked resources not modified','files':[{'path':str(f.relative_to(a.output)).replace('\\','/'),'sha256':sha(f),'bytes':f.stat().st_size} for f in sorted(a.output.rglob('*')) if f.is_file()]};Path(str(a.output)+'.manifest.json').write_text(json.dumps(manifest,indent=2));print(json.dumps({'files':len(manifest['files']),'output':str(a.output)}))
