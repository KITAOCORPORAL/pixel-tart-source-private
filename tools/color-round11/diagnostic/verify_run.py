import argparse,json
from pathlib import Path
from package_validation import validate_package
p=argparse.ArgumentParser();p.add_argument('run',type=Path);p.add_argument('--role',choices=['home','company'],required=True);p.add_argument('--allow-sealed-round10',action='store_true');a=p.parse_args();r,v=validate_package(a.run,a.role,a.allow_sealed_round10);print(json.dumps(v,indent=2))
