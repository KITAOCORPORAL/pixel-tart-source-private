#requires -Version 7.5
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Delivery,[ipaddress]$Lan,[int]$Port,[switch]$HostOnly,[switch]$RestartHost)
$ErrorActionPreference='Stop'
$root=(Get-Item -LiteralPath $Delivery).FullName
$service=(Get-Item -LiteralPath (Join-Path $root 'Service/PlanningReview.exe')).FullName
$desktop=(Get-Item -LiteralPath (Join-Path $root 'Desktop/KitaoPhotoSelector.exe')).FullName
$private=Join-Path $root '_private'; $data=Join-Path $private 'host-data'; $runtime=Join-Path $private 'desktop-data'
$configPath=Join-Path $private 'host.json'; $certPath=Join-Path $private 'candidate.pfx'; $secretPath=Join-Path $private 'control.key'
$publicCert=Join-Path $root '团队协作候选证书.cer'; $pidFile=Join-Path $private 'listener.json'
function Atomic([string]$Path,[object]$Value){
    $temp=$Path+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
    try{[IO.File]::WriteAllText($temp,($Value|ConvertTo-Json -Depth 5));[IO.File]::Move($temp,$Path,$true)}
    finally{if(Test-Path -LiteralPath $temp){Remove-Item -LiteralPath $temp}}
}
function NoLink([string]$Path){if((Test-Path -LiteralPath $Path) -and ((Get-Item -LiteralPath $Path).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'The candidate data/configuration cannot be redirected.'}}
NoLink $root; NoLink $private
if(!(Test-Path -LiteralPath $private)){
    if(!$Lan -or !$Port){throw 'First launch requires a local LAN IPv4 and an isolated port. See HOME_HANDOFF.md.'}
    New-Item -ItemType Directory -Path $private | Out-Null
    $acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false)
    $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User;$acl.SetOwner($sid)
    foreach($principal in @($sid,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($principal,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
    }
    Set-Acl -LiteralPath $private -AclObject $acl
}
$lease=[IO.File]::Open((Join-Path $private 'launcher.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    foreach($path in @($configPath,$certPath,$secretPath,$data,$runtime,$publicCert,$pidFile)){NoLink $path}
    if(Test-Path -LiteralPath $configPath){
        $config=Get-Content -Raw -LiteralPath $configPath|ConvertFrom-Json
        if($config.purpose -ne 'guangjing-planning-share-candidate' -or $config.secretFile -ne 'control.key'){throw 'Unrecognized candidate configuration.'}
        $configuredLan=[ipaddress]([uri]$config.memberBase).Host; $configuredPort=([uri]$config.controlBase).Port
        if(($Lan -and $Lan -ne $configuredLan) -or ($Port -and $Port -ne $configuredPort)){throw 'This destination is already bound. Use a new candidate delivery for new address settings.'}
        $Lan=$configuredLan;$Port=$configuredPort
    }
    if(!$Lan -or $Lan.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or [ipaddress]::IsLoopback($Lan) -or $Port -lt 1024 -or $Port -gt 65535 -or $Port -in 45731,45732,45751,45752){throw 'Specify a local LAN IPv4 and a separate available port.'}
    $ip=$Lan.IPAddressToString
    if(!(Get-NetIPAddress -AddressFamily IPv4 -IPAddress $ip -ErrorAction SilentlyContinue|Where-Object AddressState -eq Preferred)){throw 'The LAN IPv4 is not currently assigned.'}
    $localBase="https://127.0.0.1:$Port";$lanBase="https://${ip}:$Port"
    if(!(Test-Path -LiteralPath $configPath)){
        if((Test-Path -LiteralPath $certPath) -or (Test-Path -LiteralPath $secretPath) -or (Test-Path -LiteralPath $data) -or (Test-Path -LiteralPath $publicCert)){throw 'Incomplete previous candidate setup. Preserve it and use a new delivery.'}
        if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'This port is already in use. Nothing was stopped.'}
        $rsa=[Security.Cryptography.RSA]::Create(3072)
        try {
            $request=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Guangjing Planning Share Candidate',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $san=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new();$san.AddIpAddress([ipaddress]::Loopback);$san.AddIpAddress($Lan);$request.CertificateExtensions.Add($san.Build())
            $request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false,$false,0,$true))
            $usage=[Security.Cryptography.OidCollection]::new();$null=$usage.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'));$request.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($usage,$true))
            $cert=$request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(90))
            try {
                [IO.File]::WriteAllBytes($certPath,$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))
                [IO.File]::WriteAllBytes($publicCert,$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Cert))
                $pin=$cert.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256)
            } finally {$cert.Dispose()}
        } finally {$rsa.Dispose()}
        [IO.File]::WriteAllText($secretPath,[Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)))
        $config=[pscustomobject]@{purpose='guangjing-planning-share-candidate';destinationId=[Guid]::NewGuid();controlBase=$localBase;memberBase=$lanBase;certificateSha256=$pin;secretFile='control.key'}
        Atomic $configPath $config
    }
    if($config.controlBase -ne $localBase -or $config.memberBase -ne $lanBase -or [Guid]$config.destinationId -eq [Guid]::Empty){throw 'Unexpected candidate origin or destination identity.'}
    $peer=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile($certPath,$null,[Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    try{if($config.certificateSha256 -ne $peer.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256) -or $peer.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow){throw 'Candidate TLS certificate mismatch or expiry.'}}
    finally{$peer.Dispose()}
    $running=$null
    if(Test-Path -LiteralPath $pidFile){
        $saved=Get-Content -Raw -LiteralPath $pidFile|ConvertFrom-Json
        $p=Get-Process -Id $saved.pid -ErrorAction SilentlyContinue
        if($p -and $p.Path -eq $service -and $p.StartTime.ToUniversalTime().Ticks -eq [long]$saved.startUtcTicks -and [Guid]$saved.destinationId -eq [Guid]$config.destinationId){$running=$p}
    }
    if($RestartHost -and $running){Stop-Process -InputObject $running;if(!$running.WaitForExit(8000)){throw 'Owned candidate listener did not exit.'};$running=$null}
    if(!$running){
        if(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue){throw 'A different process holds the port. Nothing was stopped.'}
        $line='--data "{0}" --lan {1} --port {2} --cert "{3}" --share-destination-id {4} --share-public-base {5} --share-control-secret-file "{6}"' -f $data,$ip,$Port,$certPath,$config.destinationId,$lanBase,$secretPath
        $stem='service-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')
        $running=Start-Process -FilePath $service -ArgumentList $line -WorkingDirectory (Split-Path -Parent $service) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $private ($stem+'.out')) -RedirectStandardError (Join-Path $private ($stem+'.err')) -PassThru
        Atomic $pidFile @{pid=$running.Id;startUtcTicks=$running.StartTime.ToUniversalTime().Ticks;destinationId=$config.destinationId}
    }
    if(-not ('GuangjingShareProbe' -as [type])){
        Add-Type -TypeDefinition @"
using System;
using System.Net.Http;
using System.Security.Cryptography;
public static class GuangjingShareProbe {
  public static HttpClient Create(string pin) {
    var h=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false};
    h.ServerCertificateCustomValidationCallback=(r,c,_,e)=>c!=null && DateTime.UtcNow>=c.NotBefore.ToUniversalTime() && DateTime.UtcNow<=c.NotAfter.ToUniversalTime() && String.Equals(pin,c.GetCertHashString(HashAlgorithmName.SHA256),StringComparison.OrdinalIgnoreCase);
    return new HttpClient(h){Timeout=TimeSpan.FromSeconds(2)};
  }
}
"@
    }
    $http=[GuangjingShareProbe]::Create($config.certificateSha256)
    try {
        function Check([string]$url){
            $value=$http.GetStringAsync($url+'/health').GetAwaiter().GetResult()|ConvertFrom-Json
            if($value.mode -ne 'planning-share-candidate' -or [Guid]$value.destinationId -ne [Guid]$config.destinationId){throw 'This listener does not hold the candidate destination.'}
        }
        $ok=$false
        for($i=0;$i -lt 24;$i++){
            if($running.HasExited){throw 'The candidate listener exited. Check the isolated startup error log.'}
            try{Check $localBase;$ok=$true;break}catch{Start-Sleep -Milliseconds 220}
        }
        if(!$ok){throw 'Pinned loopback health check failed.'};Check $lanBase
        $listeners=@(Get-NetTCPConnection -State Listen -LocalPort $Port)
        if($listeners.Count -ne 2 -or @($listeners|Where-Object OwningProcess -ne $running.Id).Count -or $ip -notin $listeners.LocalAddress -or '127.0.0.1' -notin $listeners.LocalAddress){throw 'Unexpected candidate listener or bind addresses.'}
    } finally {$http.Dispose()}
    $result=[ordered]@{servicePid=$running.Id;lan=$lanBase;certificateSha256=$config.certificateSha256;desktopPid=$null}
    if(!$HostOnly){
        if(!(Test-Path -LiteralPath $runtime)){New-Item -ItemType Directory -Path $runtime|Out-Null}
        $envForDesktop=@{PIXEL_TART_ISOLATED_RUNTIME='1';PIXEL_TART_ISOLATED_RUNTIME_ROOT=$runtime;GUANGJING_TEAM_SHARE_CONFIG=$configPath;PIXEL_TART_HUMAN_ACCEPTANCE=$null;PIXEL_TART_ACCEPTANCE=$null;PIXEL_TART_ACCEPTANCE_ROOT=$null}
        $p=Start-Process -FilePath $desktop -WorkingDirectory (Split-Path -Parent $desktop) -Environment $envForDesktop -PassThru
        $result.desktopPid=$p.Id
        Atomic (Join-Path $private 'desktop-receipt.json') @{pid=$p.Id;exe=$p.Path;startUtcTicks=$p.StartTime.ToUniversalTime().Ticks}
    }
    [pscustomobject]$result
} finally {$lease.Dispose()}
