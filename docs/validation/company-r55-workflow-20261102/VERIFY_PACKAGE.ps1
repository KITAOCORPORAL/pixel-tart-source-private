param([Parameter(Mandatory=$true)][string]$Delivery)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$identity=Get-Content (Join-Path $Delivery 'ZIP_IDENTITY.json') -Raw | ConvertFrom-Json
$zip=Join-Path $Delivery ([System.IO.Path]::GetFileName($identity.zip))
$directory=Join-Path $Delivery 'Guangjing-R55-CompanyWorkflow-Diagnostic-win-x64'
if((Get-FileHash -LiteralPath $zip).Hash -ne $identity.sha256){throw 'ZIP digest differs'}
$archive=[System.IO.Compression.ZipFile]::OpenRead($zip)
try{
    if($archive.Entries.Count -ne $identity.fileCount){throw 'Entry count differs'}
    foreach($file in $identity.files){
        if((Get-FileHash -LiteralPath (Join-Path $directory $file.path)).Hash -ne $file.sha256){throw "Directory digest differs: $($file.path)"}
        $entry=$archive.GetEntry('Guangjing-R55-CompanyWorkflow-Diagnostic-win-x64/'+$file.path)
        if(-not $entry){throw "Missing archive file: $($file.path)"}
        $stream=$entry.Open()
        try{if([Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)) -ne $file.sha256){throw "Archive digest differs: $($file.path)"}}
        finally{$stream.Dispose()}
    }
}finally{$archive.Dispose()}
$product=Get-Content (Join-Path $directory 'PRODUCT_IDENTITY.json') -Raw | ConvertFrom-Json
if(-not $product.WholeAppDiagnosticBuild -or $product.PhotographerTrialBuild){throw 'Wrong shell scope'}
foreach($binding in (Get-Content (Join-Path $Delivery 'PUBLISHED_BINDING.json') -Raw | ConvertFrom-Json)){
    $published=(Get-FileHash -LiteralPath (Join-Path $directory $binding.file)).Hash
    $bound=(Get-FileHash -LiteralPath (Join-Path $Delivery ($binding.host+'/'+$binding.file))).Hash
    if($published -ne $binding.releaseSha256 -or $published -ne $bound){throw "Binding differs: $($binding.file)"}
}
[pscustomobject]@{status='PASS';productSource=$product.productSource;fileCount=$identity.fileCount;zipSha256=$identity.sha256;scope='Directory/ZIP/testhost integrity, not numerical or physical acceptance'}
