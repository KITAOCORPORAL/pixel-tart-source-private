param([Parameter(Mandatory)][string]$Delivery,[string]$Reviewer='公司审稿员 C')
$ErrorActionPreference='Stop';$deliveryRoot=(Resolve-Path -LiteralPath $Delivery).Path;$private=Join-Path $deliveryRoot 'private-review'
$config=Get-Content -Raw -LiteralPath (Join-Path $private 'desktop.json')|ConvertFrom-Json;$uri=[uri]$config.url
$receipt=Get-Content -Raw -LiteralPath (Join-Path $private 'listener.json')|ConvertFrom-Json
$socket=Get-NetTCPConnection -State Listen -OwningProcess $receipt.pid -LocalPort $uri.Port|Where-Object {$_.LocalAddress -ne '127.0.0.1'}|Select-Object -First 1
if(!$socket){throw 'No LAN listener'};$lan='https://'+$socket.LocalAddress+':'+$uri.Port+'/'
$token=(Get-Content -Raw -LiteralPath $config.credentialFile).Trim();$headers=@{Authorization='Bearer '+$token}
$state=Invoke-RestMethod ($config.url+'api/state') -Headers $headers -SkipCertificateCheck -NoProxy
$invite=$state.invitations|Where-Object label -eq $Reviewer|Select-Object -First 1
if(!$invite){if($state.people|Where-Object {$_.active -and $_.name -eq $Reviewer}){throw 'This member has already joined; use their own migration code'};$body=@{id=[guid]::NewGuid();project=$state.project;action='invite';text=$Reviewer}|ConvertTo-Json;$null=Invoke-RestMethod ($config.url+'api/op') -Method Post -ContentType 'application/json' -Body $body -Headers $headers -SkipCertificateCheck -NoProxy;$state=Invoke-RestMethod ($config.url+'api/state') -Headers $headers -SkipCertificateCheck -NoProxy;$invite=$state.invitations|Where-Object label -eq $Reviewer|Select-Object -First 1}
if(!$invite){throw 'Invitation was not committed'}
$share=Join-Path $deliveryRoot '第二台电脑';$null=New-Item -ItemType Directory -Force -Path $share
$target=$lan+'#code='+$invite.code;$until=([DateTimeOffset]$invite.expires).ToUniversalTime().ToString('yyyy-MM-dd HH:mm UTC');$safeName=[Net.WebUtility]::HtmlEncode($Reviewer)
$page=@"
<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="referrer" content="no-referrer"><title>光镜 · 第二台电脑加入策划</title><style>body{max-width:650px;margin:10vh auto;background:#202622;color:#eee;font:17px/1.65 'Microsoft YaHei',sans-serif;padding:25px}a{color:#b2d9be}a.go{display:inline-block;background:#b2d9be;color:#16251a;padding:10px 22px;border-radius:8px;text-decoration:none}code{word-break:break-all;color:#c6d8c4}small{color:#b7c0b5}</style><h1>秋冬服装型录</h1><p>此单次邀请属于 <b>$safeName</b>。请在第二台公司电脑上打开本文件，并连接托管电脑所在局域网。</p><p><a class="go" href="$target">以我的署名加入策划 →</a></p><p>若浏览器提示试点使用本地证书，请先核对证书 SHA-256：<br><code>$($config.peerSha256)</code>。此试点不要求安装全局证书。</p><details><summary>手动加入</summary><p>打开 <a href="$lan">$lan</a>，把下方邀请码贴入登录页。</p><code>$($invite.code)</code></details><p><small>有效期至 $until，首次使用后失效。换电脑请从原设备左下角「我的身份」取得本人的迁移码。仅转交这份成员邀请，不转交负责人文件。</small></p></html>
"@
[IO.File]::WriteAllText((Join-Path $share '加入秋冬服装型录.html'),$page)
$script=Join-Path $deliveryRoot 'Start-Pilot.ps1';Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-Pilot.ps1') -Destination $script -Force
$link=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $deliveryRoot '打开光镜策划审稿.lnk'));$link.TargetPath=(Get-Process -Id $PID).Path;$link.Arguments='-NoProfile -WindowStyle Hidden -File "'+$script+'" -Delivery "'+$deliveryRoot+'" -Desktop';$link.WorkingDirectory=$deliveryRoot;$link.IconLocation=(Join-Path $deliveryRoot 'Desktop/KitaoPhotoSelector.exe')+',0';$link.Save()
@{lan=$lan;local=$config.url;certificateSha256=$config.peerSha256;reviewer=$Reviewer;invitationId=$invite.id;expires=$invite.expires;reservedOrUsedMembers=$state.used;limit=$state.memberSeats;project=$state.project;document=$state.document}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $deliveryRoot 'ENTRY_RECEIPT.json')
