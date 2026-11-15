param([switch]$Service,[switch]$ResumeStudio)
$ErrorActionPreference='Stop'
$packagedContext=Join-Path $PSScriptRoot 'home-context.json'
if(Test-Path -LiteralPath $packagedContext){
 $c=Get-Content -LiteralPath $packagedContext -Raw | ConvertFrom-Json
 $private=Get-Content -LiteralPath $c.privateConnectionPath -Raw | ConvertFrom-Json
}else{
 $repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
 $c=Get-Content -LiteralPath (Join-Path $repo 'artifacts/home-20261115/CONTEXT.json') -Raw | ConvertFrom-Json
 $private=Get-Content -LiteralPath (Join-Path $repo 'artifacts/home-20261115/PRIVATE_CONNECTION.json') -Raw | ConvertFrom-Json
}
if($Service){
 $env:PIXELTART_SELECTION_LOCALDEV_ROOT=Join-Path $c.artifactRoot 'selection-service-state'
 $env:PIXELTART_SELECTION_LOCALDEV_PORT=[string]$c.selectionPort
 $env:PIXELTART_SELECTION_OWNER_KEY=$private.ownerKey
 $env:PIXELTART_SELECTION_PUBLIC_BASE_URL=$private.endpoint.TrimEnd('/')
 & (Join-Path $c.package 'SelectionService/PixelTart.SelectionApi.Server.exe')
}else{
 $env:PIXEL_TART_ISOLATED_RUNTIME='1'
 $env:PIXEL_TART_ISOLATED_RUNTIME_ROOT=$c.runtime
 $env:PIXELTART_SELECTION_ENDPOINT=$private.endpoint
 $env:PIXELTART_SELECTION_OWNER_KEY=$private.ownerKey
 $arguments=@()
 if($ResumeStudio){$arguments+="--workflow-project=$($c.workflowProjectId)";$arguments+="--studio-resume=$($c.artifactRoot)/HOME-SYNTHETIC.ptstudio.json"}
 & (Join-Path $c.package 'App/KitaoPhotoSelector.exe') @arguments
}
