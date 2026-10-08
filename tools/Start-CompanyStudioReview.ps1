param([string]$ReleaseDirectory = (Join-Path $PSScriptRoot '../artifacts/releases/company-repair-2026-10-08/publish/win-x64'),
      [string]$RuntimeDirectory = (Join-Path $PSScriptRoot '../artifacts/company-repair-2026-10-08/repaired-runtime'))
$ErrorActionPreference = 'Stop'
$exe = Join-Path ([IO.Path]::GetFullPath($ReleaseDirectory)) 'KitaoPhotoSelector.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Company Release has not been published.' }
$runtime = [IO.Path]::GetFullPath($RuntimeDirectory)
if (-not [IO.Path]::IsPathFullyQualified($runtime)) { throw 'An isolated runtime path is required.' }
# Child-only environment: never change the user's normal app data or default install.
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = $false
$start.WorkingDirectory = Split-Path $exe
$start.Environment['PIXEL_TART_ISOLATED_RUNTIME'] = '1'
$start.Environment['PIXEL_TART_ISOLATED_RUNTIME_ROOT'] = $runtime
foreach ($key in @('PIXEL_TART_ACCEPTANCE_ROOT','PIXEL_TART_HUMAN_ACCEPTANCE','PIXEL_TART_COLOR_STUDIO_FIXTURE','PIXEL_TART_COLOR_STUDIO_ACCEPTANCE')) { $start.Environment.Remove($key) | Out-Null }
# Visible is intentional: this launcher is only for the requested real UI review.
$process = [Diagnostics.Process]::Start($start)
[pscustomobject]@{ProcessId=$process.Id;Executable=$exe;RuntimeRoot=$runtime;StartedUtc=[DateTime]::UtcNow.ToString('o')}
