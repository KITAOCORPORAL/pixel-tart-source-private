"""Home compatibility entry: explicit versioned config; delegates to bounded home gates.
Company historical evidence remains in docs/company-round8. No hardcoded company paths.
"""
import argparse,subprocess,sys
from pathlib import Path
from host_environment import environment
p=argparse.ArgumentParser();p.add_argument('--environment',type=Path,required=True);p.add_argument('--output-prefix',required=True);a=p.parse_args();host=environment(a.environment)
# Controls are in home_small_gates --controls-only to avoid redoing oracles/full.
cmd=[sys.executable,str(Path(__file__).with_name('home_small_gates.py')),'--controls-only','--prefix',a.output_prefix]
import os
env=os.environ.copy();env['COLOR_RESEARCH_ENV']=str(a.environment.resolve());raise SystemExit(subprocess.call(cmd,env=env))
