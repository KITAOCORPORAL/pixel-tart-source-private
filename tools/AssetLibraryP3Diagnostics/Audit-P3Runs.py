"""Read-only index of bounded P3 run roots; never repairs or reseals evidence."""
import argparse
import json
import pathlib

parser = argparse.ArgumentParser()
parser.add_argument("--parent", action="append", required=True)
parser.add_argument("--output", required=True)
args = parser.parse_args()


def read(path):
    if not path.is_file():
        return None
    return json.loads(path.read_text(encoding="utf-8-sig"))


rows = []
for parent in args.parent:
    for root in sorted(pathlib.Path(parent).glob("P3-Automated-Acceptance-20260907-*")):
        manifest = read(root / "run-manifest.json") or {}
        results = [read(path) for path in sorted((root / "logs").glob("app-*.result.json"))]
        completed = [result for result in results if result and result.get("status") == "completed"]
        exits = [read(path) for path in sorted((root / "logs").glob("app-*.process-exit.json"))]
        stages = [{"session": result.get("session_name"), "stages": result.get("execution_wait", {}).get("stages"),
                   "owner_exited": result.get("owner", {}).get("HasExited")}
                  for result in exits if result]
        performance = read(root / "app/evidence/performance/four-view-resilience-layout-v1/primary/aggregate-performance.json")
        validator = []
        for directory in [root / "logs", root.parent / ("P3-Automated-Validator-" + manifest.get("run_id", "NONE"))]:
            for log in directory.glob("*validator*stderr*.log"):
                error = log.read_text(encoding="utf-8-sig", errors="replace").strip()
                validator.append({"path": str(log), "first_error": error[:1600]})
        rows.append({
            "run_root": str(root), "is_symlink_or_junction": root.is_symlink() or root.is_junction(),
            "run_id": manifest.get("run_id", "NOT_FOUND"), "source_head": manifest.get("source_head", "NOT_FOUND"),
            "manifest_status": manifest.get("automated_capture_status", "NOT_FOUND"),
            "seal_present": (root / "runner/run-seal.json").is_file(),
            "completed_primary_scenarios": sum(row.get("phase") == "primary" for row in completed),
            "completed_sessions": len(completed), "normal_zero_exit_sessions": sum(row.get("exit_code") == 0 for row in completed),
            "primary_failure": manifest.get("primary_failure"), "exit_stages": stages,
            "validator_logs": validator,
            "performance": performance or "NOT_FOUND",
            "scope_subphases": "NOT_FOUND", "batch_subphases": "NOT_FOUND",
            "incomplete_reason": "NOT_FOUND: no recorded primary failure" if len(completed) < 17 and not manifest.get("primary_failure") else None,
        })
output = pathlib.Path(args.output)
with output.open("x", encoding="utf-8") as stream:
    json.dump(rows, stream, ensure_ascii=False, indent=2)
for row in rows:
    payload = row["performance"].get("payload", {}) if isinstance(row["performance"], dict) else {}
    print(json.dumps({key: row[key] for key in ("run_id", "source_head", "seal_present", "completed_primary_scenarios", "completed_sessions", "is_symlink_or_junction")}
                     | {"metrics": payload.get("metrics", "NOT_FOUND"), "root": row["run_root"]}, ensure_ascii=False))
