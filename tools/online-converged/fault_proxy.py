"""Local-only fault boundary for actual HTTP acceptance; never ship this in the application."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import datetime, json, socket, sys, threading, urllib.request, urllib.error, uuid
backend, port, root = sys.argv[1], int(sys.argv[2]), Path(sys.argv[3])
root.mkdir(exist_ok=False, parents=True)
(root/"control.json").write_text('{"mode":"none"}')
lock = threading.Lock(); used = set(); blocked = set()
def record(method,path,status,fault=None):
    with lock:
        with (root/"requests.jsonl").open("a",encoding="utf-8") as stream:
            stream.write(json.dumps(dict(timeUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),method=method,path=path,status=status,fault=fault))+"\n")
class Handler(BaseHTTPRequestHandler):
    def log_message(self,*args): pass
    def do_GET(self): self.forward()
    def do_POST(self): self.forward()
    def do_PUT(self): self.forward()
    def do_DELETE(self): self.forward()
    def send(self,status,body,content_type="application/json"):
        self.send_response(status); self.send_header("Content-Type",content_type); self.send_header("Content-Length",str(len(body))); self.send_header("Cache-Control","no-store"); self.send_header("Referrer-Policy","no-referrer"); self.end_headers(); self.wfile.write(body)
    def forward(self):
        mode=json.loads((root/"control.json").read_text())["mode"]
        path=self.path.split("?",1)[0]; marker=(mode,path); fault=None
        if path in blocked:
            self.send(503,b'{"message":"Controlled subsequent network failure"}'); record(self.command,path,503,"after-confirm-view-failure"); return
        data=None
        if self.command in ["POST","PUT"]:
            if self.headers.get("Transfer-Encoding", "").lower()=="chunked":
                chunks=[]; total=0
                while True:
                    size=int(self.rfile.readline().strip().split(b";",1)[0],16)
                    if not size:
                        while self.rfile.readline() not in [b"\r\n",b"\n",b""]: pass
                        break
                    total+=size
                    if total>64*1024*1024: raise ValueError("Bounded test request exceeded")
                    chunks.append(self.rfile.read(size)); assert self.rfile.read(2)==b"\r\n"
                data=b"".join(chunks)
            else:
                data=self.rfile.read(int(self.headers.get("Content-Length","0")))
        if mode=="confirm-wrong-project" and self.command=="POST" and path.endswith("/confirm") and marker not in used:
            used.add(marker); body=json.dumps(dict(projectId=str(uuid.uuid4()),selectionVersion=1,revision=999,isLocked=True,items=[],confirmedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())).encode(); self.send(200,body); record(self.command,path,200,mode); return
        headers={k:v for k,v in self.headers.items() if k.lower() not in ["host","connection","content-length","accept-encoding","transfer-encoding"]}
        try:
            with urllib.request.urlopen(urllib.request.Request(backend.rstrip("/")+self.path,data=data,headers=headers,method=self.command),timeout=30) as response:
                status=response.status; body=response.read(); content_type=response.headers.get("Content-Type","application/json")
        except urllib.error.HTTPError as response:
            status=response.code; body=response.read(); content_type=response.headers.get("Content-Type","application/json")
        except Exception:
            self.send(502,b'{"message":"Controlled upstream unavailable"}'); record(self.command,path,502); return
        if status==200 and mode=="confirm-drop-after-commit" and self.command=="POST" and path.endswith("/confirm") and marker not in used:
            used.add(marker); record(self.command,path,200,mode); self.close_connection=True
            self.connection.shutdown(socket.SHUT_RDWR); self.connection.close(); return
        if status==200 and mode=="final-wrong-project" and path.endswith("/final-selection"):
            value=json.loads(body); value["projectId"]=str(uuid.uuid4()); body=json.dumps(value).encode(); fault=mode
        if status==200 and mode=="block-view-after-confirm" and self.command=="POST" and path.endswith("/confirm"):
            blocked.add(path[:-len("/confirm")]); fault=mode
        self.send(status,body,content_type); record(self.command,path,status,fault)
server=ThreadingHTTPServer(("127.0.0.1",port),Handler)
(root/"READY.json").write_text(json.dumps(dict(endpoint=f"http://127.0.0.1:{port}/",backend=backend,pid=__import__("os").getpid())))
server.serve_forever()
