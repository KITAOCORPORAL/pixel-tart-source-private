"""Seal existing home-photo evidence without rerunning or reclassifying tests.

Only exact runner-owned files are archived. Runtime data, photos, databases,
profiles and binaries are never recursively swept into the bundle.
"""
import argparse
import collections
import hashlib
import importlib.util
import json
import pathlib
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
EVIDENCE = pathlib.Path("docs/evidence/photo-engine-home-20261016")
SUITES = (
    "02-baseline-core", "02-baseline-wpf", "02-final-core", "02-final-wpf",
    "08-published-core", "08-published-wpf", "08-published-identity",
)
RUN_FILES = ("results.trx", "runsettings.xml", "stdout.log", "stderr.log")
ROOT_FILES = ("summary.json", "discovery.json", "stdout.log", "stderr.log")


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest().upper()


def contained_file(root, relative):
    """Refuse traversal, aliases/reparse points and absent evidence."""
    relative = pathlib.Path(relative)
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError(f"unsafe relative path: {relative}")
    root = root.resolve(strict=True)
    current = root
    for part in relative.parts:
        current = current / part
        if current.is_symlink() or (hasattr(current, "is_junction") and current.is_junction()):
            raise ValueError(f"linked evidence: {relative}")
    if not current.resolve(strict=True).is_relative_to(root) or not current.is_file():
        raise ValueError(f"not a contained evidence file: {relative}")
    return current


