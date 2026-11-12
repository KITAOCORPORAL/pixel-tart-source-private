$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'mobile-schedule-runtime.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if(-not $config.phoneHttpsConfigured){throw 'Verified private HTTPS is not configured; do not use the loopback URL on a phone'}
$observer=Join-Path $config.runtimeRoot 'PHONE_BROWSER_OBSERVER_PRIVATE.json'
if(-not(Test-Path -LiteralPath $observer)){throw 'Prepare the current phone device observer configuration first'}
if(-not(Test-Path -LiteralPath $config.pythonExecutable)){throw 'The machine-bound Python runtime is missing'}
& $config.pythonExecutable (Join-Path $PSScriptRoot 'diagnostics/BrowserTimeZoneObserver.py') --config $observer --port $config.browserObserverPort
exit $LASTEXITCODE
