import pathlib,os,subprocess,json,re,sys,time,xml.etree.ElementTree as ET
root=pathlib.Path.cwd();out=pathlib.Path("E:/Kitao-Deliveries/r55-collage-favorites-20261109")/sys.argv[1];out.mkdir(exist_ok=False)
dotnet=re.search(r'dotnet="([^"]+)"', (root/'tools/whole-app-followup/build_current.py').read_text()).group(1)
env=os.environ.copy();env.update(DOTNET_ROOT=str(pathlib.Path(dotnet).parent),PATH=str(pathlib.Path(dotnet).parent)+os.pathsep+env.get('PATH',''),PIXEL_TART_ISOLATED_RUNTIME='1',PIXEL_TART_ISOLATED_RUNTIME_ROOT=str(out/'runtime'),KITAO_FAVORITES_ACCEPTANCE_ROOT=str(out/'evidence'))
assembly=root/'tests/RAWSelectionAssistant.WpfTests/bin/x64/Release/net10.0-windows10.0.19041.0/RAWSelectionAssistant.WpfTests.dll'
results=[]
for phase in ['Phase1FavoriteViaVisibleMaterialShelf','Phase2RestartFilterFavoriteAddAndSave','Phase3ReopenProjectExportAlphaAndUnfavorite']:
    target=out/phase;target.mkdir();args=[dotnet,'vstest',str(assembly),'/TestCaseFilter:FullyQualifiedName=RAWSelectionAssistant.WpfTests.CanvasFavoriteAppAcceptanceTests.'+phase,'/Logger:trx;LogFileName=results.trx','/ResultsDirectory:'+str(target)]
    with (target/'stdout.log').open('wb') as log:
        start=time.time();p=subprocess.Popen(args,env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW)
        try:code=p.wait(timeout=210)
        except subprocess.TimeoutExpired:p.kill();code=-100
    trx=target/'results.trx';observed=[]
    if trx.exists():
        tree=ET.parse(trx);observed=[dict(x.attrib) for x in tree.findall('.//{*}UnitTestResult')]
    row=dict(phase=phase,args=args,pid=p.pid,exitCode=code,seconds=time.time()-start,results=observed);results.append(row)
    (out/'RECEIPTS.json').write_text(json.dumps(dict(sourceSha=subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),runtime=str(out/'runtime'),results=results),indent=2));print(phase,code,[x.get('outcome') for x in observed],flush=True)
    if code or len(observed)!=1 or observed[0]['outcome']!='Passed':sys.exit(1)
