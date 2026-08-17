[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$OutputRoot,
    [string]$ForegroundResultPath,
    [string]$EvidenceRoot,
    [string]$ProcessSnapshotBeforeAssetPath,
    [string]$ProcessSnapshotAfterAssetPath,
    [string]$ProcessSnapshotAfterReturnPath,
    [ValidateSet('Debug', 'Release')]
    [string]$PublishConfiguration = 'Release',
    [switch]$SkipRestore,
    [switch]$SkipProductBuilds,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-RepositoryRoot {
    param([string]$Candidate)
    if ([string]::IsNullOrWhiteSpace($Candidate)) {
        $Candidate = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    }
    $resolved = [System.IO.Path]::GetFullPath($Candidate)
    if (-not (Test-Path -LiteralPath (Join-Path $resolved 'RAWSelectionAssistant.sln') -PathType Leaf)) {
        throw "Repository root does not contain RAWSelectionAssistant.sln: $resolved"
    }
    return $resolved
}

function Resolve-DotnetHost {
    param([string]$Root)
    $configured = $env:DOTNET_HOST_PATH
    if (-not [string]::IsNullOrWhiteSpace($configured) -and (Test-Path -LiteralPath $configured -PathType Leaf)) {
        return [System.IO.Path]::GetFullPath($configured)
    }
    $workspaceHost = [System.IO.Path]::GetFullPath((Join-Path $Root '..\..\.dotnet\dotnet.exe'))
    if (Test-Path -LiteralPath $workspaceHost -PathType Leaf) { return $workspaceHost }
    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $command) { throw 'A dotnet host could not be located.' }
    return $command.Source
}

