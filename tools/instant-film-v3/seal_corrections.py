"""Seal exact regression evidence without including source photos or product binaries."""
import argparse, collections, hashlib, importlib.util, json, pathlib, re, shutil
import xml.etree.ElementTree as ET
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[2]
NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--delivery", type=pathlib.Path, required=True)
    parser.add_argument("--evidence-dir", type=pathlib.Path)
    parser.add_argument("--baseline", default="0fac23a4f17f137b4118b209f07c964fcc756757")
    parser.add_argument("--linear-chain", action="store_true")
    parser.add_argument("--safety-review", action="store_true")
    parser.add_argument("--preview-reuse", action="store_true")
    parser.add_argument("--comparison-first", action="store_true")
    args = parser.parse_args()
    delivery = args.delivery.resolve()
    evidence = args.evidence_dir.resolve() if args.evidence_dir else ROOT / "docs/research/v3-corrections-colorpage-20261017"
    out = evidence / "06_regression"
    out.mkdir(exist_ok=True)
    runner_spec = importlib.util.spec_from_file_location("verified_runner", ROOT / "tools/planning-r7/verified_runner.py")
    runner = importlib.util.module_from_spec(runner_spec)
    runner_spec.loader.exec_module(runner)
    paths, counts, conditionals = set(), [], []
    for label in ["source-focused"] + (["published-focused"] if (delivery / "published-focused").exists() else []) + ["source-core", "published-core", "candidate", "published-wpf"]:
        matches = list((delivery / label).glob("run-*/summary.json"))
        assert len(matches) == 1, (label, "Ambiguous/missing run")
        file = matches[0]
        data = load(file)
        assert data["status"] != "RUNNING", label
        assert sha(pathlib.Path(data["assembly"])) == data["assemblySha256"].upper(), label
        plan = {row["unit"]: row["cases"] for row in data["executionPlan"]}
        expected = collections.Counter(x["fullyQualifiedName"] for rows in plan.values() for x in rows)
        observed = collections.Counter(x["name"] for run in data["runs"] for x in run["results"])
        assert expected == observed, (label, "Missing or extra tests")
        for run in data["runs"]:
            folder = pathlib.Path(run["directory"])
            values, errors = runner.load_results(folder / "results.trx", plan[run.get("executionUnit", run["class"])])
            assert values == run["results"], (label, "TRX mismatch")
            assert not run["timeout"] and run["exitCode"] == 0, (label, run)
            assert all(error == "non-passing or not-executed tests" for error in errors), (label, errors)
            assert all(value["outcome"] in ["Passed", "NotExecuted"] for value in values), (label, values)
            for value in values:
                if value["outcome"] == "NotExecuted":
                    conditionals.append(dict(suite=label, **value))
            for name in ["results.trx", "runsettings.xml", "stdout.log", "stderr.log"]:
                paths.add(folder / name)
        for name in ["summary.json", "discovery.json", "stdout.log", "stderr.log"]:
            paths.add(file.parent / name)
        measured = collections.Counter(x["outcome"] for run in data["runs"] for x in run["results"])
        counts.append(dict(suite=label, sourceCommit=(delivery / "source-commit.txt").read_text().strip(),
                           assemblySha256=data["assemblySha256"], discovered=data["discovered"], selected=data["selected"],
                           executed=sum(v for k, v in measured.items() if k != "NotExecuted"), counts=dict(measured),
                           missing=[], timeouts=[], runnerStatus=data["status"],
                           interpretation="NO_EXECUTED_FAILURES_WITH_CONDITIONALS" if measured["NotExecuted"] else "PASSED"))
        shutil.copyfile(file, out / (label + "-summary.json"))
        shutil.copyfile(file.parent / "discovery.json", out / (label + "-discovery.json"))
    dpi = delivery / "dpi/CorrectionsDpi.trx"
    tree = ET.parse(dpi)
    values = tree.findall(".//t:UnitTestResult", NS)
    assert values and all(x.get("outcome") == "Passed" for x in values)
    paths.add(dpi)
    counts.append(dict(suite="logical-dpi", discovered=len(values), selected=len(values), executed=len(values),
                       counts={"Passed": len(values)}, missing=[], timeouts=[], interpretation="LOGICAL_ONLY_NOT_PHYSICAL"))
    # Preserve original failures and superseded measurements, without representing them as current green runs.
    artifacts = ROOT / "artifacts/v3-corrections"
    for label in ([] if args.evidence_dir else ["06-source-wpf", "06-latest-source-wpf", "06-final-published-wpf", "06-final-source-core", "06-final-published-core", "07-focused", "07-focused-retry", "07-history"]):
        for summary in (artifacts / label).glob("run-*/summary.json"):
            old = load(summary)
            assert old["status"] != "RUNNING"
            for name in ["summary.json", "discovery.json", "stdout.log", "stderr.log"]:
                file = summary.parent / name
                if file.exists(): paths.add(file)
            for run in old["runs"]:
                for name in ["results.trx", "runsettings.xml", "stdout.log", "stderr.log"]:
                    file = pathlib.Path(run["directory"]) / name
                    if file.exists(): paths.add(file)
    if args.evidence_dir and not args.linear_chain:
        for label in ["01-focused", "01-real-page", "02-focused", "02-second-fixed", "02-sync", "02-sync-fixed", "02-life", "02-final-focused", "04-measure", "04-visual", "04-final-fix", "04-ui-contract"]:
            for old_summary in (ROOT / "artifacts/photo-grade-v3" / label).glob("run-*/summary.json"):
                old = load(old_summary)
                assert old["status"] != "RUNNING"
                paths.update([old_summary, old_summary.parent / "discovery.json"])
                for run in old["runs"]:
                    for name in ["results.trx", "runsettings.xml", "stdout.log", "stderr.log"]:
                        file = pathlib.Path(run["directory"]) / name
                        if file.exists(): paths.add(file)
    if args.evidence_dir and not args.linear_chain:
        for name in ["03-chain-probe-before.json", "03-chain-reproduced.json", "03-chain-reproduced.log", "04-measure-build.log", "04-visual-build.log"]:
            file = ROOT / "artifacts/photo-grade-v3" / name
            if file.exists(): paths.add(file)
    if args.evidence_dir and not args.linear_chain:
        for suite in ["source-focused", "published-focused", "source-core", "published-core", "candidate", "published-wpf"]:
            for previous_summary in (ROOT / "artifacts/photo-grade-v3/04-sealed-delivery" / suite).glob("run-*/summary.json"):
                previous = load(previous_summary)
                assert previous["status"] != "RUNNING"
                for name in ["summary.json", "discovery.json", "stdout.log", "stderr.log"]:
                    file = previous_summary.parent / name
                    if file.exists(): paths.add(file)
                for run in previous["runs"]:
                    for name in ["results.trx", "runsettings.xml", "stdout.log", "stderr.log"]:
                        file = pathlib.Path(run["directory"]) / name
                        if file.exists(): paths.add(file)
    if args.safety_review:
        base=ROOT/"artifacts/safety-review"
        for label in ["03-focused","04-focused","05-focused","06-focused","07-focused","09-focused"]:
            for summary in (base/label).glob("run-*/summary.json"):
                previous=load(summary);assert previous["status"]!="RUNNING"
                for name in ["summary.json","discovery.json","stdout.log","stderr.log"]:
                    file=summary.parent/name
                    if file.exists():paths.add(file)
                for run in previous["runs"]:
                    for name in ["results.trx","runsettings.xml","stdout.log","stderr.log"]:
                        file=pathlib.Path(run["directory"])/name
                        if file.exists():paths.add(file)
        for name in ["10-full-run.log","10-full-memory.json","10-full-bindings.json","10-full-build.log","10-linear-output.log","10-linear-reference.log", "11-full-run.log", "11-full-duration.json", "11-full-bindings.json", "11-full-build.log", "11-final-stages.log", "11-probe-build.log"]:
            file=base/name
            if file.exists():paths.add(file)
    if args.linear_chain:
        base = ROOT / "artifacts/linear-chain-next"
        for label in ["04-focused", "04-focused-fixed", "05-focused", "06-focused"]:
            for summary in (base / label).glob("run-*/summary.json"):
                previous = load(summary); assert previous["status"] != "RUNNING"
                for name in ["summary.json", "discovery.json", "stdout.log", "stderr.log"]:
                    file = summary.parent / name
                    if file.exists(): paths.add(file)
                for run in previous["runs"]:
                    for name in ["results.trx", "runsettings.xml", "stdout.log", "stderr.log"]:
                        file = pathlib.Path(run["directory"]) / name
                        if file.exists(): paths.add(file)
        for name in ["05-full-run.log", "05-full-run-fixed.log", "05-full-build.log", "09-full-run.log", "09-full-memory.json", "09-full-bindings.json", "07-linear-run.log", "08-installed-reference.log"]:
            file = base / name
            if file.exists(): paths.add(file)
    if args.preview_reuse:
        base=ROOT/"artifacts/preview-reuse"
        for label in ["02-focused","03-focused","04-focused","05-risk","06-risk","07-key","08-risk","09-risk","11-risk","13-risk","14-risk","15-risk","17-mapping","19-risk","21-risk"]:
            paths.update((base/label).glob("*.trx"))
        for name in ["01-baseline-memory.json","01-baseline-run.log","10-memory.json","10-run.log","16-memory.json","16-audit-run.log","18-memory.json","18-audit-run.log","18-audit-bindings.json","18-audit-build.log","18-probe-build.log","18-stages.log","18-linear-run.log","18-linear-reference.log","20-memory.json","20-audit-run.log","22-memory.json","22-audit-run.log","22-audit-bindings.json","22-audit-build.log","22-stages.log","22-linear-run.log","22-linear-reference.log"]:
            file=base/name
            if file.exists():paths.add(file)
        for label in ["01-baseline-output","10-output","16-output","18-output","20-output","22-output"]:
            for name in ["FULL_PAGE_RESULT.json","APPLY_WORK.json","REOPEN_WORK.json","CONTINUOUS_WORK.json","LIFECYCLE_WORK.json"]:
                file=base/label/name
                if file.exists():paths.add(file)
        for label in ["12-delivery","16-delivery","18-delivery","20-delivery"]:
            for suite in ["source-focused","published-focused","source-core","published-core","published-wpf","candidate"]:
                for file in (base/label/suite).glob("run-*/summary.json"):
                    paths.add(file)
                    old=load(file)
                    for run in old["runs"]:
                        folder=pathlib.Path(run["directory"])
                        for name in ["results.trx","stdout.log","stderr.log","runsettings.xml"]:
                            if (folder/name).exists():paths.add(folder/name)
    if args.comparison_first:
        base=ROOT/"artifacts/comparison-first"
        for label in ["01-tests","06-tests","07-tests","09-tests","10-tests","12-tests"]:
            paths.update((base/label).glob("*.trx"))
        for name in ["03-hotspots.json","04-hotspots.json","05-hotspots.json","03-profile-build.log","04-profile-build.log","05-profile-build.log","08-memory.json","08-run.log","11-audit-build.log","11-audit-bindings.json","13-audit-build.log","13-audit-bindings.json","13-run-times.json","13-stages.log","13-linear-run.log","13-linear-reference.log"]:
            file=base/name
            if file.exists():paths.add(file)
        for label in ["08-output","11-run1-output"]+[f"13-run{n}-output" for n in range(1,4)]:
            for name in ["FULL_PAGE_RESULT.json","APPLY_WORK.json","REOPEN_WORK.json","CONTINUOUS_WORK.json","LIFECYCLE_WORK.json","FIRST_VISIBLE.json","INTERACTIONS.json"]:
                file=base/label/name
                if file.exists():paths.add(file)
        for n in range(1,4):
            for name in [f"13-run{n}-memory.json",f"13-run{n}.log"]:
                if (base/name).exists():paths.add(base/name)
        for label in ["11-delivery"]:
            for suite in ["source-focused","published-focused","source-core","published-core","published-wpf","candidate"]:
                for file in (base/label/suite).glob("run-*/summary.json"):
                    paths.add(file)
                    for run in load(file)["runs"]:
                        folder=pathlib.Path(run["directory"])
                        for name in ["results.trx","stdout.log","stderr.log","runsettings.xml"]:
                            if (folder/name).exists():paths.add(folder/name)
    paths.update(delivery.glob("*.log"))
    paths.update([delivery / "commands.json", delivery / "bindings.json", delivery / "release-files.json", delivery / "source-commit.txt"])
    for name in ([] if args.evidence_dir else ["07-reference-negative-controls.json", "07-negative-wrong-matrix.log", "07-negative-nonfinite.log",
                 "07-extension-risk.log", "07-extension-risk-fixed.log", "07-extension-build-fixed.log", "07-final-formula-reference.log",
                 "07-final-formula-output.json", "07-final-risk-driver.log", "07-final-risk-driver-retry.log", "07-desktop-readonly.json"]):
        file = artifacts / name
        if file.exists(): paths.add(file)
    if not args.evidence_dir: paths.update([artifacts / "07-final-risk/process-memory.json", artifacts / "07-final-risk/build.log", artifacts / "07-final-risk/run.log", artifacts / "07-final-risk/bindings.json"])
    release = delivery / "release/win-x64"
    inventory = load(delivery / "release-files.json")
    for row in inventory:
        assert sha(release / row["path"]) == row["sha256"]
    assert len(inventory) == len([p for p in release.rglob("*") if p.is_file()])
    shutil.copyfile(delivery / "release-files.json", out / "FINAL_RELEASE_FILES.json")
    shutil.copyfile(delivery / "bindings.json", out / "FINAL_BINDINGS.json")
    archive = out / "RAW_TEST_RESULTS.zip"
    assert not archive.exists(), "Never overwrite a sealed evidence archive"
    members = []
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as zipped:
        for path in sorted(paths):
            assert path.resolve().is_relative_to(ROOT) and not path.is_symlink()
            assert path.suffix.lower() in [".trx", ".json", ".xml", ".log", ".txt"]
            content = path.read_bytes()
            for pattern in [rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", rb"gh[pousr]_[A-Za-z0-9_]{30,}", rb"github_pat_[A-Za-z0-9_]{30,}", rb"Authorization\s*:\s*Bearer\s+\S+"]:
                assert not re.search(pattern, content, re.I), "Sensitive pattern in " + str(path)
            name = path.relative_to(ROOT).as_posix()
            members.append(dict(path=name, bytes=len(content), sha256=hashlib.sha256(content).hexdigest().upper()))
            zipped.writestr(name, content)
    with zipfile.ZipFile(archive) as zipped:
        assert zipped.testzip() is None
        assert len(zipped.namelist()) == len(members)
        for row in members:
            assert hashlib.sha256(zipped.read(row["path"])).hexdigest().upper() == row["sha256"]
    (out / "RAW_TEST_RESULTS_MANIFEST.json").write_text(json.dumps(members, indent=2) + "\n", encoding="utf-8")
    (evidence / "TEST_COUNTS.json").write_text(json.dumps(counts, indent=2) + "\n", encoding="utf-8")
    (out / "CONDITIONAL_NAMES.json").write_text(json.dumps(conditionals, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    identity = dict(status="BLOCKED_DIAGNOSTIC_WITH_PHOTO_PAGE_V3" if args.evidence_dir else "BLOCKED_DIAGNOSTIC_BUILD_WITH_V3_CORRECTIONS", sourceCommit=(delivery / "source-commit.txt").read_text().strip(),
                    reviewedBaseline=args.baseline if args.evidence_dir else "cd6d3ce072948d6739cd789d749c2bfaadffc59e", releaseDirectory=str(release),
                    fileCount=len(inventory), exeSha256=sha(release / "KitaoPhotoSelector.exe"), coreSha256=sha(release / "RAWSelectionAssistant.Core.dll"),
                    physicalDesktop="NOT_RUN", physicalDpi="NOT_RUN", formulaLibrary="RenderedSrgbExposure1_PRODUCTION_ONLY_RENDERED_SRGB" if args.linear_chain else "INDEPENDENT_NOT_WIRED_TO_V3",
                    candidateToolBundled=any("InstantFilmCandidate" in row["path"] for row in inventory))
    assert not identity["candidateToolBundled"]
    (out / "IDENTITY.json").write_text(json.dumps(identity, indent=2) + "\n", encoding="utf-8")
    (out / "SEAL.json").write_text(json.dumps(dict(status="EXACT_TRX_AND_ARCHIVE_READBACK_VERIFIED", archiveSha256=sha(archive),
                      bytes=archive.stat().st_size, members=len(members), scopedPrivacyPatterns="No findings; not a global guarantee", imagesDatabasesBinariesIncluded=False), indent=2) + "\n", encoding="utf-8")
    print(json.dumps(counts, indent=2))
    print("Sealed", len(members), "files", sha(archive))

if __name__ == "__main__":
    main()
