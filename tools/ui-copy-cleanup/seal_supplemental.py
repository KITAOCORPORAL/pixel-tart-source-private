import pathlib,zipfile,json,hashlib,xml.etree.ElementTree as E
r=pathlib.Path("E:/Kitao-Deliveries/whole-app-ui-copy-cleanup-20261026");out=pathlib.Path("E:/Kitao-Worktrees/whole-app-ui-copy-cleanup-20261026/docs/validation/whole-app-ui-copy-cleanup-20261026");files=[]
with zipfile.ZipFile(out/'SUPPLEMENTAL_ERROR_EVIDENCE.zip','w',zipfile.ZIP_DEFLATED) as z:
 for f in [r/'error-evidence/error.trx',r/'ERROR_EVIDENCE.log']:
  b=f.read_bytes();z.writestr(f.name,b);files.append(dict(path=f.name,sha256=hashlib.sha256(b).hexdigest()))
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};root=E.parse(r/'error-evidence/error.trx').getroot();values=[x.attrib for x in root.findall('.//t:UnitTestResult',ns)];assert len(values)==1 and values[0]['outcome']=='Passed'
(out/'SUPPLEMENTAL_ERROR_RESULT.json').write_text(json.dumps(dict(result=values,scope='Final app-bound representative error render, not added to main WPF200 counts',files=files),indent=2));print('PASS 1')
