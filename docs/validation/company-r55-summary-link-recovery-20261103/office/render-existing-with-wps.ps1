param([Parameter(Mandatory=$true)][string]$Samples,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Fresh render directory required'}
New-Item -ItemType Directory -Path $Output|Out-Null
$before=@(Get-Process wps -ErrorAction SilentlyContinue|Select-Object -ExpandProperty Id)
$receipt=[ordered]@{timestampShanghai=[DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(8)).ToString('o');engine='Installed KWPS.Application';initialWpsPids=$before;attempts=0;documents=@();status='NOT_RUN';failure=$null;quitOwnedApplication=$false}
$application=$null
$owned=$false
try{
    if($before.Count){$receipt.status='NOT_RUN_USER_WPS_PROCESS_ACTIVE';return}
    $receipt.attempts=1
    $application=New-Object -ComObject KWPS.Application
    if($application.Documents.Count -ne 0){$receipt.status='NOT_RUN_NONEMPTY_COM_APPLICATION';return}
    $owned=$true
    $application.Visible=$false
    $application.DisplayAlerts=0
    $receipt['applicationVersion']=$application.Version
    foreach($name in @('saved-two-revisions','snapshot-placeholders','narrow-portrait','explicit-placements')){
        $source=[IO.Path]::GetFullPath((Join-Path $Samples ($name+'.docx')))
        $destination=Join-Path $Output ($name+'.pdf')
        $sourceHash=(Get-FileHash -LiteralPath $source).Hash
        $document=$null
        try{
            $document=$application.Documents.Open($source,$false,$true,$false)
            $document.ExportAsFixedFormat($destination,17,$false)
            if(-not(Test-Path -LiteralPath $destination)){throw 'PDF not created'}
            $receipt.documents+=@{name=$name;input=$source;sourceSha256=$sourceHash;pdf=$destination;pdfSha256=(Get-FileHash -LiteralPath $destination).Hash;bytes=(Get-Item -LiteralPath $destination).Length;readOnly=$document.ReadOnly}
        }finally{
            if($document){$document.Close(0);[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($document)}
        }
        if((Get-FileHash -LiteralPath $source).Hash -ne $sourceHash){throw 'Read-only source changed'}
    }
    $receipt.status='RENDERED_PENDING_VISUAL_REVIEW'
}catch{
    $receipt.status='NOT_RUN_OR_PARTIAL_RENDER_FAILED'
    $receipt.failure=$_.Exception.ToString()
}finally{
    if($application){
        if($owned -and $application.Documents.Count -eq 0){$application.Quit(0);$receipt.quitOwnedApplication=$true}
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($application)
    }
    $receipt['remainingWpsPids']=@(Get-Process wps -ErrorAction SilentlyContinue|Select-Object -ExpandProperty Id)
    $receipt|ConvertTo-Json -Depth 7|Set-Content (Join-Path $Output 'WPS_RENDER_RECEIPT.json')
}
