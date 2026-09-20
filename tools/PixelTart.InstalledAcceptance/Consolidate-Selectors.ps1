# One-time mechanical migration. Plans become references into one reviewed catalog.
$ErrorActionPreference = 'Stop'
$fields = @('IdSelector','Name','ControlType','AutomationId','AncestorAutomationId','AncestorName','AncestorControlType','ScopeAnchorName','DescendantName','HelpText','ExternalDialog','ScopePath','ScopePreset','RequiredPattern','RequireFocusable')
$catalog = [ordered]@{}
$signatures = @{}
foreach ($name in @('planning-full','upgrade-full')) {
    $path = Join-Path $PSScriptRoot "$name.plan.json"
    $plan = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable
    if ($plan.Contains('SelectorCatalog')) { throw 'Already consolidated; edit selectors.json directly.' }
    $all = @($plan.Steps) + @($plan.AuditCheckpoints.Values | ForEach-Object { $_ })
    foreach ($step in $all) {
        if ($step.Name -eq '参考图：验收参考') {
            $step.ControlType = 'Button'
            $step.Remove('ScopePath'); $step.ScopePreset = 'PlanningReferences'
            $step.RequiredPattern = 'Invoke'; $step.RequireFocusable = $true
        }
        $selector = [ordered]@{}
        foreach ($field in $fields) { if ($step.Contains($field)) { $selector[$field] = $step[$field]; $step.Remove($field) } }
        if ($selector.Count -eq 0) { continue }
        $signature = $selector | ConvertTo-Json -Depth 16 -Compress
        if (!$signatures.ContainsKey($signature)) {
            $key = 'selector-' + $step.Id
            if ($catalog.Contains($key)) { $key = "$key-$name" }
            $catalog[$key] = $selector; $signatures[$signature] = $key
        }
        $step.SelectorRef = $signatures[$signature]
    }
    $plan.SelectorCatalog = 'selectors.json'
    [IO.File]::WriteAllText($path, ($plan | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'selectors.json'), ($catalog | ConvertTo-Json -Depth 20) + "`n", [Text.UTF8Encoding]::new($false))
# Mechanical source migration; every entry point must expand the same catalog.
foreach ($name in @('Program.cs','Workflow.cs','SelectorAudit.cs','NavigationLint.cs')) {
    $path = Join-Path $PSScriptRoot $name; $source = [IO.File]::ReadAllText($path)
    $source = $source.Replace('JsonSerializer.Deserialize<Plan>(File.ReadAllText(Child(PlanDirectory, "planning-full.plan.json")), Json)!','LoadPlan(Child(PlanDirectory, "planning-full.plan.json"))')
    $source = $source.Replace('JsonSerializer.Deserialize<Plan>(File.ReadAllText(Child(PlanDirectory, file)), Json)!','LoadPlan(Child(PlanDirectory, file))')
    $source = $source.Replace('JsonSerializer.Deserialize<Plan>(File.ReadAllText(args[1]), Json) ?? throw new InvalidDataException("Empty plan")','LoadPlan(args[1])')
    $source = $source.Replace('JsonSerializer.Deserialize<Plan>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, file)), Json)!','LoadPlan(Path.Combine(AppContext.BaseDirectory, file))')
    $source = $source.Replace('JsonSerializer.Deserialize<Plan>(File.ReadAllText(file),Json)!','LoadPlan(file)')
    $source = $source.Replace('JsonSerializer.Deserialize<Plan>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"planning-full.plan.json")),Json)!','LoadPlan(Path.Combine(AppContext.BaseDirectory,"planning-full.plan.json"))')
    [IO.File]::WriteAllText($path, $source, [Text.UTF8Encoding]::new($false))
}
