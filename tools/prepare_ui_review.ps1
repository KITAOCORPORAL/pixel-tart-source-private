param(
    [ValidateSet('Light', 'Dark')]
    [string]$Theme = 'Dark',
    [int]$Width = 1600,
    [int]$Height = 920,
    [ValidateSet('Workbench', 'ToolboxPopup', 'ToolboxFullPage', 'RecentProjects', 'CompletedProjectsEmpty', 'TaskCenterWithTasks', 'TaskCenterEmpty', 'Settings', 'Feedback', 'OrganizePhotos', 'Collage', 'QuickToolsOverflow')]
    [string]$State = 'Workbench',
    [string]$OutputPath = '',
    [switch]$SidebarCollapsed
)

$root = Split-Path $PSScriptRoot -Parent
$sourceRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector'
$reviewRoot = Join-Path $env:LOCALAPPDATA 'KitaoPhotoSelector.UiReview'
$sourceSettings = Join-Path $sourceRoot 'settings.json'
$reviewSettings = Join-Path $reviewRoot 'settings.json'
$reviewProjects = Join-Path $reviewRoot 'Projects\projects.json'
$reviewImages = Join-Path $reviewRoot 'DemoImages'

New-Item -ItemType Directory -Force -Path $reviewRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $reviewProjects) | Out-Null
New-Item -ItemType Directory -Force -Path $reviewImages | Out-Null
$demoSource = Join-Path $root 'src\RAWSelectionAssistant\Assets\WorkbenchProjectCover.png'
if (Test-Path $demoSource) {
    1..4 | ForEach-Object { Copy-Item -LiteralPath $demoSource -Destination (Join-Path $reviewImages "demo-$_.png") -Force }
}

if (Test-Path $sourceSettings) {
    $settings = Get-Content -LiteralPath $sourceSettings -Raw | ConvertFrom-Json
}
else {
    $settings = [pscustomobject]@{}
}

function Set-ReviewProperty {
    param([object]$Target, [string]$Name, [object]$Value)
    if ($null -eq $Target.PSObject.Properties[$Name]) {
        $Target | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
    }
    else {
        $Target.$Name = $Value
    }
}

if ($null -eq $settings.PSObject.Properties['Appearance']) {
    Set-ReviewProperty $settings 'Appearance' ([pscustomobject]@{})
}

Set-ReviewProperty $settings.Appearance 'Theme' $(if ($Theme -eq 'Dark') { 2 } else { 1 })
Set-ReviewProperty $settings.Appearance 'SidebarCollapsed' ([bool]$SidebarCollapsed)
Set-ReviewProperty $settings 'PinnedQuickTools' @('Workflow', 'PhotoOrganize', 'BatchCompress')
Set-ReviewProperty $settings 'QuickToolLayout' ([pscustomobject]@{ SchemaVersion = '1.0'; OrderedToolIds = @('Workflow', 'PhotoOrganize', 'BatchCompress') })
Set-ReviewProperty $settings 'WindowWidth' $Width
Set-ReviewProperty $settings 'WindowHeight' $Height
Set-ReviewProperty $settings 'WindowLeft' $null
Set-ReviewProperty $settings 'WindowTop' $null
Set-ReviewProperty $settings 'WindowMaximized' $false
Set-ReviewProperty $settings 'OnboardingLegacyUser' $true
Set-ReviewProperty $settings 'OnboardingUpgradeOfferShown' $true

$settings | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $reviewSettings -Encoding UTF8

$now = [DateTimeOffset]::UtcNow
$projects = @(
    [pscustomobject]@{
        Id = [Guid]::NewGuid()
        Name = '[Demo] Wedding Selection'
        Status = 1
        CreatedAt = $now.AddDays(-2).ToString('O')
        UpdatedAt = $now.AddMinutes(-18).ToString('O')
        CompletedAt = $null
        Category = 2
        OutputMode = 0
        OutputBaseDirectory = 'D:\UIReview\Output'
        OutputDirectory = 'D:\UIReview\Output\WeddingSelection'
        SourceDirectories = @('D:\UIReview\Photos')
        SelectionInputs = @('DSC01234.JPG', 'DSC01235.JPG', 'DSC01236.JPG')
        CustomExtensions = @()
        SelectionCount = 48
        MatchedFileCount = 92
        CopiedFileCount = 0
        Summary = 'Demo data: 730 photos indexed and ready for review.'
        ExportReports = $false
        ExportCsvReport = $true
        ExportJsonReport = $false
        ExportLogReport = $false
    }
)

ConvertTo-Json -InputObject $projects -Depth 12 | Set-Content -LiteralPath $reviewProjects -Encoding UTF8
$reviewState = [pscustomobject]@{
    State = $State
    Theme = $Theme
    Width = $Width
    Height = $Height
    SidebarCollapsed = [bool]$SidebarCollapsed
    OutputPath = $OutputPath
}
$reviewState | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reviewRoot 'ui-review-state.json') -Encoding UTF8
Write-Output "UI review profile prepared: $State, $Theme ${Width}x${Height}, collapsed=$([bool]$SidebarCollapsed)"
