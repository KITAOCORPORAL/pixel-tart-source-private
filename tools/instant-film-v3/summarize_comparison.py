"""Verify three final audit runs and summarize exact repeated measurements."""
import argparse,hashlib,json,pathlib,statistics,datetime,shutil

def read(p):return json.loads(p.read_text(encoding="utf-8-sig"))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def main():
 a=argparse.ArgumentParser();a.add_argument("--base",type=pathlib.Path,required=True);a.add_argument("--prefix",required=True);a.add_argument("--baseline",type=pathlib.Path,required=True);a.add_argument("--evidence",type=pathlib.Path,required=True);args=a.parse_args();args.evidence.mkdir(exist_ok=True)
 baseline=read(args.baseline);rows=[]
 for n in range(1,4):
  folder=args.base/f"{args.prefix}-run{n}-output";result=read(folder/"FULL_PAGE_RESULT.json");first=read(folder/"FIRST_VISIBLE.json");memory=read(args.base/f"{args.prefix}-run{n}-memory.json");reopen=read(folder/"REOPEN_WORK.json");interaction=read(folder/"INTERACTIONS.json");continuous=read(folder/"CONTINUOUS_WORK.json");lifecycle=read(folder/"LIFECYCLE_WORK.json")
  assert memory["exitCode"]==0 and result["outputSize"]==baseline["outputSize"]
  assert result["previewHash"]==baseline["previewHash"] and result["previewError"]<=result["limit"]
  assert result["regionChanges"]==baseline["regionChanges"]
  assert all(x["sha256"]==y["sha256"] for x,y in zip(result["formats"],baseline["formats"]))
  assert all(x.get("activeMappings",0)==0 for x in interaction)
  assert result["afterDispose"]==0
  record=dict(run=n,first=first,reopenMs=reopen["elapsedMs"],timings=result["timings"],continuous=[dict(weight=x["weight"],elapsedMs=x["elapsedMs"],timings=x["timings"]) for x in continuous],interactions=[dict(label=x["label"],ms=x.get("ms"),activeMappings=x.get("activeMappings"),comparisonUnavailable=x.get("comparisonUnavailable")) for x in interaction],formats=[dict(format=x["format"],sha256=x["sha256"],exportMs=x["exportMs"],inspection=x["receipt"])for x in result["formats"]],memory=dict(durationSeconds=memory["seconds"],samples=len(memory["samples"]),privatePeak=max(x["private"]for x in memory["samples"]),workingSetPeak=max(x["workingSet"]for x in memory["samples"]),lifetimeWorkingSetPeak=max(x["peakWorkingSet"]for x in memory["samples"]),ledgerSamples=[x for x in lifecycle if x["ElapsedMs"]==0]),previewHash=result["previewHash"],previewFullMax=result["previewError"],closedLedger=result["afterDispose"])
  rows.append(record)
  out=args.evidence/f"run{n}";out.mkdir(exist_ok=True)
  for name in ["FULL_PAGE_RESULT.json","FIRST_VISIBLE.json","CONTINUOUS_WORK.json","INTERACTIONS.json","APPLY_WORK.json","REOPEN_WORK.json","LIFECYCLE_WORK.json"]:shutil.copyfile(folder/name,out/name)
  shutil.copyfile(args.base/f"{args.prefix}-run{n}-memory.json",out/"PROCESS_MEMORY_SERIES.json")
 assert len({x["first"]["effectHash"]for x in rows})==1 and len({x["first"]["comparisonHash"]for x in rows})==1
 median=lambda values:statistics.median(values)
 medians={k:median([x["first"][k] for x in rows])for k in ["effectFirstVisibleMs","wpfOffscreenRenderedMs","operationAvailableMs","comparisonAfterDemandMs","comparisonReadyFromApplyMs"]}
 medians.update(reopenMs=median([x["reopenMs"]for x in rows]),privatePeak=median([x["memory"]["privatePeak"]for x in rows]),workingSetPeak=median([x["memory"]["workingSetPeak"]for x in rows]))
 now=datetime.datetime.now(datetime.timezone.utc);cn=datetime.timezone(datetime.timedelta(hours=8));summary=dict(recordedUtc=now.isoformat(),recordedAsiaShanghai=now.astimezone(cn).isoformat(),scope="Three new independent processes on same host and authorized inputs; managed source cache cold at each process; OS file cache not flushed; regression processes may share CPU. WPF offscreen frame, NOT physical mouse/display latency.",baseline=dict(path=str(args.baseline),sha256=sha(args.baseline),applyMs=baseline["applyMs"],outputSize=baseline["outputSize"]),runs=rows,medians=medians,allThreePreviewAndExportsMatchBaseline=True,allMappingsReleased=True)
 (args.evidence/"THREE_RUN_SUMMARY.json").write_text(json.dumps(summary,ensure_ascii=False,indent=2)+"\n",encoding="utf-8");print(json.dumps(medians,indent=2));return 0
if __name__=="__main__":raise SystemExit(main())