function Write-Utf8WithoutBom {
    param([string]$Path, [string]$Content)
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-DotnetCommand {
    param(
        [string[]]$Arguments,
        [string]$LogPath
    )
    Push-Location $script:acceptanceRepositoryRoot
    try {
        $lines = & $script:acceptanceDotnetHost @Arguments 2>&1
        $exitCode = $LASTEXITCODE
        $text = ($lines | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        Write-Utf8WithoutBom -Path $LogPath -Content $text
        return [pscustomobject]@{
            exit_code = $exitCode
            log_path = $LogPath
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-TestSuite {
    param([pscustomobject]$Definition)

    $suiteResultRoot = Join-Path $script:acceptanceResultsRoot $Definition.name
    [System.IO.Directory]::CreateDirectory($suiteResultRoot) | Out-Null
    $trxName = "$($Definition.name).trx"
    $arguments = @(
        'test',
        (Join-Path $script:acceptanceRepositoryRoot $Definition.project),
        '-c', 'Debug',
        '--logger', "trx;LogFileName=$trxName",
        '--results-directory', $suiteResultRoot,
        '--nologo'
    )
    if (-not [string]::IsNullOrWhiteSpace($Definition.filter)) {
        $arguments += @('--filter', $Definition.filter)
    }
    if ($SkipRestore) { $arguments += '--no-restore' }

    $execution = Invoke-DotnetCommand -Arguments $arguments -LogPath (Join-Path $script:acceptanceLogsRoot "$($Definition.name).log")
    $trxPath = Join-Path $suiteResultRoot $trxName
    $cases = @()
    if (Test-Path -LiteralPath $trxPath -PathType Leaf) {
        [xml]$trx = Get-Content -LiteralPath $trxPath -Raw -Encoding UTF8
        $nodes = $trx.SelectNodes("//*[local-name()='UnitTestResult']")
        foreach ($node in $nodes) {
            $cases += [pscustomobject]@{
                test_id = [string]$node.testId
                test_name = [string]$node.testName
                outcome = [string]$node.outcome
            }
        }
    }

    $passed = @($cases | Where-Object outcome -eq 'Passed').Count
    $failed = @($cases | Where-Object outcome -eq 'Failed').Count
    $skipped = @($cases | Where-Object outcome -in @('NotExecuted', 'Inconclusive', 'Skipped')).Count
    $total = $cases.Count
    $verified = $execution.exit_code -eq 0 -and $total -gt 0 -and $failed -eq 0

    return [pscustomobject]@{
        name = $Definition.name
        project = $Definition.project
        filter = $Definition.filter
        total = $total
        passed = $passed
        failed = $failed
        skipped = $skipped
        exit_code = $execution.exit_code
        verified = $verified
        trx_path = $trxPath
        log_path = $execution.log_path
        cases = $cases
    }
}

function Invoke-ProductBuild {
    param([string]$Configuration)
    $arguments = @(
        'build',
        (Join-Path $script:acceptanceRepositoryRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'),
        '-c', $Configuration,
        '--nologo',
        '-warnaserror'
    )
    if ($SkipRestore) { $arguments += '--no-restore' }
    $execution = Invoke-DotnetCommand -Arguments $arguments -LogPath (Join-Path $script:acceptanceLogsRoot "product-$($Configuration.ToLowerInvariant()).log")
    return [pscustomobject]@{
        configuration = $Configuration
        exit_code = $execution.exit_code
        warnings_allowed = $false
        verified = $execution.exit_code -eq 0
        log_path = $execution.log_path
    }
}

function Invoke-DevPreviewPublish {
    $publishRoot = Join-Path $script:acceptanceOutputRoot 'publish'
    [System.IO.Directory]::CreateDirectory($publishRoot) | Out-Null
    $arguments = @(
        'publish',
        (Join-Path $script:acceptanceRepositoryRoot 'src\RAWSelectionAssistant\RAWSelectionAssistant.csproj'),
        '-c', $PublishConfiguration,
        '-r', 'win-x64',
        '--self-contained', 'true',
        '--nologo',
        '-warnaserror',
        '-p:ModularHarnessDevPreview=true',
        '-p:PublishSingleFile=false',
        '-o', $publishRoot
    )
    if ($SkipRestore) { $arguments += '--no-restore' }
    $execution = Invoke-DotnetCommand -Arguments $arguments -LogPath (Join-Path $script:acceptanceLogsRoot 'devpreview-publish.log')
    $expectedExe = Join-Path $publishRoot 'PixelTart_ModularHarness_V1_DevPreview.exe'
    $expectedAssetModule = Join-Path $publishRoot 'PixelTart.Modules.AssetLibrary.dll'
    $depsPath = Join-Path $publishRoot 'PixelTart_ModularHarness_V1_DevPreview.deps.json'
    $exists = Test-Path -LiteralPath $expectedExe -PathType Leaf
    $assetModuleExists = Test-Path -LiteralPath $expectedAssetModule -PathType Leaf
    $depsExists = Test-Path -LiteralPath $depsPath -PathType Leaf
    $depsText = if ($depsExists) { [System.IO.File]::ReadAllText($depsPath) } else { '' }
    $runtimeHelperProvenance = $depsExists -and
        $depsText -match 'runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64/' -and
        $depsText -match '"createdump\.exe"'
    $publishedExecutables = @(Get-ChildItem -LiteralPath $publishRoot -File -Filter '*.exe')
    $applicationExecutables = @($publishedExecutables | Where-Object Name -ne 'createdump.exe')
    $runtimeHelperExecutables = @($publishedExecutables | Where-Object Name -eq 'createdump.exe')
    $unexpectedExecutables = @($publishedExecutables |
        Where-Object Name -notin @('PixelTart_ModularHarness_V1_DevPreview.exe', 'createdump.exe') |
        Select-Object -ExpandProperty Name)
    $exactExecutableSet = $applicationExecutables.Count -eq 1 -and
        $applicationExecutables[0].Name -eq 'PixelTart_ModularHarness_V1_DevPreview.exe' -and
        $runtimeHelperExecutables.Count -eq 1 -and
        $runtimeHelperProvenance -and
        $unexpectedExecutables.Count -eq 0
    $sha256 = $null
    $assetModuleSha256 = $null
    if ($exists) { $sha256 = (Get-FileHash -LiteralPath $expectedExe -Algorithm SHA256).Hash }
    if ($assetModuleExists) { $assetModuleSha256 = (Get-FileHash -LiteralPath $expectedAssetModule -Algorithm SHA256).Hash }
    return [pscustomobject]@{
        configuration = $PublishConfiguration
        executable_name = 'PixelTart_ModularHarness_V1_DevPreview.exe'
        executable_path = $expectedExe
        exists = $exists
        executable_count = $applicationExecutables.Count
        published_executable_count = $publishedExecutables.Count
        runtime_helper_executables = @($runtimeHelperExecutables | Select-Object -ExpandProperty Name)
        unexpected_executables = $unexpectedExecutables
        sha256 = $sha256
        asset_module_path = $expectedAssetModule
        asset_module_sha256 = $assetModuleSha256
        deps_path = $depsPath
        runtime_helper_provenance_verified = $runtimeHelperProvenance
        exit_code = $execution.exit_code
        verified = $execution.exit_code -eq 0 -and $exists -and $assetModuleExists -and $exactExecutableSet
        log_path = $execution.log_path
    }
}

function Read-ScaleMetrics {
    param([string]$Path)
    $empty = [pscustomobject]@{
        metrics_path = $Path
        metrics_present = $false
        corpus_count = 0
        asset_rows = 0
        visual_feature_rows = 0
        candidate_pool_limit = 0
        visual_queries = $null
        similarity = $null
        pairwise_cache_table_count = $null
        pairwise_cache_built = $null
        visual_query_100k_verified = $false
        similarity_100k_candidate_verified = $false
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $empty }

    try {
        $metrics = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        $baseVerified = $metrics.schema -eq 'pixel-tart-modular-harness-v1-visual-scale/v1' -and
            $metrics.corpus_count -eq 100000 -and
            $metrics.database.asset_rows -eq 100000 -and
            $metrics.database.visual_feature_rows -eq 100000 -and
            $metrics.database.candidate_pool_limit -gt 0 -and
            $metrics.database.candidate_pool_limit -lt $metrics.corpus_count -and
            $metrics.distribution.palette_rows -eq 100000 -and
            $metrics.distribution.tagged_rows -eq 10000 -and
            $metrics.pairwise_cache_table_count -eq 0 -and
            $metrics.pairwise_cache_built -eq $false -and
            $metrics.color_management_reference_verified -eq $false -and
            $metrics.raw_visual_proxy_verified -eq $false

        $expectedQueryCounts = [ordered]@{
            tone = 33334
            hue = 8618
            saturation = 33333
            contrast = 33333
            tag_visual = 3334
            smart_folder = 286
        }
        $queryVerified = $baseVerified
        foreach ($entry in $expectedQueryCounts.GetEnumerator()) {
            $property = $metrics.visual_queries.PSObject.Properties[$entry.Key]
            if ($null -eq $property -or
                $property.Value.milliseconds -le 0 -or
                $property.Value.total_count -ne $entry.Value -or
                $property.Value.result_count -ne 100) {
                $queryVerified = $false
            }
            if ($entry.Key -eq 'smart_folder' -and $property.Value.saved_rule_count -ne 5) {
                $queryVerified = $false
            }
        }

        $cold = $metrics.similarity.cold
        $warm = $metrics.similarity.warm
        $coldVerified = $null -ne $cold -and
            $cold.candidate_rows -gt 0 -and
            $cold.candidate_rows -le $metrics.database.candidate_pool_limit -and
            $cold.candidate_rows -lt $metrics.corpus_count -and
            $cold.pruning_milliseconds -gt 0 -and
            $cold.exact_scoring_milliseconds -gt 0 -and
            $cold.total_milliseconds -gt 0 -and
            $cold.wall_milliseconds -gt 0 -and
            $cold.returned_rows -eq $metrics.similarity.result_count
        $warmVerified = $null -ne $warm -and
            $warm.candidate_rows -gt 0 -and
            $warm.candidate_rows -le $metrics.database.candidate_pool_limit -and
            $warm.candidate_rows -lt $metrics.corpus_count -and
            $warm.pruning_milliseconds -gt 0 -and
            $warm.exact_scoring_milliseconds -gt 0 -and
            $warm.total_milliseconds -gt 0 -and
            $warm.wall_milliseconds -gt 0 -and
            $warm.returned_rows -eq $metrics.similarity.result_count
        $similarityVerified = $baseVerified -and
            $metrics.similarity.top_k -eq 100 -and
            $metrics.similarity.result_count -gt 0 -and
            $metrics.similarity.result_count -le $metrics.database.result_limit -and
            $metrics.similarity.reference_feature_store_calls -eq 2 -and
            $coldVerified -and $warmVerified
        return [pscustomobject]@{
            metrics_path = $Path
            metrics_present = $true
            corpus_count = [int]$metrics.corpus_count
            asset_rows = [int]$metrics.database.asset_rows
            visual_feature_rows = [int]$metrics.database.visual_feature_rows
            candidate_pool_limit = [int]$metrics.database.candidate_pool_limit
            visual_queries = $metrics.visual_queries
            similarity = $metrics.similarity
            pairwise_cache_table_count = [int]$metrics.pairwise_cache_table_count
            pairwise_cache_built = [bool]$metrics.pairwise_cache_built
            visual_query_100k_verified = [bool]$queryVerified
            similarity_100k_candidate_verified = [bool]$similarityVerified
        }
    }
    catch {
        return $empty
    }
}

function Read-SyntheticFixtureManifest {
    param([string]$Path)
    $invalid = [pscustomobject]@{
        manifest_path = $Path
        generated_count = 0
        unique_sha256_count = 0
        files = @()
        verified = $false
    }
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $invalid }
    try {
        $manifestPath = [System.IO.Path]::GetFullPath($Path)
        $fixtureRoot = [System.IO.Path]::GetDirectoryName($manifestPath)
        $fixturePrefix = $fixtureRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $files = @()
        $hashes = @{}
        foreach ($entry in @($manifest.files)) {
            $relativePath = [string]$entry.relative_path
            $rooted = [System.IO.Path]::IsPathRooted($relativePath)
            $resolvedPath = if ($rooted) { $null } else { [System.IO.Path]::GetFullPath((Join-Path $fixtureRoot ($relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))) }
            $withinRoot = $null -ne $resolvedPath -and $resolvedPath.StartsWith($fixturePrefix, [StringComparison]::OrdinalIgnoreCase)
            $exists = $withinRoot -and (Test-Path -LiteralPath $resolvedPath -PathType Leaf)
            $validJpeg = $false
            $hasExif = $true
            $actualHash = $null
            if ($exists) {
                $bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
                $validJpeg = $bytes.Length -ge 3 -and $bytes[0] -eq 255 -and $bytes[1] -eq 216 -and $bytes[2] -eq 255
                $latin = [System.Text.Encoding]::GetEncoding(28591).GetString($bytes)
                $hasExif = $latin.IndexOf("Exif`0`0", [StringComparison]::Ordinal) -ge 0
                $actualHash = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash
            }
            $declaredHash = [string]$entry.sha256
            $duplicate = $hashes.ContainsKey($declaredHash)
            if (-not $duplicate) { $hashes[$declaredHash] = $relativePath }
            $verified = -not $rooted -and $withinRoot -and $exists -and $validJpeg -and -not $hasExif -and
                $declaredHash.Length -eq 64 -and $declaredHash -eq $actualHash -and -not $duplicate -and
                $entry.exif_present -eq $false
            $files += [pscustomobject]@{
                relative_path = $relativePath
                declared_sha256 = $declaredHash
                actual_sha256 = $actualHash
                valid_jpeg = $validJpeg
                exif_present = $hasExif
                duplicate = $duplicate
                verified = [bool]$verified
            }
        }
        $allFilesVerified = @($files | Where-Object verified -eq $false).Count -eq 0
        $manifestVerified = $manifest.schema -eq 'pixel-tart-modular-harness-v1-synthetic-fixture/v1' -and
            $manifest.synthetic_only -eq $true -and
            $manifest.customer_media -eq $false -and
            $manifest.generated_count -eq 12 -and
            $files.Count -eq 12 -and
            $hashes.Count -eq 12 -and
            $allFilesVerified
        return [pscustomobject]@{
            manifest_path = $manifestPath
            generated_count = $files.Count
            unique_sha256_count = $hashes.Count
            files = $files
            verified = [bool]$manifestVerified
        }
    }
    catch {
        return $invalid
    }
}

function Read-AssetImportDiagnostics {
    param([string]$Path, [int]$ExpectedCount)
    $invalid = [pscustomobject]@{
        diagnostics_path = $Path
        source_kind = $null
        selected_file_count = 0
        imported_count = 0
        current_query_count = 0
        view_model_item_count = 0
        asset_grid_item_count = 0
        verified = $false
    }
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $invalid }
    try {
        $diagnosticsPath = [System.IO.Path]::GetFullPath($Path)
        $diagnostics = Get-Content -LiteralPath $diagnosticsPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $extensionCount = 0
        foreach ($property in @($diagnostics.scanned_extension_counts.PSObject.Properties)) {
            if ($property.Name -in @('.jpg', '.jpeg')) { $extensionCount += [int]$property.Value }
        }
        $verified = $diagnostics.source_kind -eq 'synthetic-directory-recursive' -and
            $diagnostics.picker_accepted -eq $true -and
            $diagnostics.import_command_entered -eq $true -and
            $diagnostics.import_service_entered -eq $true -and
            $diagnostics.selected_file_count -eq $ExpectedCount -and
            $diagnostics.imported_count -eq $ExpectedCount -and
            $diagnostics.skipped_count -eq 0 -and
            $diagnostics.failed_count -eq 0 -and
            ($diagnostics.repository_asset_count_after - $diagnostics.repository_asset_count_before) -eq $ExpectedCount -and
            $diagnostics.current_query_count -eq $ExpectedCount -and
            $diagnostics.view_model_item_count -eq $ExpectedCount -and
            $diagnostics.asset_grid_item_count -eq $ExpectedCount -and
            $diagnostics.items_source_instance -eq 'AssetCards' -and
            $diagnostics.items_source_is_view_model_collection -eq $true -and
            $diagnostics.data_context_type -eq 'AssetLibraryViewModel' -and
            $diagnostics.selected_collection -eq 'AllAssets' -and
            $extensionCount -eq $ExpectedCount
        return [pscustomobject]@{
            diagnostics_path = $diagnosticsPath
            source_kind = [string]$diagnostics.source_kind
            selected_file_count = [int]$diagnostics.selected_file_count
            imported_count = [int]$diagnostics.imported_count
            current_query_count = [int]$diagnostics.current_query_count
            view_model_item_count = [int]$diagnostics.view_model_item_count
            asset_grid_item_count = [int]$diagnostics.asset_grid_item_count
            verified = [bool]$verified
        }
    }
    catch {
        return $invalid
    }
}

function Read-TaskCenterLifecycle {
    param([pscustomobject]$Foreground)
    $invalid = [pscustomobject]@{
        database_path = $null
        task_id = $null
        display_name = $null
        input_snapshot = $null
        state = $null
        progress = 0
        created_at = $null
        started_at = $null
        completed_at = $null
        result_summary = $null
        verification_source = $null
        database_isolated_root_verified = $false
        queued_transition_persisted_verified = $false
        running_transition_persisted_verified = $false
        completed_transition_persisted_verified = $false
        queued_foreground_observed = $false
        running_foreground_observed = $false
        completed_foreground_observed = $false
        verified = $false
    }
    try {
        $databasePath = [System.IO.Path]::GetFullPath([string]$Foreground.task_center_database_path)
        $acceptanceRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'PixelTart_ModularHarness_V1_Acceptance'))
        $acceptancePrefix = $acceptanceRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $databaseVerified = $databasePath.StartsWith($acceptancePrefix, [StringComparison]::OrdinalIgnoreCase) -and
            [System.IO.Path]::GetFileName($databasePath) -eq 'pixel-tart.db' -and
            (Test-Path -LiteralPath $databasePath -PathType Leaf)

        $taskId = [Guid]::Empty
        $taskIdVerified = [Guid]::TryParse([string]$Foreground.task_center_task_id, [ref]$taskId) -and $taskId -ne [Guid]::Empty
        $createdAt = [DateTimeOffset]::MinValue
        $startedAt = [DateTimeOffset]::MinValue
        $completedAt = [DateTimeOffset]::MinValue
        $createdVerified = [DateTimeOffset]::TryParse([string]$Foreground.task_center_task_created_at, [ref]$createdAt)
        $startedVerified = [DateTimeOffset]::TryParse([string]$Foreground.task_center_task_started_at, [ref]$startedAt)
        $completedVerified = [DateTimeOffset]::TryParse([string]$Foreground.task_center_task_completed_at, [ref]$completedAt)
        $timestampsVerified = $createdVerified -and $startedVerified -and $completedVerified -and
            $createdAt -le $startedAt -and $startedAt -le $completedAt

        $summary = $Foreground.task_center_task_result_summary
        $summaryVerified = $null -ne $summary -and
            $summary.total -eq 12 -and $summary.succeeded -eq 12 -and
            $summary.failed -eq 0 -and $summary.skipped -eq 0 -and $summary.cancelled -eq 0
        $verificationSource = [string]$Foreground.task_center_verification_source
        $databaseIsolated = $Foreground.task_center_database_isolated_root_verified -eq $true -and $databaseVerified
        $queuedPersistence = $Foreground.task_center_queued_transition_persisted_verified -eq $true -and $createdVerified
        $runningPersistence = $Foreground.task_center_running_transition_persisted_verified -eq $true -and $startedVerified -and $startedAt -ge $createdAt
        $completedPersistence = $Foreground.task_center_completed_transition_persisted_verified -eq $true -and $completedVerified -and $completedAt -ge $startedAt
        $queuedForeground = $Foreground.task_center_queued_foreground_observed -eq $true
        $runningForeground = $Foreground.task_center_running_foreground_observed -eq $true
        $completedForeground = $Foreground.task_center_completed_foreground_observed -eq $true
        # Keep this comparison encoding-independent when Windows PowerShell parses
        # this UTF-8-without-BOM script using the system code page.
        $expectedTaskDisplayName = -join ([char[]]@(0x7D20, 0x6750, 0x5E93, 0x20, 0x00B7, 0x20, 0x6279, 0x91CF, 0x89C6, 0x89C9, 0x5206, 0x6790))
        $verified = $Foreground.task_center_foreground_triggered -eq $true -and
            $Foreground.global_task_center_lifecycle_verified -eq $true -and
            $databaseIsolated -and $verificationSource -eq 'foreground_action+sqlite_audit' -and
            $taskIdVerified -and $timestampsVerified -and $summaryVerified -and
            $Foreground.task_center_task_display_name -eq $expectedTaskDisplayName -and
            $Foreground.task_center_task_input_snapshot -eq 'asset-library scope=Current; count=12' -and
            $Foreground.task_center_task_state -eq 'Completed' -and
            [double]$Foreground.task_center_task_progress -eq 100 -and
            $queuedPersistence -and $runningPersistence -and $completedPersistence -and
            -not $queuedForeground -and -not $runningForeground -and $completedForeground

        return [pscustomobject]@{
            database_path = $databasePath
            task_id = if ($taskIdVerified) { $taskId.ToString('D') } else { $null }
            display_name = [string]$Foreground.task_center_task_display_name
            input_snapshot = [string]$Foreground.task_center_task_input_snapshot
            state = [string]$Foreground.task_center_task_state
            progress = [double]$Foreground.task_center_task_progress
            created_at = if ($createdVerified) { $createdAt.ToString('O') } else { $null }
            started_at = if ($startedVerified) { $startedAt.ToString('O') } else { $null }
            completed_at = if ($completedVerified) { $completedAt.ToString('O') } else { $null }
            result_summary = $summary
            verification_source = $verificationSource
            database_isolated_root_verified = [bool]$databaseIsolated
            queued_transition_persisted_verified = [bool]$queuedPersistence
            running_transition_persisted_verified = [bool]$runningPersistence
            completed_transition_persisted_verified = [bool]$completedPersistence
            queued_foreground_observed = [bool]$queuedForeground
            running_foreground_observed = [bool]$runningForeground
            completed_foreground_observed = [bool]$completedForeground
            verified = [bool]$verified
        }
    }
    catch {
        return $invalid
    }
}

function Test-PngHasForbiddenMetadata {
    param([byte[]]$Bytes)
    $offset = 8
    while ($offset + 12 -le $Bytes.Length) {
        $length = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($Bytes, $offset))
        if ($length -lt 0 -or $offset + 12 + $length -gt $Bytes.Length) { return $true }
        $type = [System.Text.Encoding]::ASCII.GetString($Bytes, $offset + 4, 4)
        if ($type -in @('tEXt', 'zTXt', 'iTXt', 'eXIf')) { return $true }
        $offset += 12 + $length
        if ($type -eq 'IEND') { return $false }
    }
    return $true
}

function Test-ForegroundEvidence {
    param([string]$Root)
    $requiredFiles = @(
        '01_workbench.png',
        '02_toolbox_asset_library.png',
        '03_embedded_asset_library.png',
        '04_visual_analysis_palette.png',
        '05_visual_analysis_histogram.png',
        '06_visual_analysis_tone.png',
        '07_visual_filter.png',
        '08_visual_similarity.png',
        '09_return_workbench.png',
        '10_module_diagnostics.png'
    )
    $missing = @()
    $invalid = @()
    $presentPngNames = if (Test-Path -LiteralPath $Root -PathType Container) {
        @(Get-ChildItem -LiteralPath $Root -File -Filter '*.png' | Select-Object -ExpandProperty Name)
    }
    else { @() }
    $unexpected = @($presentPngNames | Where-Object { $_ -notin $requiredFiles })
    $hashes = @{}
    $evidenceItems = @()
    foreach ($fileName in $requiredFiles) {
        $path = Join-Path $Root $fileName
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            $missing += $fileName
            continue
        }
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $validSignature = $bytes.Length -ge 8 -and ($bytes[0..7] -join ',') -eq '137,80,78,71,13,10,26,10'
        $forbiddenMetadata = $true
        if ($validSignature) { $forbiddenMetadata = Test-PngHasForbiddenMetadata -Bytes $bytes }
        $latin = [System.Text.Encoding]::GetEncoding(28591).GetString($bytes)
        $sensitiveMarker = $null
        foreach ($marker in @('C:\Users\', '<WORKSPACE>', 'LocalAppData', 'GPS', 'customer', 'token', 'DSC0')) {
            if ($latin.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $sensitiveMarker = $marker
                break
            }
        }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        $duplicate = $hashes.ContainsKey($hash)
        if (-not $duplicate) { $hashes[$hash] = $fileName }
        $valid = $validSignature -and -not $forbiddenMetadata -and $null -eq $sensitiveMarker -and -not $duplicate
        if (-not $valid) { $invalid += $fileName }
        $evidenceItems += [pscustomobject]@{
            file_name = $fileName
            sha256 = $hash
            valid_png = $validSignature
            forbidden_metadata = $forbiddenMetadata
            sensitive_marker = $sensitiveMarker
            duplicate = $duplicate
            verified = $valid
        }
    }
    $manifestPath = Join-Path $script:acceptanceRepositoryRoot 'tools\ModularHarnessV1Acceptance\evidence-contract.json'
    $captureStatus = $null
    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        try { $captureStatus = (Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json).capture_status }
        catch { $captureStatus = $null }
    }
    return [pscustomobject]@{
        evidence_root = $Root
        required_count = $requiredFiles.Count
        present_count = $evidenceItems.Count
        unique_count = $hashes.Count
        missing_files = $missing
        invalid_files = $invalid
        unexpected_files = $unexpected
        capture_status = $captureStatus
        files = $evidenceItems
        verified = $missing.Count -eq 0 -and $invalid.Count -eq 0 -and $unexpected.Count -eq 0 -and
            $presentPngNames.Count -eq $requiredFiles.Count -and $hashes.Count -eq $requiredFiles.Count -and $captureStatus -eq 'captured'
    }
}

