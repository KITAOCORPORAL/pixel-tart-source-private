"""Serve only the selected existing review assets; never opens a browser."""
import argparse,json,os,time,functools,http.server
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('root',type=Path);a=p.parse_args();root=a.root.resolve();assert (root/'index.html').is_file()
handler=functools.partial(http.server.SimpleHTTPRequestHandler,directory=str(root))
try:server=http.server.ThreadingHTTPServer(('127.0.0.1',3920),handler)
except OSError:server=http.server.ThreadingHTTPServer(('127.0.0.1',0),handler)
record={'pid':os.getpid(),'startedUtc':time.time(),'root':str(root),'url':'http://127.0.0.1:'+str(server.server_port)+'/','readOnly':True,'newCandidateRuns':0,'newRawDecodes':0};(root/'LISTEN.json').write_text(json.dumps(record,indent=2)+'\n');server.serve_forever()
