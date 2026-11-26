"""Company: one matched-binary diagnostic, not a gate rerun or product deployment."""
import argparse
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

from company_preflight import require, verify_inputs

TOOL = Path(__file__).resolve().parent
REPO = TOOL.parents[1]
VALIDATION = REPO / 'docs/validation/company-precision-integration-20261123/VALIDATION.json'
F319_PE = '2.3.0+f3190f4470996232aa63477838b8979d66dd8e45'


def resolve_dump_tool(explicit):
    if explicit is not None:
        return explicit.resolve(strict=True)
    path = shutil.which('dotnet-dump.exe')
    candidate = Path(path) if path else Path.home() / '.dotnet/tools/dotnet-dump.exe'
    require(candidate.is_file(), 'Use --dump-tool with the deployed 10.0.750501 exe from the company receipt')
    return candidate.resolve()


def versions(app, dump_tool):
    paths = [app / name for name in ('KitaoPhotoSelector.dll', 'RAWSelectionAssistant.Core.dll')]
    # PowerShell literals; path contents cannot become code.
    commands = ["[Diagnostics.FileVersionInfo]::GetVersionInfo('" + str(path).replace("'", "''") + "').ProductVersion" for path in paths]
    actual = subprocess.check_output(['powershell.exe', '-NoProfile', '-Command', '\n'.join(commands)], text=True).splitlines()
    require(actual == [F319_PE, F319_PE], 'Original App/Core PE versions must both be ' + F319_PE)
    version = subprocess.check_output([str(dump_tool), '--version'], text=True).strip()
    require(re.fullmatch(r'10\.0\.750501(?:[+\s].*)?', version), 'Expected dotnet-dump 10.0.750501')
    return {'appProductVersion': actual[0], 'coreProductVersion': actual[1], 'dumpToolVersion': version}


def run(launch, out, product_app, dump_tool, original):
    require(not out.is_relative_to(launch.parent), 'Use a new output outside the historical run')
    out.mkdir(parents=True, exist_ok=False)  # Also prevents a second invocation from sharing this attempt.
    receipt = out / 'INPUT_PREFLIGHT.json'
    try:
        require(__debug__, 'Use normal Python: historical guard scripts require assertions enabled')
        inputs = verify_inputs(launch, product_app, require_original=original)
        dump_tool = resolve_dump_tool(dump_tool)
        checked = versions(inputs.app, dump_tool)
        require(shutil.disk_usage(out).free > 3 * 1024**3, 'Need >3 GiB free on the output volume')
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        with receipt.open('x', encoding='utf-8') as file:
            json.dump({'status': 'STOP_BEFORE_HOST', 'reason': str(error), 'targetStarted': False,
                       'historicalIccFileReadProven': False, 'resourceAdmission': 'NOT_TESTED'}, file, indent=2)
        raise
    with receipt.open('x', encoding='utf-8') as file:
        json.dump({'status': 'INPUTS_VERIFIED_BEFORE_HOST_BUILD', 'input': inputs.receipt, **checked}, file, indent=2)
    cmd = ['dotnet', 'publish', str(TOOL/'Host.csproj'), '-c', 'Release', '-p:Platform=x64',
           '-p:ProductAppDirectory='+str(inputs.app), '-o', str(out/'host'), '-v:q', '--nologo']
    with (out/'host-build.log').open('x', encoding='utf-8') as log:
        subprocess.run(cmd, stdout=log, stderr=subprocess.STDOUT, check=True)
    subprocess.run([sys.executable, str(TOOL/'prepare_stage.py'), '--product-app', str(inputs.app), '--host-build', str(out/'host'),
                    '--resources', str(REPO/'src/RAWSelectionAssistant/App.xaml'), '--output', str(out/'stage')], check=True)
    subprocess.run([sys.executable, str(TOOL/'capture.py'), '--stage', str(out/'stage'), '--baseline-launch', str(launch),
                    '--out', str(out/'run'), '--dump-tool', str(dump_tool)], check=True)
    subprocess.run([sys.executable, str(TOOL/'analyze.py'), '--run', str(out/'run'), '--dump-tool', str(dump_tool)], check=True)
    print('Diagnostic only. Review INPUT_PREFLIGHT, CAPTURE_SUMMARY, EXIT/STOP and ANALYSIS_SUMMARY; no resource admission.')


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument('--company-63a', action='store_true', help='Resolve the one original LAUNCH from the company VALIDATION receipt')
    source.add_argument('--baseline-launch', type=Path, help='Exact original LAUNCH path on this machine')
    parser.add_argument('--product-app', type=Path, help='Optional directory with the same App/Core bytes as the stage')
    parser.add_argument('--dump-tool', type=Path, help='Installed 10.0.750501 exe if not on PATH/in .dotnet/tools')
    parser.add_argument('--output', type=Path, help='New directory; never reuse a previous attempt')
    args = parser.parse_args(argv)
    if args.company_63a:
        old_run = json.loads(VALIDATION.read_bytes())['legacyCandidateGuard']['path']
        launch = Path(old_run) / 'LAUNCH.json'
    else:
        launch = args.baseline_launch
        if args.output is None:
            parser.error('--baseline-launch requires an explicit new --output directory')
    launch = launch.resolve(strict=True)
    out = args.output or launch.parent.parent.with_name('rgb48-holder-boundary-company-once-20261126')
    run(launch, out.resolve(), args.product_app, args.dump_tool, args.company_63a)


if __name__ == '__main__':
    try:
        main()
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError) as error:
        sys.exit(f'Observation stopped (no retry): {error}')
