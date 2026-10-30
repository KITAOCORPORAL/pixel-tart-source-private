"""Defer repeated console interrupts until the native worker and owners are safe."""
import signal,threading
from contextlib import contextmanager
@contextmanager
def deferred_interrupts(cancel,events):
    if threading.current_thread() is not threading.main_thread():
        yield;return
    old={}
    def receive(signum,frame):
        events.append(int(signum));cancel.set()
    try:
        for name in ('SIGINT','SIGBREAK','SIGTERM'):
            sig=getattr(signal,name,None)
            if sig is not None:old[sig]=signal.signal(sig,receive)
        yield
    finally:
        for sig,handler in old.items():signal.signal(sig,handler)
def join_native(worker,cancel):
    """Handlers must defer SIGINT/SIGBREAK. Never release native owners before return."""
    while worker is not None and worker.is_alive():
        try:worker.join(.05)
        except KeyboardInterrupt:cancel.set()
