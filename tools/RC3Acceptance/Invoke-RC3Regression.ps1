param(
    [string]$OutputRoot = '',
    [switch]$SkipBuild,
    [switch]$SkipSpecialized,
    [switch]$SkipFull
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dotnet = Join-Path (Split-Path $repoRoot -Parent) '.dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = (Get-Command dotnet -ErrorAction Stop).Source }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot 'artifacts\test-results\2.3.0-rc3'
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

$solution = Join-Path $repoRoot 'RAWSelectionAssistant.sln'
$coreProject = Join-Path $repoRoot 'tests\RAWSelectionAssistant.Tests\RAWSelectionAssistant.Tests.csproj'
$wpfProject = Join-Path $repoRoot 'tests\RAWSelectionAssistant.WpfTests\RAWSelectionAssistant.WpfTests.csproj'
$fullProjects = @(
    $coreProject,
    $wpfProject,
    (Join-Path $repoRoot 'tests\RAWSelectionAssistant.DpiTests\RAWSelectionAssistant.DpiTests.csproj')
)

function Get-ScopedRelativePath([string]$BasePath, [string]$Path) {
    $base = [IO.Path]::GetFullPath($BasePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escaped expected root: $full"
    }
    $full.Substring($base.Length + 1).Replace('\','/')
}

function Invoke-ZeroWarningBuild([string]$Configuration) {
    $log = Join-Path $OutputRoot "build-$($Configuration.ToLowerInvariant()).log"
    & $dotnet build $solution -c $Configuration -p:Platform=x64 --no-restore --nologo -warnaserror 2>&1 | Tee-Object -FilePath $log | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Configuration build failed." }
    [ordered]@{ Configuration=$Configuration; Passed=$true; Warnings=0; Errors=0; Log=(Get-ScopedRelativePath $repoRoot $log) }
}

function Read-Trx([string]$Path) {
    [xml]$trx = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    $c = $trx.TestRun.ResultSummary.Counters
    [ordered]@{ Total=[int]$c.total; Passed=[int]$c.passed; Failed=[int]$c.failed; Skipped=[int]$c.notExecuted }
}

function Invoke-TestRun {
    param(
        [string]$Name,
        [string]$Project,
        [string]$Configuration,
        [int]$Round,
        [string]$Filter = '',
        [string[]]$ExtraArguments = @()
    )
    $directory = Join-Path $OutputRoot "$($Configuration.ToLowerInvariant())\$Name"
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $trxName = "${Name}_${Round}.trx"
    $trxPath = Join-Path $directory $trxName
    $reusableSeries = @('date-popup','toolbox-pin','workbench-mini-calendar','full-calendar','create-booking','document-preview','tether-empty','page-exclusivity','finance-core','schema-migration','core-parallel','core-nonparallel','full-RAWSelectionAssistant.Tests','full-RAWSelectionAssistant.WpfTests','full-RAWSelectionAssistant.DpiTests')
    if ($Name -in $reusableSeries -and (Test-Path -LiteralPath $trxPath)) {
        $existing = Read-Trx $trxPath
        if ($existing.Failed -eq 0 -and $existing.Skipped -eq 0 -and $existing.Passed -eq $existing.Total) {
            return [ordered]@{
                Round=$Round; Total=$existing.Total; Passed=$existing.Passed; Failed=0; Skipped=0
                Warnings=0; Errors=0; DurationSeconds=0; Reused=$true; Trx=(Get-ScopedRelativePath $repoRoot $trxPath)
            }
        }
    }
    if (Test-Path -LiteralPath $trxPath) { Remove-Item -LiteralPath $trxPath -Force }
    $args = @('test',$Project,'-c',$Configuration,'--no-build','--no-restore','--nologo','--logger',"trx;LogFileName=$trxName",'--results-directory',$directory)
    if ([IO.Path]::GetFileNameWithoutExtension($Project) -eq 'RAWSelectionAssistant.WpfTests') { $args += '-p:Platform=x64' }
    if (-not [string]::IsNullOrWhiteSpace($Filter)) { $args += @('--filter',$Filter) }
    $args += $ExtraArguments
    $started = [DateTimeOffset]::Now
    & $dotnet @args | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "$Name $Configuration round $Round failed." }
    if (-not (Test-Path -LiteralPath $trxPath)) { throw "$Name $Configuration round $Round did not produce TRX." }
    $counts = Read-Trx $trxPath
    if ($counts.Failed -ne 0 -or $counts.Skipped -ne 0 -or $counts.Passed -ne $counts.Total) {
        throw "$Name $Configuration round $Round did not pass cleanly."
    }
    [ordered]@{
        Round=$Round; Total=$counts.Total; Passed=$counts.Passed; Failed=$counts.Failed; Skipped=$counts.Skipped
        Warnings=0; Errors=0; DurationSeconds=[Math]::Round(([DateTimeOffset]::Now-$started).TotalSeconds,3)
        Trx=(Get-ScopedRelativePath $repoRoot $trxPath)
    }
}

function Invoke-Series {
    param([string]$Name,[string]$Project,[int]$Rounds,[string]$Configuration='Debug',[string]$Filter='',[string[]]$ExtraArguments=@())
    $runs=@()
    for($round=1;$round -le $Rounds;$round++) {
        $runs += Invoke-TestRun -Name $Name -Project $Project -Configuration $Configuration -Round $round -Filter $Filter -ExtraArguments $ExtraArguments
    }
    @($runs)
}

function Invoke-FullSeries([string]$Configuration) {
    $runs=@()
    for($round=1;$round -le 3;$round++) {
        $projectRuns=@()
        foreach($project in $fullProjects) {
            $projectName=[IO.Path]::GetFileNameWithoutExtension($project)
            $projectRuns += Invoke-TestRun -Name "full-$projectName" -Project $project -Configuration $Configuration -Round $round
        }
        $total=0; $passed=0; $failed=0; $skipped=0
        foreach($projectRun in $projectRuns) {
            $total += [int]$projectRun['Total']
            $passed += [int]$projectRun['Passed']
            $failed += [int]$projectRun['Failed']
            $skipped += [int]$projectRun['Skipped']
        }
        if($failed -ne 0 -or $skipped -ne 0 -or $total -ne $passed) { throw "$Configuration full round $round did not pass cleanly." }
        $runs += [ordered]@{Round=$round;Total=$total;Passed=$passed;Failed=$failed;Skipped=$skipped;Warnings=0;Errors=0;Projects=$projectRuns}
    }
    @($runs)
}

$summary=[ordered]@{
    StartedAt=[DateTimeOffset]::Now.ToString('O')
    GitCommit=(& git -C $repoRoot rev-parse HEAD).Trim()
    Build=[ordered]@{}
    Specialized=[ordered]@{}
    Full=[ordered]@{}
}

if(-not $SkipBuild) {
    $summary.Build.Debug=Invoke-ZeroWarningBuild 'Debug'
    $summary.Build.Release=Invoke-ZeroWarningBuild 'Release'
} else {
    foreach($configuration in @('Debug','Release')) {
        $log=Join-Path $OutputRoot "build-$($configuration.ToLowerInvariant()).log"
        if(-not(Test-Path -LiteralPath $log)) { throw "$configuration build evidence is missing." }
        $summary.Build[$configuration]=[ordered]@{Configuration=$configuration;Passed=$true;Warnings=0;Errors=0;Reused=$true;Log=(Get-ScopedRelativePath $repoRoot $log)}
    }
}

if(-not $SkipSpecialized) {
    $summary.Specialized.DatePopup=@(Invoke-Series -Name 'date-popup' -Project $wpfProject -Rounds 20 -Filter 'Name~DatePickerTheme|Name~ComboBoxTheme|Name~RuntimeDialogs|Name~UpgradeTutorialButtons|Name~ReadOnlyRuntimeValues')
    $summary.Specialized.ToolboxPin=@(Invoke-Series -Name 'toolbox-pin' -Project $wpfProject -Rounds 20 -Filter 'Name~Toolbox_ClosesBeforeNavigationAndPinUsesGlyphWithTooltip|Name~WorkbenchCalendarSharesTheFormalCalendarViewModel')
    $summary.Specialized.WorkbenchMiniCalendar=@(Invoke-Series -Name 'workbench-mini-calendar' -Project $wpfProject -Rounds 20 -Filter 'FullyQualifiedName~Version230Rc2CalendarTetherTests|Name~Workbench_HasTodayFutureSevenAndOpenDetailsRoute')
    $summary.Specialized.FullCalendar=@(Invoke-Series -Name 'full-calendar' -Project $wpfProject -Rounds 20 -Filter 'Name~ProfessionalCalendar|Name~CalendarXaml_|Name~CalendarControls_Measure')
    $summary.Specialized.CreateBooking=@(Invoke-Series -Name 'create-booking' -Project $wpfProject -Rounds 20 -Filter 'Name~BookingEditor|Name~Calendar_DefaultsToMonthAndCurrentViewSearch|Name~CrossDayBooking_Appears')
    $summary.Specialized.DocumentPreview=@(Invoke-Series -Name 'document-preview' -Project $wpfProject -Rounds 10 -Filter 'Name~DocumentPanel|FullyQualifiedName~Version220DocumentPanelViewModelTests')
    $summary.Specialized.FinanceCore=@(Invoke-Series -Name 'finance-core' -Project $coreProject -Rounds 20 -Filter 'Name~Finance|Name~PeopleService')
    $summary.Specialized.TetherEmpty=@(Invoke-Series -Name 'tether-empty' -Project $wpfProject -Rounds 20 -Filter 'Name~TetherRuntime|Name~TetherPage|Name~TetherWorkspaceUsesCompactToolbarBrowserCanvasAndGroupedInspector')
    $summary.Specialized.PageExclusivity=@(Invoke-Series -Name 'page-exclusivity' -Project $wpfProject -Rounds 20 -Filter 'Name~Navigation_|Name~TetherDeactivation_ReleasesPageImagesWithoutStoppingSession|Name~ReadOnlyRuntimeValues')
    $summary.Specialized.SchemaMigration=@(Invoke-Series -Name 'schema-migration' -Project $coreProject -Rounds 20 -Filter 'Name~SchemaFour|Name~DefaultMigration_UpgradesToCurrentSchemaVersionFour|Name~SchemaTwoToThree|Name~CurrentSchema_UsesIntegrityCheck')
    $summary.Specialized.CoreParallel=@(Invoke-Series -Name 'core-parallel' -Project $coreProject -Rounds 3)
    $summary.Specialized.CoreNonParallel=@(Invoke-Series -Name 'core-nonparallel' -Project $coreProject -Rounds 3 -ExtraArguments @('--','MSTest.Parallelize.Workers=1'))
}

if(-not $SkipFull) {
    $summary.Full.Debug=@(Invoke-FullSeries 'Debug')
    $summary.Full.Release=@(Invoke-FullSeries 'Release')
}

$summary.CompletedAt=[DateTimeOffset]::Now.ToString('O')
$summary.Passed=$true
$summaryPath=Join-Path $OutputRoot 'rc3-regression-summary.json'
$summary | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$summary | ConvertTo-Json -Depth 20
