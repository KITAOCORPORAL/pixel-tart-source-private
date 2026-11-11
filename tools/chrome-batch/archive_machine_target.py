from pathlib import Path
import json,sqlite3,sys,datetime
seed=json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"));target=seed['invalidProject'];root=Path(seed['runtime']);assert root.name.startswith('batch-machine')
with sqlite3.connect(root/'Data/pixel-tart.db')as connection:
 row=connection.execute('SELECT Name,IsArchived FROM Projects WHERE Id=?',(target,)).fetchone();assert row==('待失效测试项目',0)
 changed=connection.execute('UPDATE Projects SET IsArchived=1,UpdatedAt=? WHERE Id=? AND Name=? AND IsArchived=0',(datetime.datetime.now(datetime.timezone.utc).isoformat(),target,'待失效测试项目')).rowcount;assert changed==1
print('Archived only the owned isolated machine target')
