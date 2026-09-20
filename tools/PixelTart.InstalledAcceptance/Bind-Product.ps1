param([Parameter(Mandatory)][string]$InstallerProvenance)
$ErrorActionPreference='Stop'
$provenance=Get-Content -LiteralPath $InstallerProvenance -Raw|ConvertFrom-Json
$installer=Join-Path (Split-Path $InstallerProvenance) $provenance.InstallerFileName
if((Get-FileHash -LiteralPath $installer).Hash -ne $provenance.InstallerSha256){throw 'Installer provenance hash mismatch'}
foreach($name in @('planning-full','upgrade-full')) {
    $path=Join-Path $PSScriptRoot "$name.plan.json"
    $p=Get-Content -LiteralPath $path -Raw|ConvertFrom-Json -AsHashtable
    $p.ProductSourceSha=$provenance.ProductSourceSha
    $p.InstallerPath='installer/'+$provenance.InstallerFileName
    $p.InstallerSha256=$provenance.InstallerSha256
    $p.RunDirectory='InstalledAcceptance_'+$provenance.ProductSourceSha.Substring(0,7)
    [IO.File]::WriteAllText($path,($p|ConvertTo-Json -Depth 20)+"`n",[Text.UTF8Encoding]::new($false))
}
