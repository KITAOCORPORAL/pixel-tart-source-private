import urllib.request,json,pathlib,concurrent.futures,hashlib
out=pathlib.Path('C:/Users/Admin/AppData/Local/Temp/guangjing-raw-c1-reference-20261027');out.mkdir(exist_ok=True)
urls={
'c1-search':'https://support.captureone.com/api/v2/help_center/articles/search.json?query=ProStandard&per_page=20',
'c1-auto-search':'https://support.captureone.com/api/v2/help_center/articles/search.json?query=AppleScript&per_page=15',
'c1-base-search':'https://support.captureone.com/api/v2/help_center/articles/search.json?query=Base%20Characteristics&per_page=15',
'libraw-api':'https://www.libraw.org/docs/API-datastruct-eng.html',
'rawtherapee-color':'https://raw.githubusercontent.com/RawTherapee/RawTherapee/db575c6690d30ce6428c0e1383bec8b8f1fb8d21/rtengine/iccstore.cc',
'darktable-color':'https://docs.darktable.org/usermanual/development/en/module-reference/processing-modules/input-color-profile/',
'dcamprof-manual':'https://raw.githubusercontent.com/Beep6581/dcamprof/bffb659d6a0af82f7b6b2e6d0006d1824ac4d398/www/dcamprof.html',
'rea-readme':'https://raw.githubusercontent.com/morluto/rea/a95b83c2b06ef713664d6abb3d29c7c149a2ce01/README.md',
}
def one(item):
 name,url=item
 try:
  req=urllib.request.Request(url,headers={'User-Agent':'Guangjing-feasibility-readonly/1.0'})
  with urllib.request.urlopen(req,timeout=35) as f: b=f.read();final=f.url
  p=out/(name+'.txt');p.write_bytes(b)
  return {'id':name,'url':url,'resolved':final,'status':'retrieved','bytes':len(b),'sha256':hashlib.sha256(b).hexdigest(),'local_cache':str(p)}
 except Exception as e:return {'id':name,'url':url,'status':'failed','error':str(e)}
rows=list(concurrent.futures.ThreadPoolExecutor(max_workers=6).map(one,urls.items()))
print(json.dumps(rows,indent=2));(out/'fetch.json').write_text(json.dumps(rows,indent=2))
for name in ['c1-search','c1-auto-search','c1-base-search']:
 p=out/(name+'.txt')
 if p.exists():
  try:
   d=json.loads(p.read_text());print(name,[(a['id'],a['title'],a['html_url']) for a in d.get('results',[])][:15])
  except Exception: pass
