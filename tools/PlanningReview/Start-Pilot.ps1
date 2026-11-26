param([Parameter(Mandatory)][string]$Delivery,[string]$Lan='192.168.1.10',[int]$Port=45731,[Guid]$Project='a276b086-36dd-441e-b730-86ab876ac112',[string]$Instance='review',[string]$SourceProjectCopy,[switch]$Desktop)
$ErrorActionPreference='Stop'
if ($Instance -notmatch '^[a-zA-Z0-9-]+$') { throw 'Invalid instance' }
$deliveryRoot=(Resolve-Path -LiteralPath $Delivery).Path
$privateRoot=Join-Path $deliveryRoot ("private-"+$Instance)
$null=New-Item -ItemType Directory -Force -Path $privateRoot
$acl=Get-Acl -LiteralPath $privateRoot
$acl.SetAccessRuleProtection($true,$false)
$identity=[Security.Principal.WindowsIdentity]::GetCurrent().User
foreach($sid in @($identity,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))) {
    $acl.SetAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
}
Set-Acl -LiteralPath $privateRoot -AclObject $acl
$tlsPath=Join-Path $privateRoot 'host.pfx'
if (!(Test-Path -LiteralPath $tlsPath)) {
    $rsa=[Security.Cryptography.RSA]::Create(3072)
    $req=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Pixel Tart Planning Pilot',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $names=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new();$names.AddDnsName('localhost');$names.AddIpAddress([Net.IPAddress]::Loopback);$names.AddIpAddress([Net.IPAddress]::Parse($Lan));$req.CertificateExtensions.Add($names.Build())
    $req.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false,$false,0,$true))
    $usages=[Security.Cryptography.OidCollection]::new();$null=$usages.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'));$req.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usages,$true))
    $cert=$req.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(90))
    [IO.File]::WriteAllBytes($tlsPath,$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx));$cert.Dispose();$rsa.Dispose()
}
$peer=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile($tlsPath,$null)
$peerHash=$peer.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256);$peer.Dispose()
$service=Join-Path $deliveryRoot 'Service\PlanningReview.exe';$ownerFile=Join-Path $privateRoot 'owner.local';$localUrl="https://127.0.0.1:$Port/"
$pidFile=Join-Path $privateRoot 'listener.json';$running=$false
if(Test-Path -LiteralPath $pidFile){$old=Get-Content -LiteralPath $pidFile -Raw|ConvertFrom-Json;$previous=Get-Process -Id $old.pid -ErrorAction SilentlyContinue;if($previous -and $previous.Path -eq $service -and $previous.StartTime.ToUniversalTime().Ticks -eq $old.started){$running=$true}}
if(!$running){
    if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'Port already owned; no process was stopped'}
    $env:DOTNET_ROOT=Join-Path $deliveryRoot 'Desktop';$env:DOTNET_ROOT_X64=$env:DOTNET_ROOT
    $sdkInfo=Join-Path $deliveryRoot 'runtime.local';if(Test-Path -LiteralPath $sdkInfo){$env:DOTNET_ROOT=(Get-Content -LiteralPath $sdkInfo -Raw).Trim();$env:DOTNET_ROOT_X64=$env:DOTNET_ROOT}
    $argsLine='--data "{0}" --lan {1} --port {2} --project {3} --cert "{4}"' -f $privateRoot,$Lan,$Port,$Project,$tlsPath
    if($SourceProjectCopy){$copy=(Resolve-Path -LiteralPath $SourceProjectCopy).Path;$argsLine+=' --copy "'+$copy+'"'}
    $hostProcess=Start-Process -FilePath $service -ArgumentList $argsLine -WorkingDirectory (Split-Path $service) -RedirectStandardError (Join-Path $privateRoot 'host.stderr') -RedirectStandardOutput (Join-Path $privateRoot 'host.stdout') -WindowStyle Hidden -PassThru
    @{pid=$hostProcess.Id;started=$hostProcess.StartTime.ToUniversalTime().Ticks}|ConvertTo-Json|Set-Content -LiteralPath $pidFile
}
$ready=$false;for($attempt=0;$attempt -lt 25;$attempt++){try{if((Invoke-RestMethod -Uri ($localUrl+'health') -SkipCertificateCheck -NoProxy -TimeoutSec 2).version -eq 1){$ready=$true;break}}catch{};Start-Sleep -Milliseconds 200};if(!$ready){throw 'Review service did not start; see host.stderr'}
$configPath=Join-Path $privateRoot 'desktop.json';@{url=$localUrl;peerSha256=$peerHash;profile=(Join-Path $privateRoot 'webview');credentialFile=$ownerFile}|ConvertTo-Json|Set-Content -LiteralPath $configPath
if($Desktop){$env:PIXEL_TART_ISOLATED_RUNTIME='1';$env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=Join-Path $privateRoot 'desktop-runtime';$exe=Join-Path $deliveryRoot 'Desktop\KitaoPhotoSelector.exe';$desktopProcess=Start-Process -FilePath $exe -ArgumentList ('--planning-review "'+$configPath+'"') -WorkingDirectory (Split-Path $exe) -PassThru; @{pid=$desktopProcess.Id;started=$desktopProcess.StartTime.ToUniversalTime().Ticks}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $privateRoot 'desktop-process.json')}
[pscustomobject]@{Local=$localUrl;Lan="https://${Lan}:$Port/";CertificateSHA256=$peerHash;Project=$Project}
