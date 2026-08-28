# P1 最终人工验收

请在像素蛋挞与其他验收窗口全部关闭后，只执行一次：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\artifacts\manual-acceptance\P1_ASSET_LIBRARY_GATE_A_MANUAL_PACKET\Invoke-P1AssetLibraryGateAManualAcceptance.ps1" -Mode Run
```

启动前会显示 60 秒摘要。保持 PowerShell 在前台并移动鼠标一次，不需要按回车。

随后每次只按屏幕当前一条中文提示操作，完成后停手；脚本会自动进入下一步。不要重复点击或按键，也不要切回聊天。

如果失败或安全停止，只需回传屏幕最后显示的完整 `run root`，无需做其他调试。