def check_suite(summary, discovery):
    planned = [case for unit in summary["executionPlan"] for case in unit["cases"]]
    expected = collections.Counter(case["fullyQualifiedName"] for case in planned)
    results = [row for run in summary["runs"] for row in run["results"]]
    actual = collections.Counter(row["name"] for row in results)
    discovered = collections.Counter(case["fullyQualifiedName"] for case in discovery)
    missing, extra = expected - actual, actual - expected
    if missing or extra or expected - discovered:
        raise ValueError("exact test discovery/result identity mismatch")
    if summary["discovered"] != len(discovery) or summary["selected"] != len(planned):
        raise ValueError("discovery/selection count mismatch")
    if any(run["expected"] != len(run["results"]) or run.get("timeout") for run in summary["runs"]):
        raise ValueError("incomplete or timed-out run")
    counts = collections.Counter(row["outcome"] for row in results)
    if counts != summary["counts"]:
        raise ValueError("outcome counter mismatch")
    return {
        "discovered": len(discovery), "selected": len(planned), "results": len(results),
        "executed": sum(v for k, v in counts.items() if k != "NotExecuted"),
        "counts": dict(counts), "missing": list(missing.elements()),
        "extra": list(extra.elements()), "originalStatus": summary["status"],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=pathlib.Path, required=True,
                        help="New local audit output directory; never overwritten")
    args = parser.parse_args()
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    evidence = ROOT / EVIDENCE
    identity = load(evidence / "07_release/IDENTITY.json")
    publish = pathlib.Path(identity["publishDirectory"])
    files = load(evidence / "07_release/FILES.json")
    for row in files:
        item = contained_file(publish, row["path"])
        if item.stat().st_size != row["bytes"] or digest(item) != row["sha256"].upper():
            raise ValueError(f"release identity mismatch: {row['path']}")
    actual_paths = {item.relative_to(publish).as_posix() for item in publish.rglob("*") if item.is_file()}
    if actual_paths != {pathlib.Path(row["path"]).as_posix() for row in files}:
        raise ValueError("release inventory changed")
    for row in identity["components"]:
        if digest(contained_file(publish, row["name"])) != row["sha256"].upper():
            raise ValueError(f"component mismatch: {row['name']}")
        for key, folder in (("testWpfHash", "08-published-tests/wpf"),
                            ("testCoreHash", "08-published-tests/core"),
                            ("auditHash", "07-audit-runtime/win-x64")):
            if row.get(key):
                dependency = contained_file(ROOT / "artifacts/home-photo" / folder, row["name"])
                if digest(dependency) != row[key].upper() or row[key].upper() != row["sha256"].upper():
                    raise ValueError(f"payload provenance mismatch: {key}/{row['name']}")
    spec = importlib.util.spec_from_file_location("retained_runner", ROOT / "tools/planning-r7/verified_runner.py")
    runner = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(runner)
    archive_paths = set()
    suites = {}
    for suite in SUITES:
        suite_root = ROOT / "artifacts/home-photo" / suite
        summaries = list(suite_root.glob("run-*/summary.json"))
        if len(summaries) != 1:
            raise ValueError(f"ambiguous or absent retained run: {suite}")
        summary_path = summaries[0]
        run_root = summary_path.parent
        summary = load(summary_path)
        discovery = load(run_root / "discovery.json")
        suites[suite] = check_suite(summary, discovery)
        assembly = pathlib.Path(summary["assembly"])
        assembly_matches = digest(assembly) == summary["assemblySha256"].upper()
        suites[suite]["recordedTestAssemblySha256"] = summary["assemblySha256"]
        suites[suite]["currentTestAssemblyMatchesRecorded"] = assembly_matches
        if not assembly_matches and not suite.startswith("02-baseline-"):
            raise ValueError(f"final test assembly changed: {suite}")
        if not assembly_matches:
            suites[suite]["assemblyLimitation"] = "Original baseline assembly overwritten by documented rebuild; recorded discovery/hash/logs retained, original binary not recoverable from current output."
        for name in ROOT_FILES:
            archive_paths.add(contained_file(run_root, name))
        planned_units = {unit["unit"]: unit["cases"] for unit in summary["executionPlan"]}
        for run in summary["runs"]:
            directory = pathlib.Path(run["directory"])
            relative = directory.relative_to(run_root)
            if len(relative.parts) != 1 or not relative.name.isdigit():
                raise ValueError("unexpected runner directory")
            for name in RUN_FILES:
                archive_paths.add(contained_file(run_root, relative / name))
            observed, errors = runner.load_results(directory / "results.trx", planned_units[run.get("executionUnit", run["class"])])
            if observed != run["results"] or any(error != "non-passing or not-executed tests" for error in errors):
                raise ValueError(f"raw TRX/summary identity mismatch: {suite}/{run['class']}")
        suites[suite]["rawTrxReadbackVerified"] = True
        copied = evidence / "02_tests" / f"{suite}-summary.json"
        if copied.exists() and digest(copied) != digest(summary_path):
            raise ValueError(f"portable summary differs from retained original: {suite}")
    for name in ("FIRST_CORE_SUMMARY.json", "FIRST_WPF_SUMMARY.json"):
        suite = "02-baseline-core" if "CORE" in name else "02-baseline-wpf"
        original = next((ROOT / "artifacts/home-photo" / suite).glob("run-*/summary.json"))
        if digest(evidence / "02_tests" / name) != digest(original):
            raise ValueError("first failure record changed")
    focused = contained_file(ROOT / "artifacts/home-photo", "focused/home-monitor-and-race.trx")
    archive_paths.add(focused)
    # Direct tool invocation/build logs only; never recurse into runtime roots.
    archive_paths.update((ROOT / "artifacts/home-photo").glob("*.log"))
    archive_paths.add(contained_file(ROOT / "artifacts/home-photo", "strict-policy-initial-unclassified.json"))
    manifest = []
    archive = output / "RAW_TEST_RESULTS.zip"
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as bundle:
        for path in sorted(archive_paths):
            relative = path.relative_to(ROOT)
            path = contained_file(ROOT, relative)
            name = relative.as_posix()
            manifest.append({"path": name, "bytes": path.stat().st_size, "sha256": digest(path)})
            bundle.write(path, name)
    # Read every member back; ZIP CRC alone is not the identity check.
    with zipfile.ZipFile(archive) as bundle:
        if len(bundle.namelist()) != len(manifest) or bundle.testzip() is not None:
            raise ValueError("archive member count/CRC mismatch")
        for row in manifest:
            data = bundle.read(row["path"])
            if len(data) != row["bytes"] or hashlib.sha256(data).hexdigest().upper() != row["sha256"]:
                raise ValueError("archive content differs from original")
    result = {
        "status": "EVIDENCE_INTEGRITY_VERIFIED_NOT_PRODUCT_ACCEPTANCE",
        "releaseSourceCommit": identity["sourceCommit"], "releaseFilesVerified": len(files),
        "releaseStatusUnchanged": identity["status"], "suites": suites,
        "archive": {"name": archive.name, "sha256": digest(archive),
                    "bytes": archive.stat().st_size, "memberCount": len(manifest)},
        "privacyBoundary": "Exact runner TRX/settings/logs/discovery/summary and direct build logs only; no runtime trees, photos, databases, ICC or binaries.",
    }
    for name, payload in (("EVIDENCE_INTEGRITY.json", result), ("RAW_TEST_RESULTS_MANIFEST.json", manifest)):
        (output / name).write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
