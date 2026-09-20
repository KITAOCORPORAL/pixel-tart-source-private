param([Parameter(Mandatory)][string]$PeerEvidence)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$output=Join-Path $repo 'docs\acceptance'
New-Item -ItemType Directory -Path $output -Force|Out-Null
$states=Get-Content -LiteralPath (Join-Path $PeerEvidence 'peer-states.json') -Raw|ConvertFrom-Json -AsHashtable
$catalog=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'selectors.json') -Raw|ConvertFrom-Json -AsHashtable
$sourceFile='src/RAWSelectionAssistant/Views/PlanningCenterView.xaml.cs'
$patternFor=@{invoke='Invoke';setValue='Value';select='SelectionItem';expand='ExpandCollapse';assertDate='Value'}
function Get-Stage($s) {
    if($s.Id -match '^date') { return 'DATE_PICKER' }
    if($s.Id -match '^create') { return 'CREATE_PLANNING_MODAL' }
    if($s.Id -match '^quick') { return 'QUICK_PREVIEW' }
    if($s.Id -match '^menu') { return 'CONTEXT_MENU' }
    if($s.Id -match '^booking') { return 'BOOKING_PICKER' }
    if($s.Id -match '^(export|quality|pdf|save-dialog|save-path|save-submit)') { return 'PDF_EXPORT' }
    if($s.Id -match '^(tether|shoot)') { return 'TETHER' }
    if($s.Id -match '^online') { return 'ONLINE_SELECTION' }
    if($s.Id -match '^import') { return 'REFERENCE_IMPORT' }
    if($s.Id -match '^reference') { return 'REFERENCE_ITEM' }
    if($s.Id -match '^preview') { return 'PREVIEW_MODE' }
    if($s.Id -match 'close') { return 'NORMAL_CLOSE' }
    if($s.Id -match 'restart|persist|save-') { return 'PERSISTENCE' }
    if($s.Id -match 'tutorial') { return 'ONBOARDING' }
    if($s.Id -match 'planning$|planning-enabled|back-planning') { return 'PRIMARY_NAVIGATION' }
    if($s.Id -match '^module') { return 'CONTENT_NAVIGATION' }
    if($s.Id -match 'moodboard') { return 'MOODBOARD' }
    if($s.Id -match 'lighting') { return 'LIGHTING' }
    if($s.Id -match 'styling') { return 'STYLING' }
    if($s.Id -match 'files') { return 'FILES' }
    if($s.Id -match 'shots|shot-new|shot-page') { return 'SHOT_LIST' }
    if($s.Action -eq 'upgrade') { return 'UPGRADE' }
    if($s.Id -match '^startup') { return 'BOOT' }
    if($s.Id -match 'list|select-created') { return 'PLANNING_LIST' }
    return 'TEXT_EDITOR'
}
function Match-Node($node,$selector) {
    if($selector.ControlType -and $node.Type -ne $selector.ControlType) { return $false }
    if($selector.AutomationId -and $node.AutomationId -ne $selector.AutomationId) { return $false }
    if($selector.Name -and $node.Name -ne $selector.Name) { return $false }
    return $true
}
function Scoped-Nodes($nodes,$selector) {
    $pool=@($nodes)
    $scopes=@($selector.ScopePath)
    switch($selector.ScopePreset) {
        'PrimaryNavigation' {$scopes=@(@{AutomationId='SidebarNavigationScroll';ControlType='Pane'})}
        'PlanningReferences' {$scopes=@(@{AutomationId='PlanningWorkspace';ControlType='Custom'},@{AutomationId='PlanningReferencesSurface';ControlType='Pane'})}
        'MainWindow' {}
    }
    if($selector.AncestorName -or $selector.AncestorControlType -or $selector.AncestorAutomationId) { $scopes+=@{Name=$selector.AncestorName;ControlType=$selector.AncestorControlType;AutomationId=$selector.AncestorAutomationId} }
    foreach($scope in $scopes) {
        if(!$scope) {continue}
        $matches=@($pool|Where-Object{Match-Node $_ $scope})
        if($matches.Count -ne 1) {return @()}
        $path=$matches[0].Path
        if($scope.Heading) {$path=$path.Substring(0,$path.LastIndexOf('/'))}
        $pool=@($pool|Where-Object{$_.Path -eq $path -or $_.Path.StartsWith($path+'/')})
    }
    return @($pool|Where-Object{Match-Node $_ $selector})
}
$v3=Get-Content -LiteralPath (Join-Path $repo 'artifacts\installed-acceptance-history\v3\20260920-143726-3bddf3\fresh\acceptance.json') -Raw|ConvertFrom-Json -AsHashtable
$results=@{}
foreach($name in @('planning-full','upgrade-full')) {
    $plan=Get-Content -LiteralPath (Join-Path $PSScriptRoot "$name.plan.json") -Raw|ConvertFrom-Json -AsHashtable
    $rows=foreach($step in $plan.Steps) {
        $sel=if($step.SelectorRef){$catalog[$step.SelectorRef]}else{@{}}
        $pattern=if($sel.RequiredPattern){$sel.RequiredPattern}else{$patternFor[$step.Action]}
        $evidence=@(); $status='NOT_APPLICABLE'; $risk='No UI selector; action/state evidence still required'; $matchCount=$null
        if($step.SelectorRef) {
            $status='PLAN_BUG'; $risk='No complete scoped peer evidence for this exact required contract; gate remains closed'
            foreach($state in $states.Keys) {
                $nodes=@(Scoped-Nodes $states[$state] $sel)
                if(!$step.IncludeOffscreen) {$nodes=@($nodes|Where-Object{!$_.Offscreen})}
                if($sel.DescendantName) {$nodes=@($nodes|Where-Object{$parent=$_.Path; @($states[$state]|Where-Object{$_.Path.StartsWith($parent+'/') -and $_.Name -eq $sel.DescendantName.Replace('{project}','安装验收策划')}).Count -eq 1})}
                if($nodes.Count -ne 1){continue}
                if($pattern -and $nodes[0].Patterns -notcontains $pattern){continue}
                if(($step.Action -eq 'focusKey' -or $sel.RequireFocusable) -and !$nodes[0].Focusable){continue}
                $evidence+=$state; $matchCount=1
            }
            if($evidence.Count -gt 0){$status='VERIFIED';$risk='Structural peer contract only; step-specific transition must also pass'}
            if($step.Action -in @('assertAbsent','waitAbsent')) {
                $negativeStates=@{'tutorial-gone'='BOOT';'create-closed'='CREATED_PROJECT';'preview-list-hidden'='FRESH_PREVIEW';'preview-nav-hidden'='FRESH_PREVIEW';'preview-edit-hidden'='FRESH_PREVIEW';'online-not-toolbox'='ONLINE_SELECTION'}
                $state=$negativeStates[$step.Id]
                $status='PLAN_BUG';$risk='Negative assertion requires state-specific absence proof; mere existence elsewhere is insufficient'
                if($state -and $states.ContainsKey($state)) {
                    $visible=@(Scoped-Nodes $states[$state] $sel|Where-Object{!$_.Offscreen})
                    if($visible.Count -eq 0){$status='VERIFIED';$evidence=@($state);$matchCount=0;$risk='Verified absent in named production peer state; installed transition remains separate'}
                }
            }
        }
        $historical=@($v3.Steps|Where-Object{$_.Step -eq $step.Id -and $_.Status -eq 'PASS'}).Count -eq 1
        [ordered]@{
            StepId=$step.Id; Stage=(Get-Stage $step); UserIntent=($step.Coverage ?? $step.Id); Action=$step.Action
            ExpectedUIState=if($step.Action -match 'Absent'){'Target absent in required state'}else{'Required state from formal plan; not implied by arbitrary peer snapshot'}
            Scope=@{Preset=$sel.ScopePreset;Path=$sel.ScopePath;AncestorName=$sel.AncestorName;AncestorType=$sel.AncestorControlType}
            AutomationId=$sel.AutomationId; AutomationName=$sel.Name; ControlType=$sel.ControlType; RequiredPattern=$pattern
            Focusable=($step.Action -eq 'focusKey' -or $sel.RequireFocusable -eq $true); KeyboardInteraction=if($step.Action -eq 'focusKey'){$step.Value}else{$null}
            PopupRoot=if($sel.ExternalDialog){'Owned PID Windows file dialog'}elseif($sel.AncestorControlType -eq 'Menu'){'Owned PID Menu'}else{$null}
            ExpectedMatchCount=if(!$step.SelectorRef){$null}elseif($step.Action -match 'Absent'){0}elseif($step.Optional){'0 or 1 (conditional)'}else{1}
            ObservedStructuralMatch=$matchCount; ProductSourceLocation=if($sel.ScopePreset -eq 'PrimaryNavigation'){'src/RAWSelectionAssistant/MainWindow.xaml'}else{$sourceFile}
            ContractEvidence=@{PeerStates=$evidence;PeerArtifact=[IO.Path]::GetRelativePath($repo,$PeerEvidence).Replace('\','/');HistoricalV3StepPass=$historical;HistoricalProduct='8cb95e6';CurrentInstalledProof=$false}
            Risk=$risk; Status=$status; SelectorRef=$step.SelectorRef; StateTransition='PENDING_PER_STEP_REPLAY'
        }
    }
    $title=if($name -eq 'planning-full'){'PLANNING_FULL_AUTOMATION_CONTRACT'}else{'UPGRADE_FULL_AUTOMATION_CONTRACT'}
    $summary=[ordered]@{Count=@($rows).Count;Verified=@($rows|Where-Object Status -eq 'VERIFIED').Count;AccessibilityGap=@($rows|Where-Object Status -eq 'ACCESSIBILITY_GAP').Count;PlanBug=@($rows|Where-Object Status -eq 'PLAN_BUG').Count;NotApplicable=@($rows|Where-Object Status -eq 'NOT_APPLICABLE').Count;Assumed=0;CandidateGate='FAIL';Rows=@($rows)}
    $summary|ConvertTo-Json -Depth 18|Set-Content -LiteralPath (Join-Path $output "$title.json") -Encoding utf8
    $lines=@("# $title",'', 'Interim exhaustive structural inventory, not a passed readiness gate. VERIFIED means an exact scoped/type/pattern match in production peer snapshots, not that the formal step transition was replayed. PLAN_BUG includes missing proof/contract coverage; it does not assert a product defect. NOT_APPLICABLE means selector-free only. Native OS dialogs and upgrade execution remain unproven.', '', '| Step | Stage | Action | Selector | Pattern | Status | Peer evidence |','| --- | --- | --- | --- | --- | --- | --- |')
    foreach($r in $rows){$lines+="| $($r.StepId) | $($r.Stage) | $($r.Action) | $($r.SelectorRef) | $($r.RequiredPattern) | $($r.Status) | $($r.ContractEvidence.PeerStates -join ', ') |"}
    $lines|Set-Content -LiteralPath (Join-Path $output "$title.md") -Encoding utf8
    $results[$name]=$summary
    Write-Host "$name $($summary.Count) steps: $($summary.Verified) structural verified / $($summary.PlanBug) incomplete contracts / $($summary.NotApplicable) no selector"
}
