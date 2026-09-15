# Pixel Tart Product Language

Pixel Tart describes what a photographer is doing, not how the program implements it. This is a required UI language contract for new and changed product surfaces.

## Core rules

1. Ask whether a photographer needs the information to make the next decision. If not, hide it.
2. Prefer concise natural Chinese and “动词 + 对象” actions: 导入照片、整理照片、转换照片、新建文件夹、关联项目、查看拍摄、打开文件夹.
3. Keep one primary action per surface. Advanced and troubleshooting information is progressively disclosed.
4. Errors state what happened, what is affected and what the user can do next.
5. Safety behavior remains enforced even when its algorithm and identifiers are hidden.

## Required translations

| Internal / old wording | Product language |
|---|---|
| P3Query | Never shown |
| 规则组 | 满足以下所有条件 |
| 子组 | 满足以下任一条件, or natural condition-group wording |
| TaskId | Hidden; available only in 查看详细信息 |
| SHA-256 校验 | 安全校验 / 检查文件完整性 |
| LibRaw x.x / decoder name | Hidden; details only |
| Managed Copy | 已复制到素材库 |
| Reference / 原位引用 | 保留在原位置 |
| Image | Actual format: JPEG / PNG / TIFF / RAW |
| True / False / null | User-language state or hidden |
| Metadata Missing | 部分拍摄信息不可读取 |
| Conflict Policy | 同名文件处理 |
| Auto Number | 同名时自动重命名 |
| Execute | 开始 |
| Generate Manifest | 预览结果 |
| Cancel Operation | 停止 |
| Undo Selected Assets | 撤销操作 |
| Bytes | Automatically formatted KB / MB / GB |
| Available | 正常 |
| Offline | 源文件暂时不可访问 |
| Missing | 找不到源文件 |
| Managed | 已复制到素材库 |
| Processing | 正在处理 |
| Failed | 处理失败 |

## Tool structure

All photo tools use: title, one-sentence purpose, input, key settings, output, one primary action, collapsed advanced settings and task status. Use 开始压缩、开始转换、添加水印、开始整理. Do not use 执行任务、执行当前操作、提交 or 运行 Job.

## Details boundary

Complete paths, hashes, internal task IDs, decoder/backend versions, raw metadata, database references and technical exceptions belong only in **查看详细信息**. They must not appear in buttons, menus, labels, tooltips, toasts, Inspector summaries, Task Center cards, dialogs or empty states.

## Forbidden primary-UI terms

`P1`, `P2`, `P3`, `QueryOption`, `TaskId`, `LibraryId`, `AssetId`, `BookingId`, `ProjectId`, `LibRaw`, `SQLite`, `SHA-256`, `ContentHash`, raw `True` and raw `False`.

Internal source symbols, bindings, AutomationIds, logs, tests and explicit developer diagnostics may retain technical names. User-facing Automation Name and HelpText may not.

## Smart Folder language

- Default sentence: `满足： [全部满足 ▼]`.
- A condition is a single human-readable row: field, relationship, value.
- Use `＋ 添加条件`; introduce `添加条件组` only when the user asks for another logical group.
- Movement, exclusion, temporary disable and range-lock controls stay under `更多`.
- Result feedback reads `找到 N 张照片`; do not expose evaluation time, tree terminology or internal document names.

## Error language template

Every normal error answers three questions, in this order:

1. What happened: `无法读取 3 张照片`.
2. What is affected: `这些照片可能已被移动，或外置硬盘未连接。`
3. What to do next: `重新连接硬盘后点击“重试”。`

Stack traces, error codes and internal IDs belong in the collapsed `技术信息` section of `查看详细信息`.
