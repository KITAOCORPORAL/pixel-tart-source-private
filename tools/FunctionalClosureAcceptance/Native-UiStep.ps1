param([Parameter(Mandatory=$true)][string]$Context,[string]$Label='state',[ValidateSet('Get','SetValue','Invoke','Expand','Click','Select','Key','Type')][string]$Action='Get',[string]$ElementPattern='',[string]$Value='')
$ErrorActionPreference='Stop'
$config=Get-Content -LiteralPath $Context -Raw | ConvertFrom-Json
$outDir=Join-Path $config.deliveryRoot 'native-resume'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
$initialRaw=(& ocu call get_app_state --args '{"app":"KitaoPhotoSelector","show_full_text":true}') -join "`n"
if($LASTEXITCODE -ne 0){throw 'Native CLI state capture failed'}
$initial=$initialRaw | ConvertFrom-Json
$initialText=(@($initial.content | Where-Object type -eq 'text' | Select-Object -ExpandProperty text) -join "`n")
if($initialText -notmatch ('^App=KitaoPhotoSelector \(pid '+$config.workflowAppPid+'\)')){throw 'Native CLI selected another process; no action taken'}
$final=$initial
if($Action -ne 'Get'){
 if($Action -notin @('Key','Type')){
 $matchesFound=[regex]::Matches($initialText,$ElementPattern,[Text.RegularExpressions.RegexOptions]::Multiline)
 if($matchesFound.Count -ne 1){throw ('Expected exactly one observed UI element; found '+$matchesFound.Count)}
 $elementId=$matchesFound[0].Groups[1].Value
 $toolArgs=@{app='KitaoPhotoSelector';element_index=$elementId}
 }else{$toolArgs=@{app='KitaoPhotoSelector'}}
 switch($Action){
  Key{$tool='press_key';$toolArgs.key=$Value}
  Type{if($initialText -notmatch 'focused UI element is (编辑|edit)'){throw 'No observed editable focus for typing'};$tool='type_text';$toolArgs.text=$Value}
  SetValue{$tool='set_value';$toolArgs.value=$Value}
  Invoke{$tool='perform_secondary_action';$toolArgs.action='Invoke'}
  Expand{$tool='perform_secondary_action';$toolArgs.action='Expand'}
  Select{$tool='perform_secondary_action';$toolArgs.action='Select'}
  Click{$tool='click'}
 }
 $calls=@(@{tool='get_app_state';args=@{app='KitaoPhotoSelector';show_full_text=$true}},@{tool=$tool;args=$toolArgs},@{tool='get_app_state';args=@{app='KitaoPhotoSelector';show_full_text=$true}})
 $callFile=Join-Path $outDir ($Label+'-calls.json')
 $calls | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $callFile -Encoding UTF8
 $raw=(& ocu call --calls-file $callFile) -join "`n"
 $result=$raw | ConvertFrom-Json
 $errorsFound=@($result | Where-Object {$_.result.isError})
 if($LASTEXITCODE -ne 0 -or $errorsFound.Count){$safe=@($result | ForEach-Object {@{tool=$_.tool;isError=$_.result.isError;text=@($_.result.content | Where-Object type -eq 'text' | Select-Object -ExpandProperty text)}});$safe | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outDir ($Label+'-failure.json')) -Encoding UTF8;throw ('Native action failed; refresh before retry: '+$Label)}
 $final=$result[-1].result
}
$text=(@($final.content | Where-Object type -eq 'text' | Select-Object -ExpandProperty text) -join "`n")
$receipt=@{observedAtUtc=[DateTime]::UtcNow.ToString('o');authority='Normal published EXE via Open Computer Use supported CLI';pid=$config.workflowAppPid;productSource=$config.workflowAppProductSource;action=$Action;label=$Label;requestedPattern=$ElementPattern;requestedValue=$Value;accessibilityText=$text;physicalPointerClaimed=$false}
$receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outDir ($Label+'.json')) -Encoding UTF8
# Retain target-app accessibility only; desktop screenshots can contain unrelated occluding windows.
$text.Substring([Math]::Max(0,$text.Length-6500))