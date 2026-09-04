#!/usr/bin/env python3
"""Clone, rebase, mutate, reseal, and reject every P3 negative evidence case."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import pathlib
import shutil
import sqlite3
import stat
import subprocess
import sys
from typing import Any, Callable


SHA0 = "0" * 64
EXPECTED_REJECTION_FRAGMENTS = {
    "lifecycle-pending-count-missing": ".pending_operation_count is missing or null.",
    "lifecycle-pending-count-string": ".pending_operation_count must be a JSON integer number.",
    "lifecycle-pending-count-negative": ".pending_operation_count violates rule 'observed-nonnegative'",
    "lifecycle-completed-count-nonzero": ".pending_operation_count violates rule 'zero'",
}
TEXT_SUFFIXES = {
    ".json", ".ndjson", ".log", ".txt", ".ps1", ".py", ".md", ".cs", ".xaml", ".csproj"
}


def sha_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha_file(path: pathlib.Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def json_bytes(value: Any, *, pretty: bool = True) -> bytes:
    if pretty:
        return json.dumps(value, ensure_ascii=True, indent=2, separators=(",", ": ")).encode("utf-8")
    return json.dumps(value, ensure_ascii=True, separators=(",", ":")).encode("utf-8")


def write_json(path: pathlib.Path, value: Any, *, pretty: bool = True) -> None:
    path.write_bytes(json_bytes(value, pretty=pretty))


def read_json(path: pathlib.Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def relative(root: pathlib.Path, path: pathlib.Path) -> str:
    return path.resolve().relative_to(root.resolve()).as_posix()


def writable_tree(root: pathlib.Path) -> None:
    for path in [root, *root.rglob("*")]:
        try:
            path.chmod(path.stat().st_mode | stat.S_IWRITE)
        except FileNotFoundError:
            pass


def remove_tree(path: pathlib.Path) -> None:
    if path.exists():
        writable_tree(path)
        shutil.rmtree(path)


def tree_rows(root: pathlib.Path, *, exclude: set[str] | None = None) -> list[dict[str, Any]]:
    excluded = exclude or set()
    rows = []
    for path in sorted((p for p in root.rglob("*") if p.is_file()), key=lambda p: p.as_posix().casefold()):
        rel = relative(root, path)
        if rel in excluded:
            continue
        rows.append({"path": rel, "byte_length": path.stat().st_size, "sha256": sha_file(path)})
    return rows


def rows_hash(rows: list[dict[str, Any]]) -> str:
    text = "\n".join(
        f"{row['path']}|{int(row['byte_length'])}|{row['sha256']}"
        for row in sorted(rows, key=lambda row: str(row["path"]))
    )
    return sha_bytes(text.encode("utf-8"))


def tree_fingerprint(root: pathlib.Path) -> str:
    return rows_hash(tree_rows(root))


def replace_root_text(root: pathlib.Path, old: str, new: str) -> None:
    forms = [
        (old, new),
        (old.replace("\\", "\\\\"), new.replace("\\", "\\\\")),
        (old.replace("\\", "/"), new.replace("\\", "/")),
    ]
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in TEXT_SUFFIXES:
            continue
        try:
            text = path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            continue
        changed = text
        for source, target in forms:
            changed = changed.replace(source, target)
        if changed != text:
            path.write_text(changed, encoding="utf-8", newline="")


def replace_sqlite_roots(root: pathlib.Path, old: str, new: str) -> None:
    for path in root.rglob("*"):
        if not path.is_file() or path.suffix.lower() not in {".db", ".sqlite", ".sqlite3"}:
            continue
        try:
            connection = sqlite3.connect(path)
            tables = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table'")}
            if "AssetItems" in tables:
                columns = {row[1] for row in connection.execute("PRAGMA table_info(AssetItems)")}
                if "SourcePath" in columns:
                    connection.execute("UPDATE AssetItems SET SourcePath=replace(SourcePath,?,?)", (old, new))
                if "NormalizedSourcePath" in columns:
                    connection.execute(
                        "UPDATE AssetItems SET NormalizedSourcePath=replace(NormalizedSourcePath,?,?)",
                        (old.lower(), new.lower()),
                    )
                connection.commit()
            connection.close()
        except sqlite3.DatabaseError:
            try:
                connection.close()
            except Exception:
                pass


def rehash_journal(path: pathlib.Path, kind: str, summary: Any | None = None) -> None:
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    previous = SHA0
    previous_name = "previous_event_hash" if kind == "event" else "previous_summary_hash"
    hash_name = "event_hash" if kind == "event" else "summary_hash"
    if summary is not None:
        rows[-1]["summary"] = summary
    output = []
    for row in rows:
        row.pop(hash_name, None)
        row.pop("record_sha256", None)
        row[previous_name] = previous
        row["previous_record_sha256"] = previous
        digest = sha_bytes(json_bytes(row, pretty=False))
        row[hash_name] = digest
        row["record_sha256"] = digest
        output.append(json_bytes(row, pretty=False).decode("utf-8"))
        previous = digest
    path.write_text("\n".join(output) + "\n", encoding="utf-8", newline="")


def with_record_sha256(value: dict[str, Any]) -> dict[str, Any]:
    value.pop("record_sha256", None)
    value["record_sha256"] = sha_bytes(json_bytes(value, pretty=False))
    return value


def rehash_summary_journal(
    path: pathlib.Path, phase_summaries: dict[str, dict[str, Any]]
) -> None:
    rows = [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
    previous = SHA0
    output = []
    for row in rows:
        session_id = str(row["process_session_id"])
        if session_id not in phase_summaries:
            raise RuntimeError(f"summary journal has no phase summary for process session {session_id}")
        row["summary"] = phase_summaries[session_id]
        row.pop("summary_hash", None)
        row.pop("record_sha256", None)
        row["previous_summary_hash"] = previous
        row["previous_record_sha256"] = previous
        digest = sha_bytes(json_bytes(row, pretty=False))
        row["summary_hash"] = digest
        row["record_sha256"] = digest
        output.append(json_bytes(row, pretty=False).decode("utf-8"))
        previous = digest
    path.write_text("\n".join(output) + "\n", encoding="utf-8", newline="")


def write_lifecycle(path: pathlib.Path, rows: list[dict[str, Any]], *, renumber: bool = False) -> None:
    """Write a valid lifecycle hash chain so mutations reach semantic validation."""
    previous = SHA0
    output = []
    for index, row in enumerate(rows):
        row.pop("record_sha256", None)
        if renumber:
            row["sequence"] = index + 1
        row["previous_record_sha256"] = previous
        digest = sha_bytes(json_bytes(row, pretty=False))
        row["record_sha256"] = digest
        output.append(json_bytes(row, pretty=False).decode("utf-8"))
        previous = digest
    path.write_text("\n".join(output) + "\n", encoding="utf-8", newline="")


def update_binary_snapshot(root: pathlib.Path, snapshot: dict[str, Any]) -> None:
    directory = pathlib.Path(snapshot["directory"])
    rows = []
    for row in snapshot.get("files", []):
        path = directory / pathlib.PurePosixPath(row["path"])
        rows.append({"path": row["path"], "byte_length": path.stat().st_size, "sha256": sha_file(path)})
    snapshot["files"] = rows
    snapshot["file_count"] = len(rows)
    snapshot["tree_sha256"] = rows_hash(rows)


def sqlite_source_metadata(database: pathlib.Path) -> dict[str, Any]:
    connection = sqlite3.connect(database.as_uri() + "?mode=ro&immutable=1", uri=True)
    try:
        rows = connection.execute("SELECT SourcePath FROM AssetItems ORDER BY SourcePath").fetchall()
    finally:
        connection.close()
    paths = [str(pathlib.Path(row[0]).resolve()) for row in rows]
    inside_count = 0
    for source_path in paths:
        try:
            pathlib.Path(source_path).relative_to(database.parent)
            inside_count += 1
        except ValueError:
            pass
    return {
        "count": len(paths),
        "inside_count": inside_count,
        "outside_count": len(paths) - inside_count,
        "sha256": sha_bytes("\n".join(paths).encode("utf-8")),
    }


def update_integrity(root: pathlib.Path) -> None:
    manifest_path = root / "run-manifest.json"
    manifest = read_json(manifest_path)
    manifest["run_root"] = str(root)
    fixture = manifest["fixture"]
    for path_key, hash_key in [
        ("database_path", "database_sha256"),
        ("legacy_database_path", "legacy_database_sha256"),
        ("expectations_path", "expectations_sha256"),
        ("generator_script_path", "generator_script_sha256"),
    ]:
        fixture[hash_key] = sha_file(pathlib.Path(fixture[path_key]))
    fixture["generator_script_byte_length"] = pathlib.Path(fixture["generator_script_path"]).stat().st_size
    process = fixture["generator_process_result"]
    current_source = sqlite_source_metadata(pathlib.Path(fixture["database_path"]))
    legacy_source = sqlite_source_metadata(pathlib.Path(fixture["legacy_database_path"]))
    fixture["source_path_count"] = current_source["count"] + legacy_source["count"]
    fixture["source_paths_inside_fixture_count"] = current_source["inside_count"] + legacy_source["inside_count"]
    fixture["source_paths_outside_fixture_count"] = current_source["outside_count"] + legacy_source["outside_count"]
    fixture["current_source_path_sha256"] = current_source["sha256"]
    fixture["legacy_source_path_sha256"] = legacy_source["sha256"]
    fixture["source_path_tree_sha256"] = sha_bytes(
        f"{current_source['sha256']}\n{legacy_source['sha256']}".encode("ascii")
    )
    fixture["user_source_read_count"] = fixture["source_paths_outside_fixture_count"]
    fixture["user_source_write_count"] = fixture["source_paths_outside_fixture_count"]
    generator_stdout_path = pathlib.Path(process["stdout"])
    generator_metadata = read_json(generator_stdout_path)
    for key in (
        "source_path_count", "source_paths_inside_fixture_count", "source_paths_outside_fixture_count",
        "current_source_path_sha256", "legacy_source_path_sha256", "source_path_tree_sha256",
    ):
        generator_metadata[key] = fixture[key]
    write_json(generator_stdout_path, generator_metadata, pretty=False)
    process["stdout_sha256"] = sha_file(generator_stdout_path)
    process["stderr_sha256"] = sha_file(pathlib.Path(process["stderr"]))
    fixture_directory = pathlib.Path(fixture["directory"])
    generated_rows = []
    for row in fixture["generated_files"]:
        path = fixture_directory / row["path"]
        generated_rows.append({"path": row["path"], "byte_length": path.stat().st_size, "sha256": sha_file(path)})
    fixture["generated_files"] = generated_rows
    fixture["generated_file_count"] = len(generated_rows)
    fixture["generated_tree_sha256"] = rows_hash(generated_rows)
    fixture_manifest_path = pathlib.Path(fixture["fixture_manifest_path"])
    fixture_snapshot = read_json(fixture_manifest_path)
    for key in list(fixture_snapshot):
        fixture_snapshot[key] = fixture[key]
    write_json(fixture_manifest_path, fixture_snapshot)
    fixture["fixture_manifest_sha256"] = sha_file(fixture_manifest_path)
    for session in manifest["sessions"]:
        for path_key, hash_key in [
            ("stdout", "stdout_sha256"), ("stderr", "stderr_sha256"),
            ("executable_path", "executable_sha256"), ("application_path", "application_sha256"),
            ("asset_module_path", "asset_module_sha256"),
            ("process_exit_diagnostic_path", "process_exit_diagnostic_sha256"),
            ("lifecycle_path", "lifecycle_sha256"),
        ]:
            session[hash_key] = sha_file(pathlib.Path(session[path_key]))
        session["process_exit_diagnostic"] = read_json(pathlib.Path(session["process_exit_diagnostic_path"]))
    if "binary_snapshot" in manifest:
        update_binary_snapshot(root, manifest["binary_snapshot"])
    inputs = manifest["acceptance_inputs"]
    input_dir = pathlib.Path(inputs["directory"])
    input_rows = []
    for row in inputs["files"]:
        path = input_dir / row["path"]
        input_rows.append({"path": row["path"], "byte_length": path.stat().st_size, "sha256": sha_file(path)})
    inputs["files"] = input_rows
    inputs["file_count"] = len(input_rows)
    inputs["tree_sha256"] = rows_hash(input_rows)
    safety = manifest.get("safety_measurement", {})
    path_confinement = safety.get("path_confinement")
    if path_confinement:
        for key in (
            "source_path_observation", "source_path_count", "source_paths_inside_fixture_count",
            "source_paths_outside_fixture_count", "source_path_tree_sha256",
        ):
            path_confinement[key] = fixture[key]
        path_confinement["user_source_read_count"] = fixture["user_source_read_count"]
        path_confinement["user_source_write_count"] = fixture["user_source_write_count"]
    for scan_name in ("static_scan_before", "static_scan_after"):
        scan = safety.get(scan_name)
        if not scan:
            continue
        for target in scan.get("targets", []):
            path = pathlib.Path(target["path"])
            target["sha256"] = sha_file(path)
            target["byte_length"] = path.stat().st_size
        scan["snapshot_tree_sha256"] = tree_fingerprint(pathlib.Path(scan["snapshot_root"]))
    database_audit = root / "runner/database-consistency-audit.json"
    if database_audit.is_file():
        audit = read_json(database_audit)
        for scenario in audit.get("scenarios", []):
            for side_name in ("active", "evidence"):
                side = scenario[side_name]
                path = pathlib.Path(side["path"])
                if path.is_file():
                    side["sha256"] = sha_file(path)
        write_json(database_audit, audit)
        manifest["pre_cleanup_database_audit"]["sha256"] = sha_file(database_audit)
        audit_result = manifest["pre_cleanup_database_audit"].get("result", {})
        for path_key, hash_key in (("stdout", "stdout_sha256"), ("stderr", "stderr_sha256")):
            if path_key in audit_result:
                audit_result[hash_key] = sha_file(pathlib.Path(audit_result[path_key]))
    build_path = root / "build-manifest.json"
    build = read_json(build_path)
    for path_key, hash_key in [
        ("executable_path", "executable_sha256"), ("application_path", "application_sha256"),
        ("asset_module_path", "asset_module_sha256"),
    ]:
        build[hash_key] = sha_file(pathlib.Path(build[path_key]))
    if "binary_snapshot" in build:
        update_binary_snapshot(root, build["binary_snapshot"])
    write_json(build_path, build)

    summary_path = root / "app/evidence/summary.json"
    summary = read_json(summary_path)
    for scenario in summary["scenarios"]:
        database = scenario["database"]
        evidence_path = root / pathlib.PurePosixPath(database["path"])
        database["absolute_path"] = str(evidence_path)
        database["sha256"] = sha_file(evidence_path)
    for artifact in summary["artifacts"]:
        artifact["sha256"] = sha_file(root / pathlib.PurePosixPath(artifact["path"]))
    with_record_sha256(summary)
    write_json(summary_path, summary)
    # The last immutable phase summary is the exact published summary. Earlier
    # phase files retain their own cumulative snapshots but all record hashes
    # must be recomputed after rebasing absolute run-owned paths.
    last_session = manifest["sessions"][-1]
    write_json(pathlib.Path(last_session["phase_summary_path"]), summary)
    phase_summaries: dict[str, dict[str, Any]] = {}
    for session in manifest["sessions"]:
        phase_path = pathlib.Path(session["phase_summary_path"])
        phase_summary = with_record_sha256(read_json(phase_path))
        write_json(phase_path, phase_summary)
        session["phase_summary_sha256"] = sha_file(phase_path)
        session["phase_summary_record_sha256"] = phase_summary["record_sha256"]
        phase_summaries[str(session["process_session_id"])] = phase_summary
    rehash_journal(root / "app/evidence/events.ndjson", "event")
    rehash_summary_journal(root / "app/evidence/summary.ndjson", phase_summaries)
    for session in manifest["sessions"]:
        result_path = pathlib.Path(session["result_path"])
        result = read_json(result_path)
        for key in list(result):
            if key != "record_sha256" and key in session:
                result[key] = session[key]
        with_record_sha256(result)
        write_json(result_path, result)
        session["record_sha256"] = result["record_sha256"]
        session["result_sha256"] = sha_file(result_path)
    write_json(manifest_path, manifest)


def reseal(root: pathlib.Path) -> None:
    writable_tree(root)
    seal_path = root / "runner/run-seal.json"
    if seal_path.exists():
        seal_path.unlink()
    manifest = read_json(root / "run-manifest.json")
    rows = tree_rows(root, exclude={"runner/run-seal.json"})
    seal = {
        "schema": "pixel-tart-p3-run-seal/v1",
        "run_root": str(root),
        "run_id": manifest["run_id"],
        "source_head": manifest["source_head"],
        "sealed_at": "negative-proof-reseal",
        "seal_file": "runner/run-seal.json",
        "inventory_excludes_seal_file": True,
        "read_only_required": True,
        "file_count": len(rows),
        "tree_sha256": rows_hash(rows),
        "files": rows,
    }
    write_json(seal_path, seal)
    for path in root.rglob("*"):
        if path.is_file():
            path.chmod(path.stat().st_mode & ~stat.S_IWRITE)


def rebase(root: pathlib.Path, old_root: pathlib.Path) -> None:
    writable_tree(root)
    replace_root_text(root, str(old_root), str(root))
    replace_sqlite_roots(root, str(old_root), str(root))
    update_integrity(root)
    reseal(root)


class Mutator:
    def __init__(self, root: pathlib.Path, name: str):
        self.root, self.name = root, name
        self.changed: set[str] = set()
        self.summary_path = root / "app/evidence/summary.json"
        self.summary = read_json(self.summary_path)
        self.summary_dirty = False

    def mark(self, path: pathlib.Path) -> None:
        self.changed.add(relative(self.root, path))

    def manifest(self, callback: Callable[[dict[str, Any]], None]) -> None:
        path = self.root / "run-manifest.json"
        value = read_json(path); callback(value); write_json(path, value); self.mark(path)

    def sync_fixture_manifest(self, manifest: dict[str, Any]) -> None:
        fixture = manifest["fixture"]
        snapshot_path = pathlib.Path(fixture["fixture_manifest_path"])
        snapshot = {key: fixture[key] for key in read_json(snapshot_path)}
        write_json(snapshot_path, snapshot)
        fixture["fixture_manifest_sha256"] = sha_file(snapshot_path)
        self.mark(snapshot_path)

    def sync_fixture_file_integrity(self) -> None:
        path = self.root / "run-manifest.json"
        manifest = read_json(path)
        fixture = manifest["fixture"]
        fixture["database_sha256"] = sha_file(pathlib.Path(fixture["database_path"]))
        fixture_directory = pathlib.Path(fixture["directory"])
        rows = []
        for row in fixture["generated_files"]:
            target = fixture_directory / row["path"]
            rows.append({"path": row["path"], "byte_length": target.stat().st_size, "sha256": sha_file(target)})
        fixture["generated_files"] = rows
        fixture["generated_file_count"] = len(rows)
        fixture["generated_tree_sha256"] = rows_hash(rows)
        self.sync_fixture_manifest(manifest)
        write_json(path, manifest)
        self.mark(path)

    def sync_database_audit_hash(self) -> None:
        path = self.root / "run-manifest.json"
        manifest = read_json(path)
        audit_path = self.root / "runner/database-consistency-audit.json"
        manifest["pre_cleanup_database_audit"]["sha256"] = sha_file(audit_path)
        write_json(path, manifest)
        self.mark(path)

    def sync_lifecycle_binding(
        self, manifest: dict[str, Any], index: int, lifecycle_path: pathlib.Path
    ) -> None:
        """Keep every independent integrity binding valid so semantic lifecycle guards run."""
        session = manifest["sessions"][index]
        session["lifecycle_sha256"] = sha_file(lifecycle_path)
        result_path = pathlib.Path(session["result_path"])
        result = read_json(result_path)
        result["lifecycle_sha256"] = session["lifecycle_sha256"]
        with_record_sha256(result)
        write_json(result_path, result)
        session["record_sha256"] = result["record_sha256"]
        session["result_sha256"] = sha_file(result_path)
        manifest_path = self.root / "run-manifest.json"
        write_json(manifest_path, manifest)
        self.mark(lifecycle_path)
        self.mark(result_path)
        self.mark(manifest_path)

    def lifecycle(self, callback: Callable[[list[dict[str, Any]]], None], index: int = 0,
                  *, renumber: bool = False) -> None:
        manifest_path = self.root / "run-manifest.json"
        manifest = read_json(manifest_path)
        session = manifest["sessions"][index]
        path = pathlib.Path(session["lifecycle_path"])
        rows = [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines() if line.strip()]
        callback(rows)
        write_lifecycle(path, rows, renumber=renumber)
        self.sync_lifecycle_binding(manifest, index, path)

    def process_exit(self, callback: Callable[[dict[str, Any]], None], index: int = 0) -> None:
        manifest_path = self.root / "run-manifest.json"
        manifest = read_json(manifest_path)
        session = manifest["sessions"][index]
        path = pathlib.Path(session["process_exit_diagnostic_path"])
        diagnostic = read_json(path)
        callback(diagnostic)
        write_json(path, diagnostic)
        session["process_exit_diagnostic"] = diagnostic
        session["process_exit_diagnostic_sha256"] = sha_file(path)
        write_json(manifest_path, manifest)
        self.mark(path)
        self.mark(manifest_path)

    def phase_summary_path(self, index: int = 0) -> pathlib.Path:
        manifest = read_json(self.root / "run-manifest.json")
        return pathlib.Path(manifest["sessions"][index]["phase_summary_path"])

    def sync_phase_summary_binding(self, index: int = 0, *, sync_record_alias: bool) -> None:
        manifest_path = self.root / "run-manifest.json"
        manifest = read_json(manifest_path)
        session = manifest["sessions"][index]
        phase_path = pathlib.Path(session["phase_summary_path"])
        session["phase_summary_sha256"] = sha_file(phase_path)
        if sync_record_alias:
            session["phase_summary_record_sha256"] = read_json(phase_path)["record_sha256"]
        result_path = pathlib.Path(session["result_path"])
        result = read_json(result_path)
        result["phase_summary_sha256"] = session["phase_summary_sha256"]
        result["phase_summary_record_sha256"] = session["phase_summary_record_sha256"]
        with_record_sha256(result)
        write_json(result_path, result)
        session["record_sha256"] = result["record_sha256"]
        session["result_sha256"] = sha_file(result_path)
        write_json(manifest_path, manifest)
        self.mark(phase_path)
        self.mark(result_path)
        self.mark(manifest_path)

    def artifact(self, file_name: str, callback: Callable[[dict[str, Any]], None]) -> None:
        matches = [a for a in self.summary["artifacts"] if pathlib.PurePosixPath(a["path"]).name == file_name]
        if len(matches) != 1:
            raise RuntimeError(f"artifact {file_name!r} is absent or ambiguous ({len(matches)})")
        artifact = matches[0]
        path = self.root / pathlib.PurePosixPath(artifact["path"])
        value = read_json(path); callback(value); write_json(path, value); self.mark(path)
        artifact["sha256"] = sha_file(path); self.summary_dirty = True

    def artifact_for(self, scenario: str, predicate: str) -> dict[str, Any]:
        for artifact in self.summary["artifacts"]:
            if artifact.get("scenario_id") != scenario:
                continue
            path = self.root / pathlib.PurePosixPath(artifact["path"])
            if path.suffix.lower() != ".json":
                continue
            value = read_json(path)
            if value.get("payload", {}).get("predicate_variant") == predicate:
                return artifact
        raise RuntimeError(f"predicate artifact not found: {scenario}/{predicate}")

    def remove_predicate(self, scenario: str, predicate: str) -> None:
        artifact = self.artifact_for(scenario, predicate)
        path = self.root / pathlib.PurePosixPath(artifact["path"])
        path.unlink(); self.mark(path)
        self.summary["artifacts"].remove(artifact); self.summary_dirty = True

    def finish(self) -> list[str]:
        if self.summary_dirty:
            write_json(self.summary_path, self.summary); self.mark(self.summary_path)
            journal = self.root / "app/evidence/summary.ndjson"
            rehash_journal(journal, "summary", self.summary); self.mark(journal)
        reseal(self.root); self.mark(self.root / "runner/run-seal.json")
        return sorted(self.changed)


def mutate(root: pathlib.Path, name: str) -> list[str]:
    writable_tree(root)
    m = Mutator(root, name)
    zero = SHA0
    payload = lambda value: value["payload"]
    if name == "missing-screenshot":
        artifact = next(a for a in m.summary["artifacts"] if "screenshot" in a["kind"])
        path = root / pathlib.PurePosixPath(artifact["path"]); path.unlink(); m.mark(path)
    elif name == "mutated-hash":
        artifact = m.summary["artifacts"][0]; path = root / pathlib.PurePosixPath(artifact["path"])
        path.write_bytes(path.read_bytes() + b"negative"); m.mark(path)
    elif name == "wrong-scenario-order":
        m.summary["scenarios"][0], m.summary["scenarios"][1] = m.summary["scenarios"][1], m.summary["scenarios"][0]; m.summary_dirty = True
    elif name == "wrong-restart-order":
        m.manifest(lambda x: x["sessions"].__setitem__(slice(-3, None), [x["sessions"][-2], x["sessions"][-3], x["sessions"][-1]]))
    elif name == "fixture-count-mismatch": m.manifest(lambda x: x["fixture"].__setitem__("total_count", 10129))
    elif name in {"fixture-content-hash-mismatch", "fixture-schema-marker-mismatch"}:
        manifest = read_json(root / "run-manifest.json"); db = pathlib.Path(manifest["fixture"]["database_path"])
        connection = sqlite3.connect(db)
        if name == "fixture-content-hash-mismatch": connection.execute("UPDATE AssetItems SET ContentHash=? WHERE rowid=(SELECT min(rowid) FROM AssetItems)", (zero,))
        else: connection.execute("UPDATE AssetLibrarySchemaInfo SET Version=6")
        connection.commit(); connection.close(); m.mark(db)
        m.sync_fixture_file_integrity()
    elif name == "fixture-path-escape":
        def escape_fixture(x: dict[str, Any]) -> None:
            x["fixture"]["database_path"] = str(root.parent / "escaped.db")
            m.sync_fixture_manifest(x)
        m.manifest(escape_fixture)
    elif name == "legacy-fixture-missing":
        manifest = read_json(root / "run-manifest.json"); path = pathlib.Path(manifest["fixture"]["legacy_database_path"]); path.unlink(); m.mark(path)
    elif name == "duplicate-automation-id": m.artifact("dpi-1366x768-100.bounds.json", lambda x: payload(x)["elements"][1].__setitem__("identity", payload(x)["elements"][0]["identity"]))
    elif name == "canonical-query-hash-mismatch": m.artifact("scope-all-query.json", lambda x: payload(x).__setitem__("canonical_sha256", zero))
    elif name == "query-result-hash-mismatch": m.artifact("scope-all-results.json", lambda x: payload(x).__setitem__("asset_id_sha256", zero))
    elif name == "query-plan-parameter-mismatch": m.artifact("scope-parameterized-plan.json", lambda x: payload(x).__setitem__("parameter_count", payload(x)["parameter_count"] + 1))
    elif name == "unparameterized-sql": m.artifact("scope-parameterized-plan.json", lambda x: payload(x).__setitem__("unparameterized_sql_count", 1))
    elif name == "scope-result-mismatch": m.artifact("scope-smart-folder-current-results.json", lambda x: payload(x).__setitem__("viewmodel_oracle_match", False))
    elif name == "stale-cancelled-query": m.artifact("ime-cancellation-query.json", lambda x: payload(x).__setitem__("cancelled_query_generation_published", True))
    elif name == "search-history-not-persisted": m.artifact("history-restart.json", lambda x: payload(x).__setitem__("persisted_after_restart", False))
    elif name == "folder-any-all-not-mismatch": m.remove_predicate("folder-any-all-not/v1", "not")
    elif name == "tag-any-all-not-mismatch": m.remove_predicate("tag-any-all-not/v1", "not")
    elif name == "scalar-null-mismatch": m.remove_predicate("scalar-null-composition/v1", "not-null")
    elif name == "visual-query-mismatch": m.remove_predicate("visual-composition/v1", "not-analyzed")
    elif name == "nested-query-mismatch": m.artifact("nested-eight-rule-query.json", lambda x: payload(x).__setitem__("rule_count", 7))
    elif name == "invalid-query-expanded": m.artifact("invalid-reference-results.json", lambda x: payload(x).__setitem__("fail_closed", False))
    elif name == "smart-folder-roundtrip-mismatch": m.artifact("smart-folder-lifecycle.json", lambda x: payload(x).__setitem__("canonical_roundtrip", False))
    elif name == "smart-folder-invalid-ref-expanded": m.artifact("smart-folder-migration.json", lambda x: payload(x).__setitem__("invalid_reference_fail_closed", False))
    elif name == "smart-folder-migration-mismatch": m.artifact("smart-folder-migration.json", lambda x: payload(x).__setitem__("migrated_schema_version", 6))
    elif name == "tag-merge-membership-duplicate": m.artifact("tag-manager-lifecycle.json", lambda x: payload(x).__setitem__("merge_duplicate_membership_count", 1))
    elif name == "tag-group-cycle-accepted": m.artifact("tag-manager-lifecycle.json", lambda x: payload(x).__setitem__("group_cycle_rejected", False))
    elif name == "batch-partial-commit": m.artifact("batch-500.json", lambda x: payload(x).__setitem__("atomic", False))
    elif name == "journal-chain-mismatch":
        path = root / "app/evidence/events.ndjson"; lines = path.read_text(encoding="utf-8").splitlines(); row = json.loads(lines[1]); row["previous_event_hash"] = zero; lines[1] = json_bytes(row, pretty=False).decode(); path.write_text("\n".join(lines)+"\n", encoding="utf-8"); m.mark(path)
    elif name == "lifecycle-chain-mismatch":
        manifest = read_json(root / "run-manifest.json")
        path = pathlib.Path(manifest["sessions"][0]["lifecycle_path"])
        lines = path.read_text(encoding="utf-8").splitlines()
        row = json.loads(lines[1]); row["previous_record_sha256"] = zero
        row.pop("record_sha256", None)
        row["record_sha256"] = sha_bytes(json_bytes(row, pretty=False))
        lines[1] = json_bytes(row, pretty=False).decode()
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        m.sync_lifecycle_binding(manifest, 0, path)
    elif name == "missing-completion-handshake":
        m.process_exit(lambda x: x["timeline"].__setitem__(
            slice(None), [row for row in x["timeline"] if row.get("event") != "completion-handshake-observed"]))
    elif name == "lifecycle-state-regression":
        def regress_state(rows: list[dict[str, Any]]) -> None:
            row = next(item for item in rows if item["event"] == "application-async-dispose-completed")
            row["result"] = "started"
            row["pending"] = True
            row["pending_operation_count"] = 1
        m.lifecycle(regress_state)
    elif name == "lifecycle-pending-count-missing":
        m.lifecycle(lambda rows: next(
            item for item in rows if item["event"] == "page-dispose-start").pop("pending_operation_count"))
    elif name == "lifecycle-pending-count-string":
        m.lifecycle(lambda rows: next(
            item for item in rows if item["event"] == "page-dispose-start").__setitem__("pending_operation_count", "0"))
    elif name == "lifecycle-pending-count-negative":
        m.lifecycle(lambda rows: next(
            item for item in rows if item["event"] == "page-dispose-start").__setitem__("pending_operation_count", -1))
    elif name == "lifecycle-completed-count-nonzero":
        m.lifecycle(lambda rows: next(
            item for item in rows if item["event"] == "page-dispose-completed").__setitem__("pending_operation_count", 1))
    elif name == "lifecycle-duplicate-transition":
        def duplicate_transition(rows: list[dict[str, Any]]) -> None:
            index = next(i for i, item in enumerate(rows) if item["event"] == "page-dispose-completed")
            rows[index]["event"] = rows[index - 1]["event"]
        m.lifecycle(duplicate_transition)
    elif name == "lifecycle-run-id-mismatch":
        m.lifecycle(lambda rows: rows[0].__setitem__("run_id", "spliced-lifecycle-run"))
    elif name == "lifecycle-session-id-mismatch":
        m.lifecycle(lambda rows: rows[0].__setitem__("process_session_id", "f" * 32))
    elif name == "lifecycle-source-head-mismatch":
        m.lifecycle(lambda rows: rows[0].__setitem__("source_head", "f" * 40))
    elif name == "lifecycle-binary-hash-mismatch":
        m.lifecycle(lambda rows: rows[0].__setitem__("executable_sha256", zero))
    elif name == "partial-phase-summary":
        path = m.phase_summary_path()
        content = path.read_bytes()
        if not content:
            raise RuntimeError("phase summary is unexpectedly empty before truncation")
        path.write_bytes(content[:-1])
        m.sync_phase_summary_binding(sync_record_alias=False)
    elif name == "phase-summary-record-hash-mismatch":
        path = m.phase_summary_path()
        summary = read_json(path)
        summary["record_sha256"] = zero
        write_json(path, summary)
        m.sync_phase_summary_binding(sync_record_alias=True)
    elif name == "forced-cleanup-false-success":
        def false_success(diagnostic: dict[str, Any]) -> None:
            diagnostic["outcome"] = "completed"
            diagnostic["forced_cleanup"]["required"] = True
            diagnostic["forced_cleanup"]["started"] = True
            diagnostic["forced_cleanup"]["owner_identity_verified"] = True
            diagnostic["forced_cleanup"]["kill_requested_through_retained_handle"] = True
            diagnostic["forced_cleanup"]["process_exit_observed"] = True
            diagnostic["forced_cleanup"]["completed"] = True
        m.process_exit(false_success)
    elif name == "missing-exit-code":
        def remove_exit_code(manifest: dict[str, Any]) -> None:
            manifest["sessions"][0].pop("exit_code", None)
            diagnostic = manifest["sessions"][0]["process_exit_diagnostic"]
            diagnostic["timeline"] = [
                row for row in diagnostic["timeline"] if row.get("event") != "exit-code-observed"
            ]
            diagnostic_path = pathlib.Path(manifest["sessions"][0]["process_exit_diagnostic_path"])
            write_json(diagnostic_path, diagnostic)
            manifest["sessions"][0]["process_exit_diagnostic_sha256"] = sha_file(diagnostic_path)
            m.mark(diagnostic_path)
        m.manifest(remove_exit_code)
    elif name == "missing-on-exit-completed":
        m.lifecycle(
            lambda rows: rows.__setitem__(
                slice(None), [row for row in rows if row.get("event") != "application-on-exit-completed"]),
            renumber=True)
    elif name == "lifecycle-timing-regression":
        def regress_timing(rows: list[dict[str, Any]]) -> None:
            index = next(i for i, item in enumerate(rows) if item["event"] == "page-dispose-completed")
            rows[index]["timestamp_utc"] = "2000-01-01T00:00:00.0000000+00:00"
            rows[index]["stopwatch_elapsed_ms"] = -1.0
        m.lifecycle(regress_timing)
    elif name == "lifecycle-old-root-splice":
        def splice_old_root(manifest: dict[str, Any]) -> None:
            session = manifest["sessions"][0]
            source = pathlib.Path(session["lifecycle_path"])
            old_path = root.parent / "old-run-root" / "app" / "evidence" / source.name
            old_path.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, old_path)
            session["lifecycle_path"] = str(old_path)
        m.manifest(splice_old_root)
    elif name == "undo-redo-mismatch": m.artifact("batch-500.json", lambda x: payload(x).__setitem__("redo_passed", False))
    elif name == "restart-identity-reused":
        row = next(s for s in m.summary["scenarios"] if s["id"] == "search-suggestions-history/v1"); row["restart_pid"] = row["pid"]; m.summary_dirty = True
    elif name == "view-result-divergence": m.artifact("four-view-result-stability.json", lambda x: payload(x)["views"][1].__setitem__("result_sha256", zero))
    elif name == "selection-hash-divergence": m.artifact("four-view-result-stability.json", lambda x: payload(x)["views"][1].__setitem__("selection_sha256", zero))
    elif name == "dpi-overflow": m.artifact("dpi-1366x768-100.bounds.json", lambda x: payload(x).__setitem__("has_overflow", True))
    elif name == "contrast-threshold-failed": m.artifact("live-button-state-matrix.json", lambda x: payload(x).__setitem__("contrast_passed", False))
    elif name == "accessibility-identity-missing": m.artifact("dpi-1366x768-100.bounds.json", lambda x: payload(x)["elements"][0].__setitem__("identity", ""))
    elif name in {"performance-threshold-exceeded", "ui-block-exceeded"}:
        key = "first_screen_10000" if name.startswith("performance") else "ui_block"
        m.artifact("aggregate-performance.json", lambda x: payload(x)["metrics"].__setitem__(key, 999999))
    elif name in {"user-source-write", "permanent-delete"}:
        field = "user_source_write_count" if name.startswith("user") else "permanent_delete_count"
        def safety(x: dict[str, Any]) -> None:
            x["safety"][field] = 1; x["safety_measurement"]["path_confinement"][field] = 1
        m.manifest(safety)
    elif name == "safety-counter-null":
        m.manifest(lambda x: x["safety"].__setitem__("desktop_input_injection_count", None))
    elif name in {"eagle-write", "network-upload"}:
        rule = "eagle_io" if name.startswith("eagle") else "network_upload"
        token = "// Eagle.exe\n" if rule == "eagle_io" else "// HttpClient\n"
        def unsafe(x: dict[str, Any]) -> None:
            scans = x["safety_measurement"]
            target = pathlib.Path(scans["static_scan_before"]["targets"][0]["path"])
            target.write_text(target.read_text(encoding="utf-8") + token, encoding="utf-8"); m.mark(target)
            for scan_name in ("static_scan_before", "static_scan_after"):
                scan = scans[scan_name]; scan["targets"][0]["sha256"] = sha_file(target); scan["targets"][0]["byte_length"] = target.stat().st_size
                scan["snapshot_tree_sha256"] = tree_fingerprint(pathlib.Path(scan["snapshot_root"]))
                next(r for r in scan["rules"] if r["rule_id"] == rule)["match_count"] += 1
            if rule == "eagle_io": x["safety"]["eagle_read_count"] = 1; x["safety"]["eagle_write_count"] = 1
            else:
                for field in ("network_upload_count", "third_party_upload_count", "ai_upload_count", "mcp_upload_count"): x["safety"][field] = 1
        m.manifest(unsafe)
    elif name == "residual-process":
        def residual(x: dict[str, Any]) -> None:
            x["process_cleanup"]["devpreview_get_process_count_after"] = 1; x["safety_measurement"]["process_observation"]["devpreview_get_process_count_after"] = 1
        m.manifest(residual)
    elif name == "database-not-v7":
        path = root / "runner/database-consistency-audit.json"
        audit = read_json(path)
        audit["scenarios"][0]["active"]["schema_version"] = 6
        write_json(path, audit)
        m.mark(path)
        m.sync_database_audit_hash()
    elif name == "cross-run-splice": m.summary.__setitem__("run_id", "spliced-run"); m.summary_dirty = True
    elif name == "runner-session-splice": m.manifest(lambda x: x["sessions"][1].__setitem__("process_session_id", x["sessions"][0]["process_session_id"]))
    elif name == "process-session-splice": m.summary["artifacts"][0].__setitem__("process_session_id", "f" * 32); m.summary_dirty = True
    elif name == "binary-hash-mismatch":
        build = read_json(root / "build-manifest.json"); path = pathlib.Path(build["executable_path"]); path.write_bytes(path.read_bytes()+b"negative"); m.mark(path)
    elif name == "input-tree-mutated":
        path = root / "runner/acceptance-inputs/README.md"; path.write_text(path.read_text(encoding="utf-8")+"\nnegative mutation\n", encoding="utf-8"); m.mark(path)
    else: raise RuntimeError(f"unknown negative mutation: {name}")
    return m.finish()


def run_validator(root: pathlib.Path) -> subprocess.CompletedProcess[str]:
    validator = root / "runner/acceptance-inputs/Test-P3AssetLibraryAutomatedEvidence.ps1"
    return subprocess.run(
        ["powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(validator), "-RunRoot", str(root), "-SkipNegativeProofs"],
        text=True, capture_output=True, encoding="utf-8", errors="replace", timeout=600,
    )


def baseline_result(result: subprocess.CompletedProcess[str]) -> dict[str, Any]:
    if result.returncode != 0:
        raise RuntimeError(f"rebased negative baseline did not validate: {result.stdout}\n{result.stderr}")
    try:
        payload = json.loads(result.stdout)
    except json.JSONDecodeError as exception:
        raise RuntimeError(f"rebased negative baseline did not return JSON: {result.stdout}") from exception
    if (payload.get("schema") != "pixel-tart-p3-automated-validation-result/v1" or
            payload.get("status") != "passed-negative-baseline" or
            payload.get("negative_proofs_skipped") is not True or
            payload.get("negative_fixture_proof_count") != 0):
        raise RuntimeError(f"recursive negative baseline returned an invalid non-release result: {result.stdout}")
    return payload


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-root", required=True)
    parser.add_argument("--workspace", required=True)
    parser.add_argument("--names-json", required=True)
    args = parser.parse_args()
    original = pathlib.Path(args.run_root).resolve(); workspace = pathlib.Path(args.workspace).resolve()
    if workspace == original or original in workspace.parents or workspace in original.parents:
        raise RuntimeError("negative proof workspace must be a sibling outside the sealed run root")
    names = json.loads(args.names_json)
    remove_tree(workspace); workspace.mkdir(parents=True)
    mutant, golden = workspace / "mutant", workspace / "golden"
    try:
        shutil.copytree(original, mutant); rebase(mutant, original)
        baseline = run_validator(mutant)
        baseline_result(baseline)
        shutil.copytree(mutant, golden)
        proofs = []
        for index, name in enumerate(names):
            if index:
                remove_tree(mutant); shutil.copytree(golden, mutant)
            changed = mutate(mutant, name)
            result = run_validator(mutant)
            if result.returncode == 0:
                raise RuntimeError(f"negative mutation was accepted: {name}")
            output = (result.stdout + "\n" + result.stderr).strip()
            if "P3 automated evidence rejected" not in output:
                raise RuntimeError(f"negative mutation failed outside the validator contract: {name}: {output}")
            expected_fragment = EXPECTED_REJECTION_FRAGMENTS.get(name)
            if expected_fragment is not None and expected_fragment not in output:
                raise RuntimeError(
                    f"negative mutation rejection did not reach intended validator guard: "
                    f"{name}: expected {expected_fragment!r}: {output}"
                )
            proofs.append({"name": name, "changed_paths": changed, "exit_code": result.returncode, "rejection_sha256": sha_bytes(output.encode("utf-8"))})
        result = {"schema": "pixel-tart-p3-negative-evidence-proof/v1", "count": len(proofs), "proof_sha256": sha_bytes(json_bytes(proofs, pretty=False)), "proofs": proofs}
        print(json.dumps(result, ensure_ascii=True, separators=(",", ":")))
        return 0
    finally:
        remove_tree(workspace)


if __name__ == "__main__":
    raise SystemExit(main())
