from pathlib import Path
import json,subprocess,sys
root=Path(__file__).resolve().parents[2]/"artifacts/legacy-export-memory-20261122"
cases=[("synthetic-neutral","d07-neutral-oracle","final-neutral-cycle"),("synthetic-points","d07-points-oracle","final-points-cycle")]
reports={}
for label,oracle,cycle in cases:
 record=json.loads((root/cycle/"RESULT.json").read_text());exit=json.loads((root/cycle/"EXIT.json").read_text());assert exit["exitCode"]==0 and exit["stop"] is None
 probes={}
 for name,file in [("before",root/oracle/"oracle.tif"),("after",Path(record["first"])),("reopen",Path(record["second"]))]:
  target=root/(label+"-"+name+"-probe.json");subprocess.run([sys.executable,str(Path(__file__).with_name("probe_tiff.py")),str(file),str(target)],check=True);probes[name]=json.loads(target.read_text())
 checks={k:probes["before"][k]==probes["after"][k]==probes["reopen"][k] for k in ["width","height","bits","orientation","pixelBytes","pixelSha256","icc"]};checks["allSampledResourcesUnderGuard"]=exit["peakPrivateBytes50ms"]<=2**31 and exit["osPeakWorkingSetBytes"]<=2**31
 checks["newRawDecodesZero"]=record["newRawDecodes"]==0;checks["twoReplayReads"]=record["replayReads"]==2
 report={"status":"PASS" if all(checks.values()) else "FAIL","checks":checks,"beforeAuthority":"Original d07 full Core processing without WPF, independent frozen-sample oracle; before WPF cycle stops retained separately.","run":record,"resources":exit,"probes":probes};reports[label]=report
 assert all(checks.values()),(label,checks)
(root/"PIXEL_AND_TIFF_COMPARISON.json").write_text(json.dumps(reports,ensure_ascii=False,indent=2)+"\n",encoding="utf-8");print(json.dumps({name:{"status":r["status"],"peakMiB":round(r["resources"]["peakPrivateBytes50ms"]/1048576,2),"pixelSha":r["probes"]["after"]["pixelSha256"]} for name,r in reports.items()}))
