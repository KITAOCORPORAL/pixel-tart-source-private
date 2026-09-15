# Pixel Tart UX Product Language Audit

Date: 2026-09-15  
Scope: RC12 user-visible XAML, primary ViewModel status text, dialogs, menus, tooltips, empty states, Inspector and Task Center. Internal identifiers, bindings, logs, tests and collapsed troubleshooting details are not product-language leaks.

## Findings

| Current UI Text | Location | Problem | User Meaning | Replacement | Decision |
|---|---|---|---|---|---|
| 使用 SHA-256 校验 | OrganizePhotosView | Exposes implementation | Confirm copied photos are complete | 文件确认复制完整后才继续处理原文件 | Hide |
| 复制后同时校验 SHA-256（更慢） | TetherCaptureView | Exposes algorithm | Safer copy | 复制完成后检查文件完整性 | Replace |
| TaskId `{guid}` | RawToJpegViewModel | Internal ID in failure status | Conversion needs attention | 转换未完成，请查看详情 | Hide |
| TaskId `{guid}` | BatchCompressionViewModel | Internal ID in failure status | Compression needs attention | 压缩未完成，请查看详情 | Hide |
| TaskId: `{guid}` | Task Center card | Internal ID competes with task state | Identify a task for support | Only in 查看详细信息 | Move to Details |
| 分组规则 | OrganizePhotosView | System-centric wording | How photos should be organized | 怎么整理？ | Replace |
| 执行模式 | OrganizePhotosView | Programmer/operator wording | What happens to originals | 整理完成后 | Move to Advanced |
| 同名策略 | OrganizePhotosView | Technical policy language | What to do with duplicate names | 同名文件处理 | Move to Advanced |
| 生成并预览操作清单 | OrganizePhotosView | Manifest-oriented | Preview destination folders | 预览整理结果 | Replace |
| 执行当前清单 | OrganizePhotosView | Machine action wording | Start organizing | 开始整理 | Replace |
| 来源 / 有效 / 元数据缺失 | Organize summary | Internal counters | Photo/folder impact | 将整理…预计占用…缺少日期… | Replace |
| 缺元数据 | Organize photo grid | Database-oriented | Capture date unavailable | 拍摄日期 | Replace |
| 输出参数 | RawToJpegModal | Technical configuration tone | Conversion choices | 转换设置 | Replace |
| 保留 EXIF | RawToJpegModal | Acronym without user meaning | Keep capture information | 保留拍摄信息 | Replace |
| decoder / LibRaw capability | RAW status | Backend detail | Whether RAW can be converted | Friendly availability text; decoder only in details | Hide |
| P3Query identifiers in accessibility IDs | Smart Folder | Internal naming can leak to assistive diagnostics | Human condition editor | Keep internal AutomationId only; accessible names stay natural | Keep internal only |
| 规则树 / 子组 | Smart Folder editor | Implementation structure | Match all/any conditions | 满足所有条件 / 添加条件组 | Replace |
| ★ 0 | Asset cards | Persistent zero-value noise | No rating yet | Show nothing until rating > 0 | Hide |
| Image / True / False / raw bytes | Inspector | Raw model values | Format, availability and size | JPEG/PNG/TIFF/RAW, status language, KB/MB/GB | Replace/Hide |
| 技术信息 on task card | Task Center | Troubleshooting competes with progress | Support diagnostics | 查看详细信息 surface | Move to Details |
| IOException / AccessDenied / Source missing | Error surfaces | No action guidance | Files unavailable | What happened + impact + next action | Replace |
| No Assets / No Result / Collection Empty | Empty states | English/system wording | Nothing here yet | Natural Chinese next step | Replace |

## Entry consolidation

- Temporary photo finding: **筛选**.
- Reusable saved criteria: **智能文件夹**.
- Technical query model remains internal and is never named in product UI.
- Gallery remains the central surface; Smart Folder editor, Inspiration and batch editors are mutually exclusive auxiliary surfaces.
- Each primary surface has one accent action; preview, advanced settings and navigation remain secondary.

## Baseline result

- Static visible-XAML internal-term leaks: 2.
- Confirmed dynamic primary-text leaks: 3.
- Primary workflows using manifest/query/task-ID language: Organize, RAW failure, Task Center and Smart Folder assistive naming.
- Target after this pass: 0 leaks in normal product UI, with technical values available only from **查看详细信息**.
