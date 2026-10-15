"""Fail-closed VSTest runner. Discovery uses the installed adapter, never source regex.
Every invocation gets a new root; process children and files are never shared with another run.
"""
import argparse,collections,hashlib,json,os,pathlib,subprocess,sys,time,uuid,xml.etree.ElementTree as ET
NS={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def load_results(trx,expected):
    if not trx.is_file():return [],['missing TRX']
    tree=ET.parse(trx);defs={x.attrib['id']:x.find('t:TestMethod',NS).attrib['className'].split(',')[0]+'.'+x.find('t:TestMethod',NS).attrib['name'] for x in tree.findall('.//t:UnitTest',NS)}
    results=[{'id':x.get('testId'),'name':defs.get(x.get('testId'),'UNKNOWN'),'display':x.get('testName'),'outcome':x.get('outcome'),'error':''.join(x.itertext()).strip() if x.get('outcome')!='Passed' else ''} for x in tree.findall('.//t:UnitTestResult',NS)]
    wanted=collections.Counter(x['fullyQualifiedName'] for x in expected);actual=collections.Counter(x['name'] for x in results);errors=[]
    if actual!=wanted:errors.append({'discoveryMismatch':{'missing':list((wanted-actual).elements()),'extra':list((actual-wanted).elements())}})
    counters=tree.find('.//t:Counters',NS)
    if counters is None or int(counters.get('total','-1'))!=len(results):errors.append('TRX counter mismatch')
    if any(x['outcome']!='Passed' for x in results):errors.append('non-passing or not-executed tests')
    return results,errors

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--assembly',required=True);ap.add_argument('--output',required=True);ap.add_argument('--dotnet',default='D:/AI AGENT/.dotnet/dotnet.exe');ap.add_argument('--sdk',default='D:/AI AGENT/.dotnet/sdk/10.0.302');ap.add_argument('--timeout',type=int,default=180);ap.add_argument('--classes',nargs='*');ap.add_argument('--single-process',action='store_true');ap.add_argument('--resource-root',help='Explicit source/resource root; may differ from testhost directory');a=ap.parse_args()
    assembly=pathlib.Path(a.assembly).resolve();repo=pathlib.Path(__file__).resolve().parents[2];root=pathlib.Path(a.output).resolve()/('run-'+time.strftime('%Y%m%d-%H%M%S')+'-'+uuid.uuid4().hex[:8]);root.mkdir(parents=True,exist_ok=False)
    env=os.environ.copy();env['PATH']=str(pathlib.Path(a.dotnet).parent)+os.pathsep+env.get('PATH','');env['DOTNET_ROOT']=str(pathlib.Path(a.dotnet).parent);env['VSTEST_CONNECTION_TIMEOUT']='30'
    if a.resource_root:env['KITAO_TEST_RESOURCE_ROOT']=str(pathlib.Path(a.resource_root).resolve())
    helper=repo/'tools/VerifiedTestRunner/bin/Release/net10.0/VerifiedTestRunner.dll';discovery=root/'discovery.json'
    def run(cmd,folder,timeout,localenv):
        with (folder/'stdout.log').open('wb') as out,(folder/'stderr.log').open('wb') as err:
            proc=subprocess.Popen(cmd,stdout=out,stderr=err,env=localenv,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));timedout=False
            try:code=proc.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                timedout=True
                # Only the process started by this invocation and its descendants.
                subprocess.run(['taskkill','/PID',str(proc.pid),'/T','/F'],stdout=err,stderr=err,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0));code=proc.wait(timeout=20)
        return {'pid':proc.pid,'exitCode':code,'timeout':timedout}
    d=run([a.dotnet,str(helper),str(pathlib.Path(a.sdk)/'vstest.console.dll'),str(assembly),str(discovery)],root,90,env)
    if d['exitCode']!=0 or d['timeout'] or not discovery.exists():raise RuntimeError('Adapter discovery failed: '+str(d))
    tests=json.loads(discovery.read_text(encoding='utf-8-sig'));groups=collections.defaultdict(list)
    for t in tests:groups[t['fullyQualifiedName'].rsplit('.',1)[0]].append(t)
    selected=groups if not a.classes else {k:v for k,v in groups.items() if k in a.classes}
    if not selected or (a.classes and set(a.classes)!=set(selected)):raise RuntimeError('Requested exact classes not discovered')
    if a.single_process:selected={'ALL':sum(selected.values(),[])}
    summary={'root':str(root),'assembly':str(assembly),'assemblySha256':hashlib.sha256(assembly.read_bytes()).hexdigest(),'discovered':len(tests),'selected':sum(map(len,selected.values())),'runs':[],'status':'RUNNING'}
    def write(): (root/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
    write();print(root,flush=True)
    for index,(name,cases) in enumerate(sorted(selected.items())):
        folder=root/f'{index:03}-{name.rsplit(".",1)[-1]}';folder.mkdir(exist_ok=False);temp=folder/'temp';temp.mkdir();local=env.copy();local.update({'TEMP':str(temp),'TMP':str(temp),'PIXEL_TART_ISOLATED_RUNTIME':'1','PIXEL_TART_ISOLATED_RUNTIME_ROOT':str(folder/'runtime')})
        local.update({'KITAO_R7_EVIDENCE':str(folder/'r7-evidence'),'KITAO_RICH_FLOW_EVIDENCE':str(folder/'rich-flow'),'KITAO_PLANNING_COMPANY_SCREENSHOTS':str(folder/'company')})
        filter_='|'.join('FullyQualifiedName='+t for t in sorted(set(c['fullyQualifiedName'] for c in cases)))
        # Long suites may exceed Windows argv; runsettings holds exact disjunction.
        settings=ET.Element('RunSettings');config=ET.SubElement(settings,'RunConfiguration');ET.SubElement(config,'TestCaseFilter').text=filter_;ET.SubElement(config,'ResultsDirectory').text=str(folder)
        ET.ElementTree(settings).write(folder/'runsettings.xml',encoding='utf-8',xml_declaration=True)
        seconds=600 if 'AssetLibraryP1AutomatedEvidenceContractTests' in name or 'AssetLibraryP1GateAEvidenceContractTests' in name or a.single_process else a.timeout
        result=run([a.dotnet,'vstest',str(assembly),'/Settings:'+str(folder/'runsettings.xml'),'/Logger:trx;LogFileName=results.trx'],folder,seconds,local)
        values,errors=load_results(folder/'results.trx',cases)
        if result['exitCode']!=0:errors.append('nonzero child exit')
        if result['timeout']:errors.append('timeout')
        result.update({'class':name,'directory':str(folder),'expected':len(cases),'results':values,'errors':errors});summary['runs'].append(result);write();print(f'{index+1}/{len(selected)} {name} results={len(values)} errors={len(errors)}',flush=True)
    summary['counts']=dict(collections.Counter(v['outcome'] for r in summary['runs'] for v in r['results']));summary['status']='FAILED' if any(r['errors'] for r in summary['runs']) else 'PASSED';write();return 1 if summary['status']=='FAILED' else 0
if __name__=='__main__':sys.exit(main())
