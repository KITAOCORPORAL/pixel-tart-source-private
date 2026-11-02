param([Parameter(Mandatory=$true)][string]$Delivery)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$identity=Get-Content (Join-Path $Delivery 'ZIP_IDENTITY.json') -Raw|ConvertFrom-Json
$name='Guangjing-R55-SummarySnapshots-Diagnostic-win-x64'
$directory=Join-Path $Delivery $name
$zip=Join-Path $Delivery ([System.IO.Path]::GetFileName($identity.zip))
if((Get-FileHash -LiteralPath $zip).Hash -ne $identity.sha256){throw 'ZIP differs'}
$archive=[System.IO.Compression.ZipFile]::OpenRead($zip)
try{
    if($archive.Entries.Count -ne $identity.fileCount){throw 'Count differs'}
    foreach($file in $identity.files){
        if((Get-FileHash -LiteralPath (Join-Path $directory $file.path)).Hash -ne $file.sha256){throw "Directory differs: $($file.path)"}
        $entry=$archive.GetEntry($name+'/'+$file.path)
        if(-not $entry){throw "Missing: $($file.path)"}
        $stream=$entry.Open()
        try{if([Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)) -ne $file.sha256){throw "Archive differs: $($file.path)"}}
        finally{$stream.Dispose()}
    }
}finally{$archive.Dispose()}
foreach($binding in (Get-Content (Join-Path $Delivery 'PUBLISHED_BINDING.json') -Raw|ConvertFrom-Json)){
    $published=(Get-FileHash -LiteralPath (Join-Path $directory $binding.file)).Hash
    $bound=(Get-FileHash -LiteralPath (Join-Path $Delivery ($binding.host+'/'+$binding.file))).Hash
    if($published -ne $binding.releaseSha256 -or $published -ne $bound){throw "Binding differs: $($binding.file)"}
}
$product=Get-Content (Join-Path $directory 'PRODUCT_IDENTITY.json') -Raw|ConvertFrom-Json
if(-not $product.WholeAppDiagnosticBuild -or $product.PhotographerTrialBuild){throw 'Wrong shell'}
[pscustomobject]@{status='PASS';files=$identity.fileCount;zipSha256=$identity.sha256;productSource=$product.productSource;testSupport=$product.testSupport;scope='Package integrity only; not independent Office or physical acceptance'}
