#requires -Version 7.5
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Delivery,
    [Parameter(Mandatory)][ipaddress]$Lan,
    [ValidateRange(1024,65535)][int]$Port=45751,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]{0,31}$')][string]$Instance='review',
    [Guid]$Project='95ff154e-5a63-448d-a3bc-8f49dfb78b80',
    [string]$SourceProjectCopy,
    [ValidateLength(1,32)][string]$Reviewer,
    [Alias('Route')][ValidateSet('list','mood','brief','shots')][string]$OpenOwnerPage
)
$ErrorActionPreference='Stop'
if(!$IsWindows){throw 'Windows x64 and PowerShell 7.5+ are required.'}
$ip=$Lan.IPAddressToString
if($Lan.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or [IPAddress]::IsLoopback($Lan) -or $Port -eq 45731 -or $Project -eq [Guid]::Empty){throw 'Use a local LAN IPv4, an isolated project ID, and a port other than the original review pilot.'}
$nic=Get-NetIPAddress -AddressFamily IPv4 -IPAddress $ip -ErrorAction SilentlyContinue | Where-Object AddressState -eq Preferred
if(!$nic){throw 'Requested LAN IPv4 is not currently assigned and Preferred on this computer.'}
$deliveryRoot=(Get-Item -LiteralPath $Delivery).FullName
if((Split-Path $deliveryRoot -Leaf) -eq 'planning-collaboration-pilot-20261126'){throw 'Preserve the existing review delivery. Use a separate workbench delivery.'}
$service=(Get-Item -LiteralPath (Join-Path $deliveryRoot 'Service/PlanningReview.exe')).FullName
$serviceRoot=Split-Path $service -Parent
$null=Get-Item -LiteralPath (Join-Path $serviceRoot 'wwwroot/workbench.html')
$sourceCopy=if($SourceProjectCopy){(Get-Item -LiteralPath $SourceProjectCopy).FullName}else{$null}
# Reuse only directories bearing our matching purpose/project/address marker.
$privateRoot=Join-Path $deliveryRoot ('private-'+$Instance)
$marker=Join-Path $privateRoot 'workbench-instance.json'
function Write-LocalText([string]$Path,[string]$Text){
    $tempPath=Join-Path (Split-Path $Path -Parent) ('.workbench-tmp-'+[Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($tempPath,$Text,[Text.UTF8Encoding]::new($false));[IO.File]::Move($tempPath,$Path,$true)
}
function Keep-Private([string]$Directory){
    $dirInfo=Get-Item -LiteralPath $Directory
    if($dirInfo.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing a redirected private directory.'}
    $acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false)
    $selfSid=[Security.Principal.WindowsIdentity]::GetCurrent().User;$acl.SetOwner($selfSid)
    foreach($sid in @($selfSid,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'))){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
    Set-Acl -LiteralPath $Directory -AclObject $acl
}
$identity=[ordered]@{schema=1;purpose='isolated-planning-workbench';project=$Project.ToString();lan=$ip;port=$Port;instance=$Instance}
if(Test-Path -LiteralPath $privateRoot){
    $privateItem=Get-Item -LiteralPath $privateRoot;if($privateItem.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing a redirected private root.'}
    if(!(Test-Path -LiteralPath $marker)){throw 'Unrecognized private data root: no startup, migration or overwrite.'}
    $old=Get-Content -Raw -LiteralPath $marker|ConvertFrom-Json
    foreach($k in $identity.Keys){if([string]$identity[$k] -cne [string]$old.$k){throw "Existing workbench $k differs. Use a new instance name and port."}}
    if($sourceCopy -and $old.sourceCopy -ne $sourceCopy){throw 'The existing workbench cannot be rebound to a new source copy.'}
}else{
    $null=New-Item -ItemType Directory -Path $privateRoot -ErrorAction Stop;Keep-Private $privateRoot
    $identity.sourceCopy=$sourceCopy;Write-LocalText $marker ($identity|ConvertTo-Json)
}
# Exclude concurrent bootstrap/check/start and invite-slot races within this private instance.
$lease=[IO.File]::Open((Join-Path $privateRoot 'launcher.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    $certPath=Join-Path $privateRoot 'workbench-host.pfx'
    if(!(Test-Path -LiteralPath $certPath)){
        if(Test-Path -LiteralPath (Join-Path $privateRoot 'notebook.json')){throw 'Existing project certificate is missing. Preserved without replacing trust.'}
        $rsa=[Security.Cryptography.RSA]::Create(3072)
        try{
            $req=[Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Guangjing Planning Workbench',$rsa,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $sans=[Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new();$sans.AddDnsName('localhost');$sans.AddIpAddress([ipaddress]::Loopback);$sans.AddIpAddress($Lan);$req.CertificateExtensions.Add($sans.Build())
            $req.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new($false,$false,0,$true))
            $ku=[Security.Cryptography.OidCollection]::new();$null=$ku.Add([Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'));$req.CertificateExtensions.Add([Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($ku,$true))
            $cert=$req.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5),[DateTimeOffset]::UtcNow.AddDays(90));try{[IO.File]::WriteAllBytes($certPath,$cert.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx))}finally{$cert.Dispose()}
        }finally{$rsa.Dispose()}
    }
    $cert=[Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadPkcs12FromFile($certPath,$null,[Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)
    try{if($cert.NotAfter.ToUniversalTime() -le [DateTime]::UtcNow){throw 'The certificate has expired. Preserve this project and review trust renewal separately.'};$pin=$cert.GetCertHashString([Security.Cryptography.HashAlgorithmName]::SHA256)}finally{$cert.Dispose()}
    $localBase="https://127.0.0.1:$Port";$lanBase="https://${ip}:$Port";$pidFile=Join-Path $privateRoot 'listener.json';$running=$null
    if(Test-Path -LiteralPath $pidFile){$savedPid=Get-Content -LiteralPath $pidFile -Raw|ConvertFrom-Json;$prior=Get-Process -Id $savedPid.pid -ErrorAction SilentlyContinue;if($prior -and $prior.Path -eq $service -and $prior.StartTime.ToUniversalTime().Ticks -eq [long]$savedPid.started){$running=$prior}}
    if(!$running){
        if(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue){throw 'The port already has a listener. No process was stopped.'}
        $envForChild=@{};$runtimeFile=Join-Path $deliveryRoot 'runtime.local'
        if(Test-Path -LiteralPath $runtimeFile){$sdkPath=(Get-Content -LiteralPath $runtimeFile -Raw).Trim();$null=Get-Item -LiteralPath (Join-Path $sdkPath 'dotnet.exe');$envForChild=@{DOTNET_ROOT=$sdkPath;DOTNET_ROOT_X64=$sdkPath}}
        $argsLine='--workbench true --data "{0}" --lan {1} --port {2} --project {3} --cert "{4}"' -f $privateRoot,$ip,$Port,$Project,$certPath
        if($sourceCopy){$argsLine+=' --copy "'+$sourceCopy+'"'}
        $logStem='service-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fffffff')
        $running=Start-Process -FilePath $service -ArgumentList $argsLine -WorkingDirectory $serviceRoot -WindowStyle Hidden -Environment $envForChild -RedirectStandardOutput (Join-Path $privateRoot ($logStem+'.out')) -RedirectStandardError (Join-Path $privateRoot ($logStem+'.err')) -PassThru
        Write-LocalText $pidFile (@{pid=$running.Id;started=$running.StartTime.ToUniversalTime().Ticks}|ConvertTo-Json)
    }
    # Certificate pinning happens at the TLS handshake, including before the owner bearer is sent.
    if(-not ('GuangjingWorkbenchPinnedHttp' -as [type])){
        Add-Type -TypeDefinition @"
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
public static class GuangjingWorkbenchPinnedHttp {
    public static HttpClient Open(string hash) {
        var h=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false,UseCookies=false};
        h.ServerCertificateCustomValidationCallback=(req,cert,chain,errors)=>cert!=null && DateTime.UtcNow>=cert.NotBefore.ToUniversalTime() && DateTime.UtcNow<=cert.NotAfter.ToUniversalTime() && String.Equals(cert.GetCertHashString(HashAlgorithmName.SHA256),hash,StringComparison.OrdinalIgnoreCase);
        return new HttpClient(h) { Timeout=TimeSpan.FromSeconds(3) };
    }
}
"@
    }
    $http=[GuangjingWorkbenchPinnedHttp]::Open($pin)
    try{
        function Read-Service([string]$Uri,[string]$Bearer='',[object]$Body=$null){
            $method=if($null -eq $Body){[Net.Http.HttpMethod]::Get}else{[Net.Http.HttpMethod]::Post};$req=[Net.Http.HttpRequestMessage]::new($method,$Uri)
            try{if($Bearer){$req.Headers.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$Bearer)};if($null -ne $Body){$req.Content=[Net.Http.StringContent]::new(($Body|ConvertTo-Json -Depth 5 -Compress),[Text.Encoding]::UTF8,'application/json')};$resp=$http.SendAsync($req).GetAwaiter().GetResult();try{if(!$resp.IsSuccessStatusCode){throw ('Workbench request returned '+[int]$resp.StatusCode)};return $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()}finally{$resp.Dispose()}}finally{$req.Dispose()}
        }
        $ok=$false;for($i=0;$i -lt 36;$i++){if($running.HasExited){throw 'Workbench host exited. See its private startup log.'};try{if((Read-Service ($localBase+'/health')|ConvertFrom-Json).version -eq 1){$ok=$true;break}}catch{};Start-Sleep -Milliseconds 220}
        if(!$ok){throw 'Pinned loopback health did not succeed. No process was stopped.'}
        $sockets=@(Get-NetTCPConnection -State Listen -LocalPort $Port);if($sockets.Count -ne 2 -or @($sockets|Where-Object OwningProcess -ne $running.Id).Count -or $ip -notin $sockets.LocalAddress -or '127.0.0.1' -notin $sockets.LocalAddress){throw 'Listener cannot be bound to the expected process and two exact addresses.'}
        if((Read-Service ($lanBase+'/health')|ConvertFrom-Json).version -ne 1){throw 'Pinned LAN health failed.'};$null=Read-Service ($localBase+'/workbench.html')
        $ownerFile=Join-Path $privateRoot 'owner.local';$owner=(Get-Content -LiteralPath $ownerFile -Raw).Trim();$state=Read-Service ($localBase+'/api/state') $owner|ConvertFrom-Json
        if(!$state.me.owner -or [Guid]$state.project -ne $Project -or $null -eq $state.workbench){throw 'The authenticated application is not the requested isolated workbench.'}
        $entryDir=Join-Path $privateRoot 'entries';if(!(Test-Path -LiteralPath $entryDir)){$null=New-Item -ItemType Directory -Path $entryDir};if((Get-Item -LiteralPath $entryDir).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing redirected private entries.'}
        $entryBase=$localBase+'/workbench.html';$encoded=[Uri]::EscapeDataString($owner);$routeLabels=[ordered]@{list='策划总览';mood='视觉情绪板';brief='创意简报';shots='拍摄分镜'};$ownerHrefs=@();$launchPages=[ordered]@{}
        function Html([string]$value){[Net.WebUtility]::HtmlEncode($value)}
        function Page([string]$Title,[string]$Inside,[string]$Redirect=''){
            $jump=if($Redirect){'<meta http-equiv="refresh" content="0;url='+ (Html $Redirect) +'">'}else{''}
            '<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="referrer" content="no-referrer"><meta name="viewport" content="width=device-width, initial-scale=1">'+$jump+'<title>'+ (Html $Title) +'</title><style>body{margin:10vh auto;padding:24px;max-width:700px;background:#f7f6ed;color:#27382d;font:17px/1.6 system-ui,sans-serif}a{display:block;margin:8px 0;padding:10px 16px;background:#e4eddf;color:#24432d;text-decoration:none;border-radius:6px}code{overflow-wrap:anywhere}</style><h1>'+ (Html $Title) +'</h1>'+$Inside+'</html>'
        }
        foreach($route in $routeLabels.Keys){$url=$entryBase+'#code='+$encoded+'&route='+$route;$label=$routeLabels[$route];$anchor='<a href="'+(Html $url)+'">'+(Html $label)+'</a>';$ownerHrefs+=$anchor;$f=Join-Path $entryDir ('owner-'+$route+'.html');Write-LocalText $f (Page $label $anchor $url);$launchPages[$route]=$f}
        $ownerIndex=Join-Path $entryDir '负责人审稿入口.html';Write-LocalText $ownerIndex (Page '光镜 · 策划工作台' ('<p>本机负责人入口。若首次打开提示本地证书，请核对 SHA-256：<br><code>'+$pin+'</code></p>'+($ownerHrefs -join '')))
        # The public local landing contains relative links to ACL-restricted local files only.
        $localLinks=foreach($route in $routeLabels.Keys){'<a href="private-'+$Instance+'/entries/owner-'+$route+'.html">'+(Html $routeLabels[$route])+'</a>'}
        $publicName=if($Instance -eq 'review'){'打开光镜工作台.html'}else{'打开光镜工作台-'+$Instance+'.html'};$publicIndex=Join-Path $deliveryRoot $publicName
        Write-LocalText $publicIndex (Page '光镜 · 策划工作台' ('<p>在托管电脑上选择要审阅的页面。其他成员使用各自的邀请文件。首次连接请核对本机证书：<br><code>'+$pin+'</code></p>'+($localLinks -join '')))
        $entryReceipt=Join-Path $privateRoot 'ENTRY_RECEIPT.json';$memberFile=$null;$expires=$null
        if(!$Reviewer -and (Test-Path -LiteralPath $entryReceipt)){
            # Keep the previous handoff visible on plain restarts only while its seat is still pending.
            $priorEntry=Get-Content -Raw -LiteralPath $entryReceipt|ConvertFrom-Json
            if($priorEntry.memberEntry){foreach($pending in $state.invitations){$candidate=Join-Path $entryDir ('member-'+[Guid]$pending.id+'.html');if($priorEntry.memberEntry -eq $candidate -and (Test-Path -LiteralPath $candidate)){$memberFile=$candidate;$expires=([DateTimeOffset]$pending.expires).ToUniversalTime().ToString('o');break}}}
        }
        if($Reviewer){
            $label=$Reviewer.Trim();if(!$label){throw 'Reviewer name is required.'};if(!$state.team){throw 'Upgrade this dedicated project before reserving a team seat.'}
            $invite=@($state.invitations|Where-Object label -CEQ $label)|Select-Object -First 1
            if(!$invite){if(@($state.people|Where-Object {$_.active -and $_.name -ceq $label}).Count){throw 'Reviewer already joined; use their existing member session or transfer code.'};$null=Read-Service ($localBase+'/api/op') $owner @{id=[guid]::NewGuid();project=$Project;action='invite';text=$label};$state=Read-Service ($localBase+'/api/state') $owner|ConvertFrom-Json;$invite=@($state.invitations|Where-Object label -CEQ $label)|Select-Object -First 1}
            if(!$invite){throw 'Designated invitation was not committed.'};$expires=([DateTimeOffset]$invite.expires).ToUniversalTime().ToString('o');$memberFile=Join-Path $entryDir ('member-'+[Guid]$invite.id+'.html');$memberUrl=$lanBase+'/workbench.html';$link=$memberUrl+'#code='+[Uri]::EscapeDataString($invite.code)+'&route=list';$body='<p>受邀署名：'+(Html $label)+'。第一次使用后不能再领取。请只把此文件交给该成员。</p><p>连接公司局域网，首次如有提示，请核对证书 SHA-256：<br><code>'+$pin+'</code></p><a href="'+(Html $link)+'">以我的署名打开策划</a><details><summary>手动填写邀请码</summary><p>打开 <a href="'+(Html $memberUrl)+'">'+(Html $memberUrl)+'</a>，把下方邀请码贴到「邀请码或个人迁移码」，再点「进入策划」。</p><code>'+(Html $invite.code)+'</code></details><p>有效期：'+(Html $expires)+'</p>';Write-LocalText $memberFile (Page '成员加入策划工作台' $body)
        }
        $result=[ordered]@{instance=$Instance;project=$Project;pid=$running.Id;loopback=$localBase+'/workbench.html';lan=$lanBase+'/workbench.html';certificateSHA256=$pin;ownerEntry=$publicIndex;privateOwnerEntry=$ownerIndex;ownerPages=$launchPages;memberEntry=$memberFile;memberExpiresUtc=$expires;physicalSecondDevice='NOT_TESTED_BY_LAUNCHER'}
        Write-LocalText $entryReceipt ($result|ConvertTo-Json -Depth 4)
        if($OpenOwnerPage){Start-Process -FilePath $launchPages[$OpenOwnerPage]}
        [pscustomobject]$result
    }finally{$http.Dispose()}
}finally{$lease.Dispose()}
