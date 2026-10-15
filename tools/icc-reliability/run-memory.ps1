$root='E:/Kitao-Worktrees/kitao-icc-reliability-20261015'
$tool="$root/tools/ControlledIccWorkflow/bin/Release/net10.0-windows10.0.19041.0/ControlledIccWorkflow.dll"
$photo='D:/AI AGENT/kitao-controlled-icc-20261015/artifacts/controlled-icc/canon-linear-headroom.tif'
$working="$root/tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-prophoto.icc"
$output="$root/tests/RAWSelectionAssistant.Tests/TestData/controlled-linear-srgb.icc"
$dll='D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/icc/lcms/Library/bin/lcms2.dll'
foreach($mode in @('old','bounded')){
 $env:KITAO_ICC_MEMORY_LOG="$root/artifacts/icc-reliability/memory-$mode.json"
 $switch=if($mode -eq 'old'){'--explicit-research'}else{'--bounded-research'}
 & 'D:/AI AGENT/.dotnet/dotnet.exe' $tool $switch $photo "$root/artifacts/icc-reliability/photo-$mode" $dll $working $output *> "$root/artifacts/icc-reliability/photo-$mode.log"
 Write-Output "$mode exit=$LASTEXITCODE"
}
Remove-Item Env:KITAO_ICC_MEMORY_LOG
