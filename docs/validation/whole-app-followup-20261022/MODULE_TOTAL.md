# 全模块本轮续接总表

2026-10-22 Europe/Berlin；完整范围诊断包，不能发行。相关测试数量在模块间有重叠，不可求和；页面、源码及fixture通过均不代表人工业务闭环。

| Module | BuildIncluded | AutoRelated | Passed | Failed | NotRun | ActualOperations | ManualDesktop | Status |
|---|---|---|---|---|---|---|---|---|
| 工作台 | PASS | 128 | 128 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 素材库 | PASS | 562 | 560 | 0 | 2 | NOT_RUN | BLOCKED | PARTIAL |
| 归片工作区 | PASS | 98 | 95 | 0 | 3 | NOT_RUN | BLOCKED | PARTIAL |
| 新建分片 | PASS | 129 | 129 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 日历 | PASS | 198 | 198 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 策划中心 | PASS | 120 | 120 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 联机拍摄 | PASS | 215 | 213 | 0 | 2 | NOT_RUN | BLOCKED | PARTIAL |
| Color Studio/参考仿色 | PASS | 266 | 254 | 0 | 12 | NOT_RUN | BLOCKED | PARTIAL |
| 暗室R3 | PASS | 28 | 28 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 自由画布 | PASS | 32 | 32 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 在线选片 | PASS | 55 | 55 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 摄影收支 | PASS | 63 | 63 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 项目历史 | PASS | 65 | 65 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 工具箱 | PASS | 15 | 15 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| RAW转档 | PASS | 19 | 19 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 压缩 | PASS | 11 | 11 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 发布导出 | PASS | 87 | 86 | 0 | 1 | NOT_RUN | BLOCKED | PARTIAL |
| 拼图 | PASS | 4 | 4 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 整理图片 | PASS | 1 | 1 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 水印 | PARTIAL | 35 | 35 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 设置 | PASS | 35 | 35 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 帮助 | PASS | 34 | 34 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 授权 | PASS | 0 | 0 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 安装升级卸载 | NOT_RUN | 10 | 10 | 0 | 0 | NOT_RUN | NOT_RUN | NOT_RUN |

## 当前测试
| suite | discovered | selected | obtained | passed | failed | skipped | status |
|---|---|---|---|---|---|---|---|
| FULL_CORE | 1723 | 1723 | 1723 | 1717 | 1 | 5 | FAIL |
| FULL_CORE_CORRECTED | 1723 | 1723 | 1723 | 1718 | 0 | 5 | PARTIAL |
| PUBLISHED_MODULES | 14 | 14 | 14 | 14 | 0 | 0 | PASS |
| FULL_DOCUMENTS | 1 | 1 | 1 | 1 | 0 | 0 | PASS |
| FULL_RESEARCH | 25 | 25 | 25 | 25 | 0 | 0 | PASS |
| FULL_DPI | 91 | 91 | 91 | 91 | 0 | 0 | PASS |
| PLANNING_SAMPLE_RECHECK | 1726 | 15 | 15 | 14 | 1 | 0 | FAIL |
| PLANNING_SAMPLE_NATIVE_RECHECK | 1726 | 15 | 15 | 15 | 0 | 0 | PASS |
| FULL_WPF | 1726 | 1726 | 1726 | 1706 | 0 | 20 | PARTIAL |

## 策划15区
| Section | BuildIncluded | RelatedTests | RelatedPassed | RelatedFailed | ManualDesktop | Status | Blocker |
|---|---|---|---|---|---|---|---|
| 项目概览 | PASS | 35 | 35 | 0 | BLOCKED | PARTIAL | 四卡片/右Inspector全视觉另验 |
| 文字 | PASS | 50 | 50 | 0 | BLOCKED | PARTIAL | 完整Word功能区/复杂分节未完成 |
| 镜头清单与分镜 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 复合正文摄影字段历史/分镜缩略图等需逐项核实 |
| 参考图 | PASS | 41 | 41 | 0 | BLOCKED | PARTIAL | Windows真实选图保存未验 |
| 情绪板 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 桌面视觉/自由排列未验 |
| 灯位图 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 拖拽/标尺/自动画布快照需核实 |
| 服化道与道具 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 快捷表格视觉未验 |
| 设备 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 真实工具箱设备目录依赖未连接 |
| 人员 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 通讯录联动未在当前实现范围 |
| 场地 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 地图/自动天象来源未连接，OfflineManual |
| 档期 | PASS | 66 | 66 | 0 | BLOCKED | PARTIAL | 真实日历双向返回未验 |
| 当天时间表 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 跨日拆段/拖排/自动天象未完成 |
| 预算 | PASS | 26 | 26 | 0 | BLOCKED | PARTIAL | 币种分开、实际支出只读；分类对照需验 |
| 文件 | PASS | 33 | 33 | 0 | BLOCKED | PARTIAL | 原位置附件打开实机未验 |
| 文档内容 | PASS | 50 | 50 | 0 | BLOCKED | PARTIAL | 任意混排SourceIds内联块/双向复合撤销未完成 |
