"""Bounded final build and fail-closed regression for the frozen correction commit.
Does not open UI, scan photos, alter other worktrees or reinterpret conditional tests.
"""
import argparse, hashlib, json, os, pathlib, shutil, subprocess, sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    root = pathlib.Path(__file__).resolve().parents[2]
    os.chdir(root)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise RuntimeError("dotnet runtime not found")
    sdk_lines = subprocess.check_output([dotnet, "--list-sdks"], text=True).splitlines()
    version, location = sdk_lines[-1].split(" [", 1)
    sdk = str(pathlib.Path(location.rstrip("]")) / version)
    source = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
    commands = []

    def run(label, command, timeout=240, env=None):
        with (output / (label + ".log")).open("w", encoding="utf-8") as log:
            completed = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT,
                                       timeout=timeout, env=env)
        commands.append(dict(label=label, command=command, exitCode=completed.returncode))
        (output / "commands.json").write_text(json.dumps(commands, indent=2), encoding="utf-8")
        print(label, completed.returncode, flush=True)
        return completed.returncode

    def required(label, command, timeout=240):
        if run(label, command, timeout):
            raise RuntimeError(label + " failed; see saved log")

    release = output / "release" / "win-x64"
    required("publish", [dotnet, "publish", "src/RAWSelectionAssistant/RAWSelectionAssistant.csproj",
             "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:Platform=x64",
             "-p:PublishSingleFile=false", "-p:InformationalVersion=2.3.0-v3-corrections-BLOCKED+" + source,
             "--artifacts-path", str(output / "build-release"), "-o", str(release)])
    required("test-build", [dotnet, "build", "tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj",
             "-c", "Release", "-p:Platform=x64", "--artifacts-path", str(output / "build-tests")])
    required("core-build", [dotnet, "build", "tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj",
             "-c", "Release", "-p:Platform=x64", "--artifacts-path", str(output / "build-core")])
    required("candidate-build", [dotnet, "build", "tools/InstantFilmCandidate.Tests/InstantFilmCandidate.Tests.csproj", "-c", "Release"])
    identities = []
    for item in release.rglob("*"):
        if item.is_file():
            identities.append(dict(path=item.relative_to(release).as_posix(), bytes=item.stat().st_size,
                                   sha256=hashlib.sha256(item.read_bytes()).hexdigest().upper()))
    (output / "release-files.json").write_text(json.dumps(identities, indent=2), encoding="utf-8")
    (output / "source-commit.txt").write_text(source + "\n", encoding="utf-8")
    host = output / "published-tests"
    bindings = []
    for name, project in [("wpf", "RAWSelectionAssistant.WpfTests"), ("core", "RAWSelectionAssistant.Tests")]:
        build = output / ("build-tests" if name == "wpf" else "build-core") / "bin" / project / "release"
        destination = host / name
        shutil.copytree(build, destination)
        for item in release.glob("*.dll"):
            if item.name in ["KitaoPhotoSelector.dll", "RAWSelectionAssistant.Core.dll", "PixelTart.Kernel.dll", "PixelTart.MatchV4.Dx12.dll"] or item.name.startswith("PixelTart.Modules."):
                if (destination / item.name).exists():
                    shutil.copyfile(item, destination / item.name)
                    original = hashlib.sha256(item.read_bytes()).hexdigest()
                    actual = hashlib.sha256((destination / item.name).read_bytes()).hexdigest()
                    assert original == actual
                    bindings.append(dict(host=name, name=item.name, releaseSha256=original, hostSha256=actual))
    (output / "bindings.json").write_text(json.dumps(bindings, indent=2), encoding="utf-8")
    runner = [sys.executable, "tools/planning-r7/verified_runner.py"]
    def suite(label, assembly, single=False, env=None, classes=None):
        command = runner + ["--assembly", str(assembly), "--output", str(output / label),
                            "--dotnet", dotnet, "--sdk", sdk, "--resource-root", str(root)]
        if single:command += ["--single-process"]
        if classes:command += ["--classes"] + classes
        # A nonzero result from the fail-closed runner is recorded, not silenced.
        run(label, command, timeout=3600, env=env)

    focused = ["RAWSelectionAssistant.WpfTests." + name for name in ["PhotoGradePageV1Tests", "InstantV3CorrectionBindingTests", "InstantV3PhaseCancellationTests", "InstantV3MultipleInputTests", "InstantV3ProductTests"]]
    suite("source-focused", output / "build-tests/bin/RAWSelectionAssistant.WpfTests/release/RAWSelectionAssistant.WpfTests.dll", classes=focused)
    suite("source-core", output / "build-core/bin/RAWSelectionAssistant.Tests/release/RAWSelectionAssistant.Tests.dll", single=True)
    env = os.environ.copy()
    env["PIXEL_TART_BATCH12_RELEASE_CORE"] = str(host / "core/RAWSelectionAssistant.Core.dll")
    suite("published-core", host / "core/RAWSelectionAssistant.Tests.dll", single=True, env=env)
    suite("candidate", root / "tools/InstantFilmCandidate.Tests/bin/Release/net10.0/InstantFilmCandidate.Tests.dll", single=True)
    required("dpi", [dotnet, "test", "tests/RAWSelectionAssistant.DpiTests/RAWSelectionAssistant.DpiTests.csproj", "-c", "Release",
                      "--logger", "trx;LogFileName=CorrectionsDpi.trx", "--results-directory", str(output / "dpi")])
    suite("published-wpf", host / "wpf/RAWSelectionAssistant.WpfTests.dll")
    print("COMPLETE; read summary status and conditional cases, not command success alone", flush=True)

if __name__ == "__main__":
    main()