function Read-ProcessSnapshot {
    param([string]$Path, [string]$Stage)
    $invalid = [pscustomobject]@{
        stage = $Stage
        path = $Path
        root_process_id = $null
        process_count = 0
        descendant_process_ids = @()
        gui_process_ids = @()
        matching_executable_process_ids = @()
        verified = $false
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $invalid }
    try {
        $snapshot = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        $processes = @($snapshot.processes)
        $descendants = @($snapshot.descendant_process_ids)
        $guiProcesses = @($snapshot.gui_process_ids)
        $matchingExecutableProcesses = @($snapshot.matching_executable_process_ids)
        $rootProcess = @($processes | Where-Object { $_.process_id -eq $snapshot.root_process_id })
        $verified = $snapshot.schema -eq 'pixel-tart-modular-harness-v1-process-snapshot/v1' -and
            $rootProcess.Count -eq 1 -and
            $rootProcess[0].executable_name -eq 'PixelTart_ModularHarness_V1_DevPreview.exe' -and
            $processes.Count -eq 1 -and
            $descendants.Count -eq 0 -and
            $guiProcesses.Count -eq 1 -and
            $guiProcesses[0] -eq $snapshot.root_process_id -and
            $matchingExecutableProcesses.Count -eq 1 -and
            $matchingExecutableProcesses[0] -eq $snapshot.root_process_id
        return [pscustomobject]@{
            stage = $Stage
            path = $Path
            root_process_id = [int]$snapshot.root_process_id
            process_count = $processes.Count
            descendant_process_ids = $descendants
            gui_process_ids = $guiProcesses
            matching_executable_process_ids = $matchingExecutableProcesses
            verified = [bool]$verified
        }
    }
    catch {
        return $invalid
    }
}

