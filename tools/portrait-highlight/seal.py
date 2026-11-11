"""Seal portable reports and curves; private photos, pixels and GPL tools remain local."""
import argparse,json,shutil,subprocess,xml.etree.ElementTree as ET
from pathlib import Path
from prepare_input import sha
REPO=Path(__file__).resolve().parents[2]
def main():
 p=argparse.ArgumentParser();p.add_argument('--local-root',type=Path,required=True);a=p.parse_args();root=a.local_root.resolve();out=REPO/'docs/research/portrait-highlight-20261111';out.mkdir(exist_ok=True)
 mapping={'input-manifest.json':'INPUT_BINDING.json','reference-manifest.json':'REFERENCES.json','baseline-report.json':'BASELINE.json','candidate-report.json':'RESULTS.json','candidate-validation/recipe.json':'CANDIDATE_RECIPE.json','candidate-validation/curves.csv':'CURVES.csv'}
 for source,dest in mapping.items():(out/dest).write_bytes((root/source).read_text().encode())
 test=root/'tests/closure-final.trx';xml=ET.parse(test).getroot();ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};c=xml.find('t:ResultSummary/t:Counters',ns);skipped=[dict(name=x.attrib['testName'],outcome=x.attrib['outcome']) for x in xml.findall('t:Results/t:UnitTestResult',ns) if x.attrib['outcome']!='Passed'];(out/'TESTS.json').write_text(json.dumps(dict(counters=c.attrib,nonPassing=skipped,trxSha256=sha(test),consoleSha256=sha(root/'tests-final-console.txt')),indent=2))
 local=[]
 names=['portrait-crop.rgb16.tif','portrait-encoded.f32','source-srgb.icc','candidate-validation/candidate.rgb16.tif','candidate-validation/portrait-product/candidate-product-rgb16.tif','candidate-validation/portrait-comparison.png','candidate-validation/recipe.json']
 for folder in ['baseline','candidate-validation']:
  for ext in ['*.f32','*.f64']:names += [str(p.relative_to(root)) for p in sorted((root/folder).rglob(ext))]
 for name in names:
  p=root/name;local.append(dict(path=str(p),bytes=p.stat().st_size,sha256=sha(p),git=False))
 (out/'LOCAL_ARTIFACTS.json').write_text(json.dumps(dict(root=str(root),scope='Media and pixel arrays remain local. This file contains hashes and paths only.',files=local),indent=2))
 srcs=[REPO/'tools/PortraitHighlightProbe/Program.cs',REPO/'tools/PortraitHighlightProbe/MaxChannelShoulderV1.cs',REPO/'src/RAWSelectionAssistant.Core/Services/Color/PhotoWideV1/RenderedSrgbExposure.cs',REPO/'src/RAWSelectionAssistant.Core/Services/Projects/ProfessionalRawPipeline.cs',REPO/'src/RAWSelectionAssistant.Core/Services/Projects/ColorStudioToolProcessor.cs']
 srcs+=sorted((REPO/'tools/portrait-highlight').glob('*.py'));(out/'SOURCE_BINDING.json').write_text(json.dumps(dict(baseCommit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=REPO,text=True).strip(),files=[dict(path=p.relative_to(REPO).as_posix(),sha256=sha(p)) for p in srcs],runtime=dict(probeDllSha256=sha(REPO/'tools/PortraitHighlightProbe/bin/Release/net10.0/PortraitHighlightProbe.dll'),coreDllSha256=sha(REPO/'tools/PortraitHighlightProbe/bin/Release/net10.0/RAWSelectionAssistant.Core.dll')),inputDomain='Explicit; no sensor data synthesized from RGB16'),indent=2))
 for path in out.glob('*.json'):path.write_bytes(path.read_text().encode())
 for path in out.glob('*.csv'):path.write_bytes(path.read_text().encode())
 portable=dict(note='Original evidence digests bind local raw bytes; committed UTF-8 copies normalize line endings only.',files=[dict(sourceLocal=source,destination=dest,sourceByteSha256=sha(root/source),portableByteSha256=sha(out/dest)) for source,dest in mapping.items()])
 (out/'PORTABLE_BINDING.json').write_bytes(json.dumps(portable,indent=2).encode())
 print(json.dumps(dict(mediaLocal=len(local),portableFiles=len(mapping),tests=c.attrib),indent=2))
if __name__=='__main__':main()
