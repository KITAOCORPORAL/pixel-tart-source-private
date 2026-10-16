"""Execute through PrivateWpfHost with KITAO_PRIVATE_HOST_TIMEOUT_SECONDS=2."""
import pathlib,subprocess,sys,time,json,os
p=subprocess.Popen([sys.executable,'-c','import time;time.sleep(90)']);pathlib.Path(sys.argv[1]).write_text(json.dumps({'parent':os.getpid(),'child':p.pid}));time.sleep(90)
