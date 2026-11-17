"""Serve only the explicitly selected local material directory on loopback."""
from pathlib import Path
from functools import partial
from http.server import SimpleHTTPRequestHandler,ThreadingHTTPServer
import argparse,json,os
p=argparse.ArgumentParser();p.add_argument('--root',required=True,type=Path);p.add_argument('--port',type=int,default=0);p.add_argument('--receipt',required=True,type=Path);a=p.parse_args();root=a.root.resolve()
if not (root/'index.html').is_file() or not (root/'data.json').is_file():raise RuntimeError('Prepared viewer index/data required')
if a.receipt.exists():raise RuntimeError('Use a fresh receipt; preserve prior server identity')
server=ThreadingHTTPServer(('127.0.0.1',a.port),partial(SimpleHTTPRequestHandler,directory=str(root)))
r={'pid':os.getpid(),'root':str(root),'url':'http://127.0.0.1:'+str(server.server_port)+'/','loopbackOnly':True,'newRawDecodes':0};a.receipt.write_text(json.dumps(r,indent=2)+chr(10),encoding='utf-8');print(json.dumps(r),flush=True);server.serve_forever()
