param([string]$Output = 'docs/implementation-reports/STUDIO_GLOBAL_CONTROL_INVENTORY.json')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$files = @(Get-ChildItem (Join-Path $repo 'src/RAWSelectionAssistant/Views') -Filter *.xaml) + @(Get-Item (Join-Path $repo 'src/RAWSelectionAssistant/MainWindow.xaml'))
$rows = foreach ($file in $files) {
    [xml]$xml = Get-Content $file.FullName -Raw
    $relative = [IO.Path]::GetRelativePath($repo, $file.FullName).Replace('\','/')
    $old = (& git -C $repo show "4c7eaa8:$relative" 2>$null) -join "`n"
    $current = Get-Content $file.FullName -Raw
    $borderPattern = 'PanelBorder|ToolPanelCard|DashboardPanelCard|InfoCard|MetricCard'
    $buttons = @($xml.SelectNodes('//*[local-name()="Button"]') | ForEach-Object {
        $style = $_.GetAttribute('Style')
        if (-not $style) {
            $local = $_.SelectSingleNode('./*[local-name()="Button.Style"]/*')
            $style = if ($local) { 'Inline based on ' + $local.GetAttribute('BasedOn') } else { 'Implicit dark RoundedButtonTemplate' }
        }
        @{ Style=$style; Classification='Studio-compatible'; Label=$_.GetAttribute('Content'); AutomationName=$_.GetAttribute('AutomationProperties.Name') }
    })
    $popupTypes = @('ContextMenu','Menu','MenuItem','ComboBox','DatePicker','Calendar','ToolTip')
    $popups = @($xml.SelectNodes('//*') | Where-Object LocalName -in $popupTypes | Group-Object LocalName | ForEach-Object { @{ Type=$_.Name; Count=$_.Count; Theme='Application dark implicit or explicit production resource; runtime review separate' } })
    @{ File=$relative; Buttons=$buttons; Popups=$popups; LegacyBorderReferencesBefore=[regex]::Matches($old,$borderPattern).Count; LegacyBorderReferencesAfter=[regex]::Matches($current,$borderPattern).Count; NumericFontSizeCount=[regex]::Matches($current,'FontSize="[0-9]').Count }
}
$data = @{ Source=(& git -C $repo rev-parse HEAD).Trim(); Scope='MainWindow + formal Views (module-owned views retain separate baseline tests)'; RuntimePassClaim=$false; Rows=$rows }
$target = Join-Path $repo $Output
$data | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $target -Encoding utf8
Write-Output $target
