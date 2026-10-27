param(
 [Parameter(Mandatory=$true)][string]$Manifest,
 [Parameter(Mandatory=$true)][string]$ExperimentRoot
)
$ErrorActionPreference='Stop'
# Never launch Capture One or change an existing Session/Catalog/defaults.
$root=[IO.Path]::GetFullPath($ExperimentRoot)
if(Test-Path -LiteralPath $root){throw 'Require a new independent experiment directory; no overwrite'}
$data=Get-Content -LiteralPath $Manifest -Raw|ConvertFrom-Json
foreach($s in $data.samples){
 $source=[IO.Path]::GetFullPath($s.path)
 $parent=[IO.Path]::GetDirectoryName($source).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
 if($root.Equals($source,[StringComparison]::OrdinalIgnoreCase) -or ($root+[IO.Path]::DirectorySeparatorChar).StartsWith($parent,[StringComparison]::OrdinalIgnoreCase)){throw 'Experiment must not be inside a source directory'}
 if(-not $s.sha256){throw 'Selected source SHA256 required'}
 if((Get-Item -LiteralPath $source).Length -ne $s.bytes){throw "Source size changed: $source"}
}
New-Item -ItemType Directory -Path $root | Out-Null
$inputs=Join-Path $root 'InputCopies';New-Item -ItemType Directory -Path $inputs|Out-Null
$receipts=@()
foreach($s in $data.samples){
 $destination=Join-Path $inputs ($s.sampleId+[IO.Path]::GetExtension($s.path))
 Copy-Item -LiteralPath $s.path -Destination $destination -ErrorAction Stop
 $actual=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
 if($actual -ne $s.sha256){throw "Input copy hash mismatch; do not use: $destination"}
 $receipts+=[pscustomobject]@{source=$s.path;copy=$destination;bytes=(Get-Item -LiteralPath $destination).Length;sha256=$actual}
}
$receipts|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'input-copy-receipts.json') -Encoding utf8
foreach($name in @('Session','Exports','Logs','Settings')){New-Item -ItemType Directory -Path (Join-Path $root $name)|Out-Null}
Write-Output "Prepared input copies only: $root. Capture One Session has NOT been created."