function Read-ForegroundResult {
    param(
        [string]$Path,
        [string]$BeforePath,
        [string]$AfterAssetPath,
        [string]$AfterReturnPath
    )
    $before = Read-ProcessSnapshot -Path $BeforePath -Stage 'before_asset'
    $afterAsset = Read-ProcessSnapshot -Path $AfterAssetPath -Stage 'after_asset'
    $afterReturn = Read-ProcessSnapshot -Path $AfterReturnPath -Stage 'after_return'
    $empty = [pscustomobject]@{
        result_path = $Path
        status = 'not_run'
        executable_sha256 = $null
        asset_module_sha256 = $null
        same_mainwindow_verified = $false
        synthetic_chain_verified = $false
        visual_smart_folder_verified = $false
        color_similarity_verified = $false
        palette_similarity_verified = $false
        global_task_center_queued_verified = $false
        global_task_center_running_verified = $false
        global_task_center_completed_verified = $false
        global_task_center_verified = $false
        task_center_lifecycle = $null
        module_diagnostics_verified = $false
        user_verified = $false
        synthetic_fixture_imported_count = 0
        synthetic_fixture = $null
        asset_import_diagnostics = $null
        verification_checks = $null
        process_snapshots = @($before, $afterAsset, $afterReturn)
        gui_process_count_before_asset = $before.gui_process_ids.Count
        gui_process_count_after_asset = $afterAsset.gui_process_ids.Count
        gui_process_count_after_return = $afterReturn.gui_process_ids.Count
        exact_child_process_enumeration_verified = $false
        verified = $false
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $empty }
    try {
        $foreground = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
        $evidenceContract = Get-Content -LiteralPath (Join-Path $script:acceptanceRepositoryRoot 'tools\ModularHarnessV1Acceptance\evidence-contract.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $syntheticFixture = Read-SyntheticFixtureManifest -Path ([string]$foreground.synthetic_fixture_manifest_path)
        $assetImportDiagnostics = Read-AssetImportDiagnostics -Path ([string]$foreground.asset_import_diagnostics_path) -ExpectedCount 12
        $visualSmartFolder = $foreground.visual_smart_folder_verified -eq $true
        $colorSimilarity = $foreground.color_similarity_verified -eq $true
        $paletteSimilarity = $foreground.palette_similarity_verified -eq $true
        $taskCenterLifecycle = Read-TaskCenterLifecycle -Foreground $foreground
        $globalTaskCenterQueued = $foreground.global_task_center_queued_verified -eq $true -and $taskCenterLifecycle.queued_transition_persisted_verified
        $globalTaskCenterRunning = $foreground.global_task_center_running_verified -eq $true -and $taskCenterLifecycle.running_transition_persisted_verified
        $globalTaskCenterCompleted = $foreground.global_task_center_completed_verified -eq $true -and $taskCenterLifecycle.completed_transition_persisted_verified -and $taskCenterLifecycle.completed_foreground_observed
        $globalTaskCenter = (
            $foreground.global_task_center_verified -eq $true -and
            $taskCenterLifecycle.verified -and
            $globalTaskCenterQueued -and
            $globalTaskCenterRunning -and
            $globalTaskCenterCompleted
        )
        $requiredActions = @(
            $foreground.workbench_to_toolbox_verified,
            $foreground.toolbox_to_asset_verified,
            $foreground.synthetic_reference_import_verified,
            $foreground.asset_grid_verified,
            $foreground.inspector_palette_verified,
            $foreground.inspector_histogram_verified,
            $foreground.inspector_tone_verified,
            $foreground.visual_filter_verified,
            $visualSmartFolder,
            $colorSimilarity,
            $paletteSimilarity,
            $foreground.visual_similarity_verified,
            $foreground.return_workbench_verified
        )
        $rootIds = @($before.root_process_id, $afterAsset.root_process_id, $afterReturn.root_process_id)
        $processVerified = @(@($before, $afterAsset, $afterReturn) | Where-Object verified -eq $false).Count -eq 0 -and
            @($rootIds | Select-Object -Unique).Count -eq 1
        $sameMainWindow = $foreground.same_mainwindow_verified -eq $true
        $syntheticChain = @($requiredActions | Where-Object { $_ -ne $true }).Count -eq 0
        $verificationChecks = [ordered]@{
            schema = $foreground.schema -eq 'pixel-tart-modular-harness-v1-foreground/v1'
            executable = $foreground.executable_name -eq 'PixelTart_ModularHarness_V1_DevPreview.exe'
            executable_sha256 = ([string]$foreground.executable_sha256).Length -eq 64
            asset_module_sha256 = ([string]$foreground.asset_module_sha256).Length -eq 64
            window_title = $foreground.window_title -eq $evidenceContract.expected_window_title
            synthetic_only = $foreground.synthetic_only -eq $true
            user_verified_false = $foreground.user_verified -eq $false
            imported_count_12 = $foreground.synthetic_fixture_imported_count -eq 12
            fixture_manifest = [bool]$syntheticFixture.verified
            import_diagnostics = [bool]$assetImportDiagnostics.verified
            same_mainwindow = [bool]$sameMainWindow
            synthetic_chain = [bool]$syntheticChain
            visual_smart_folder = [bool]$visualSmartFolder
            color_similarity = [bool]$colorSimilarity
            palette_similarity = [bool]$paletteSimilarity
            global_task_center_queued = [bool]$globalTaskCenterQueued
            global_task_center_running = [bool]$globalTaskCenterRunning
            global_task_center_completed = [bool]$globalTaskCenterCompleted
            global_task_center = [bool]$globalTaskCenter
            task_center_lifecycle = [bool]$taskCenterLifecycle.verified
            module_diagnostics = $foreground.module_diagnostics_verified -eq $true
            process_snapshots = [bool]$processVerified
        }
        $verified = @($verificationChecks.GetEnumerator() | Where-Object Value -eq $false).Count -eq 0
        return [pscustomobject]@{
            result_path = $Path
            status = if ($verified) { 'verified' } else { 'failed' }
            executable_sha256 = ([string]$foreground.executable_sha256).ToUpperInvariant()
            asset_module_sha256 = ([string]$foreground.asset_module_sha256).ToUpperInvariant()
            same_mainwindow_verified = [bool]$sameMainWindow
            synthetic_chain_verified = [bool]$syntheticChain
            visual_smart_folder_verified = [bool]$visualSmartFolder
            color_similarity_verified = [bool]$colorSimilarity
            palette_similarity_verified = [bool]$paletteSimilarity
            global_task_center_queued_verified = [bool]$globalTaskCenterQueued
            global_task_center_running_verified = [bool]$globalTaskCenterRunning
            global_task_center_completed_verified = [bool]$globalTaskCenterCompleted
            global_task_center_verified = [bool]$globalTaskCenter
            task_center_lifecycle = $taskCenterLifecycle
            module_diagnostics_verified = [bool]($foreground.module_diagnostics_verified -eq $true)
            user_verified = [bool]($foreground.user_verified -eq $true)
            synthetic_fixture_imported_count = [int]$foreground.synthetic_fixture_imported_count
            synthetic_fixture = $syntheticFixture
            asset_import_diagnostics = $assetImportDiagnostics
            verification_checks = $verificationChecks
            process_snapshots = @($before, $afterAsset, $afterReturn)
            gui_process_count_before_asset = $before.gui_process_ids.Count
            gui_process_count_after_asset = $afterAsset.gui_process_ids.Count
            gui_process_count_after_return = $afterReturn.gui_process_ids.Count
            exact_child_process_enumeration_verified = [bool]$processVerified
            verified = [bool]$verified
        }
    }
    catch {
        return $empty
    }
}

$script:acceptanceRepositoryRoot = Resolve-RepositoryRoot -Candidate $RepositoryRoot
$script:acceptanceDotnetHost = Resolve-DotnetHost -Root $script:acceptanceRepositoryRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path ([System.IO.Path]::GetTempPath()) (Join-Path 'PixelTart_ModularHarness_V1_Acceptance' ([DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')))
}
$script:acceptanceOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$script:acceptanceResultsRoot = Join-Path $script:acceptanceOutputRoot 'test-results'
$script:acceptanceLogsRoot = Join-Path $script:acceptanceOutputRoot 'logs'
[System.IO.Directory]::CreateDirectory($script:acceptanceResultsRoot) | Out-Null
[System.IO.Directory]::CreateDirectory($script:acceptanceLogsRoot) | Out-Null
if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    $EvidenceRoot = Join-Path $script:acceptanceRepositoryRoot 'ui-review\modular-harness'
}
$EvidenceRoot = [System.IO.Path]::GetFullPath($EvidenceRoot)
if ([string]::IsNullOrWhiteSpace($ForegroundResultPath)) {
    $ForegroundResultPath = Join-Path $script:acceptanceOutputRoot 'foreground-result.json'
}
if ([string]::IsNullOrWhiteSpace($ProcessSnapshotBeforeAssetPath)) {
    $ProcessSnapshotBeforeAssetPath = Join-Path $script:acceptanceOutputRoot 'process-before-asset.json'
}
if ([string]::IsNullOrWhiteSpace($ProcessSnapshotAfterAssetPath)) {
    $ProcessSnapshotAfterAssetPath = Join-Path $script:acceptanceOutputRoot 'process-after-asset.json'
}
if ([string]::IsNullOrWhiteSpace($ProcessSnapshotAfterReturnPath)) {
    $ProcessSnapshotAfterReturnPath = Join-Path $script:acceptanceOutputRoot 'process-after-return.json'
}

$startedAt = [DateTimeOffset]::Now
$suiteDefinitions = @(
    [pscustomobject]@{
        name = 'harness-focused'
        project = 'tests\PixelTart.ModularHarness.Tests\PixelTart.ModularHarness.Tests.csproj'
        filter = ''
    },
    [pscustomobject]@{
        name = 'asset-focused'
        project = 'tests\RAWSelectionAssistant.Tests\RAWSelectionAssistant.Tests.csproj'
        filter = 'FullyQualifiedName~AssetLibrary&FullyQualifiedName!~AssetLibraryV16&FullyQualifiedName!~VisualAnalysis'
    },
    [pscustomobject]@{
        name = 'visual-focused'
        project = 'tests\RAWSelectionAssistant.Tests\RAWSelectionAssistant.Tests.csproj'
        filter = 'FullyQualifiedName~VisualAnalysis|FullyQualifiedName~AssetLibraryV16'
    },
    [pscustomobject]@{
        name = 'wpf-embedded'
        project = 'tests\RAWSelectionAssistant.WpfTests\RAWSelectionAssistant.WpfTests.csproj'
        filter = 'FullyQualifiedName~ModularHarnessEmbeddedEvidenceContractTests|FullyQualifiedName~AssetLibrary'
    },
    [pscustomobject]@{
        name = 'visual-scale-100k'
        project = 'tests\RAWSelectionAssistant.Tests\RAWSelectionAssistant.Tests.csproj'
        filter = 'FullyQualifiedName~ModularHarnessVisualScaleAcceptanceTests'
    }
)

if (-not $SkipRestore) {
    $restore = Invoke-DotnetCommand -Arguments @('restore', (Join-Path $script:acceptanceRepositoryRoot 'RAWSelectionAssistant.sln'), '--nologo') -LogPath (Join-Path $script:acceptanceLogsRoot 'restore.log')
    if ($restore.exit_code -ne 0) { throw "Restore failed. See $($restore.log_path)" }
}

$scaleMetricsPath = Join-Path $script:acceptanceOutputRoot 'visual-scale-100k.metrics.json'
$previousMetricsPath = $env:PIXEL_TART_MODULAR_HARNESS_METRICS_PATH
$previousForegroundResultPath = $env:PIXEL_TART_MODULAR_HARNESS_FOREGROUND_RESULT_PATH
$suiteResults = @()
try {
    foreach ($definition in $suiteDefinitions) {
        if ($definition.name -eq 'visual-scale-100k') {
            $env:PIXEL_TART_MODULAR_HARNESS_METRICS_PATH = $scaleMetricsPath
        }
        else {
            Remove-Item Env:PIXEL_TART_MODULAR_HARNESS_METRICS_PATH -ErrorAction SilentlyContinue
        }
        if ($definition.name -eq 'wpf-embedded') {
            $env:PIXEL_TART_MODULAR_HARNESS_FOREGROUND_RESULT_PATH = [System.IO.Path]::GetFullPath($ForegroundResultPath)
        }
        else {
            Remove-Item Env:PIXEL_TART_MODULAR_HARNESS_FOREGROUND_RESULT_PATH -ErrorAction SilentlyContinue
        }
        $suiteResults += Invoke-TestSuite -Definition $definition
    }
}
finally {
    if ($null -eq $previousMetricsPath) {
        Remove-Item Env:PIXEL_TART_MODULAR_HARNESS_METRICS_PATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PIXEL_TART_MODULAR_HARNESS_METRICS_PATH = $previousMetricsPath
    }
    if ($null -eq $previousForegroundResultPath) {
        Remove-Item Env:PIXEL_TART_MODULAR_HARNESS_FOREGROUND_RESULT_PATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PIXEL_TART_MODULAR_HARNESS_FOREGROUND_RESULT_PATH = $previousForegroundResultPath
    }
}

$identity = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$uniquePassed = 0
$uniqueFailed = 0
$uniqueSkipped = 0
foreach ($suite in $suiteResults) {
    foreach ($case in $suite.cases) {
        $key = "$($suite.project)::$($case.test_id)"
        if (-not $identity.Add($key)) { continue }
        switch ($case.outcome) {
            'Passed' { $uniquePassed++ }
            'Failed' { $uniqueFailed++ }
            default { $uniqueSkipped++ }
        }
    }
}

$buildResults = @()
if (-not $SkipProductBuilds) {
    $buildResults += Invoke-ProductBuild -Configuration 'Debug'
    $buildResults += Invoke-ProductBuild -Configuration 'Release'
}

$publishResult = $null
if (-not $SkipPublish) { $publishResult = Invoke-DevPreviewPublish }
$scaleMetrics = Read-ScaleMetrics -Path $scaleMetricsPath
$evidenceResult = Test-ForegroundEvidence -Root $EvidenceRoot
$foregroundResult = Read-ForegroundResult -Path $ForegroundResultPath -BeforePath $ProcessSnapshotBeforeAssetPath -AfterAssetPath $ProcessSnapshotAfterAssetPath -AfterReturnPath $ProcessSnapshotAfterReturnPath

$suiteReport = @($suiteResults | ForEach-Object {
    [pscustomobject]@{
        name = $_.name
        project = $_.project
        filter = $_.filter
        total = $_.total
        passed = $_.passed
        failed = $_.failed
        skipped = $_.skipped
        exit_code = $_.exit_code
        verified = $_.verified
        trx_path = $_.trx_path
        log_path = $_.log_path
    }
})

$allSuitesVerified = @($suiteResults | Where-Object verified -eq $false).Count -eq 0
$allBuildsVerified = $buildResults.Count -eq 2 -and @($buildResults | Where-Object verified -eq $false).Count -eq 0
$publishVerified = $null -ne $publishResult -and $publishResult.verified
$publishIdentityVerified = $publishVerified -and $foregroundResult.verified -and
    $foregroundResult.executable_sha256 -eq $publishResult.sha256 -and
    $foregroundResult.asset_module_sha256 -eq $publishResult.asset_module_sha256
$complete = $allSuitesVerified -and $allBuildsVerified -and $publishVerified -and
    $publishIdentityVerified -and
    $scaleMetrics.visual_query_100k_verified -and $scaleMetrics.similarity_100k_candidate_verified -and
    $evidenceResult.verified -and $foregroundResult.verified

$head = (& git -C $script:acceptanceRepositoryRoot rev-parse HEAD).Trim()
$branch = (& git -C $script:acceptanceRepositoryRoot branch --show-current).Trim()
$result = [ordered]@{
    schema = 'pixel-tart-modular-harness-v1-acceptance/v1'
    branch = $branch
    head = $head
    started_at = $startedAt.ToString('O')
    completed_at = [DateTimeOffset]::Now.ToString('O')
    output_root = $script:acceptanceOutputRoot
    dotnet_host = $script:acceptanceDotnetHost
    suites = $suiteReport
    current_run_unique_total = [ordered]@{
        total = $identity.Count
        passed = $uniquePassed
        failed = $uniqueFailed
        skipped = $uniqueSkipped
    }
    product_builds = $buildResults
    devpreview_publish = $publishResult
    visual_scale_100k = $scaleMetrics
    evidence = $evidenceResult
    foreground = $foregroundResult
    publish_identity_verified = [bool]$publishIdentityVerified
    explicit_false = [ordered]@{
        color_management_reference_verified = $false
        raw_visual_proxy_verified = $false
        user_verified = $false
        p0_merged = $false
        rc_generated = $false
    }
    complete = [bool]$complete
}

$resultPath = Join-Path $script:acceptanceOutputRoot 'modular-harness-v1.acceptance.json'
Write-Utf8WithoutBom -Path $resultPath -Content ($result | ConvertTo-Json -Depth 10)
Write-Output $resultPath
if (-not $complete) { exit 1 }
