# Pixel Tart RC12 Installer Report

## 结论

`2.3.0-RC12` 安装包已从 Asset Library UX Closure 源码生成，并完成两轮隔离安装烟测和一轮当前 4K Windows 电脑安装验证。安装、启动、正常关闭、卸载与重新安装均通过。

本报告只证明安装生命周期，不代替 Asset Library 的真人交互验收。

本轮参考仿色工作区收口后再次完成了 Release 编译；安装包仍为本轮生成的 RC12 自包含 x64 包，未改变安装目录结构或卸载行为。

## 安装包身份

| 项目 | 值 |
|---|---|
| 用户要求文件名 | `artifacts/releases/2.3.0/installer/Pixel Tart_Setup_2.3.0_RC12_x64.exe` |
| 品牌文件名 | `artifacts/releases/2.3.0/installer/像素蛋挞_Setup_2.3.0_RC12_x64.exe` |
| 文件大小 | 51,214,323 bytes |
| SHA-256 | `FF3FDC70C81C8B433348013AB4944A6F9527AA7CF487B5676799F9BD80AACFE0` |
| 产品版本 | `2.3.0-RC12` |
| Closure source | `b8552458818d8540ad6399318ae32605046e0ad6` |
| 最新已提交文档基线 | `84d28dfad0726c917252a8d2d921795e85bc3171` |

两个文件名对应同一二进制，大小和 SHA-256 完全一致。`b855245..84d28df` 只增加验收报告，`src/`、`installer/` 与 `build_rc12.ps1` 没有差异，因此安装包仍精确对应 closure 产品源码。

## 验证结果

| 轮次 | 安装 | 启动 | 关闭 | 卸载 | 安装目录清除 | 结果 |
|---|---:|---:|---:|---:|---:|---:|
| 隔离安装 1 | exit 0 | 主窗口 `像素蛋挞` | exit 0 | exit 0 | 是 | PASS |
| 隔离重装 2 | exit 0 | 主窗口 `像素蛋挞` | exit 0 | exit 0 | 是 | PASS |
| 当前 4K 电脑 | exit 0 | 主窗口 `像素蛋挞`，进程响应正常 | 正常退出 | exit 0 | 是 | PASS |

当前电脑：Windows 10 专业版 22H2（build 19045）、x64、3840×2160 @ 60 Hz、系统 DPI 96（100%）。

## 原始证据

- `artifacts/rc12-real-user-acceptance/installer-smoke/result.json`
- `artifacts/rc12-real-user-acceptance/installer-smoke-reinstall/result.json`
- `artifacts/rc12-real-user-acceptance/physical-machine-4k-20260916/result.json`
- `artifacts/releases/2.3.0/installer/rc12-ux-candidate-current-head.json`

当前 4K 安装使用全新隔离目录；验收结束后已正常关闭并卸载，安装目录已移除。没有修改客户素材或历史验收数据。
