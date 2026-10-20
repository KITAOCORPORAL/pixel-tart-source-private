# 全模块诊断总表

PARTIAL：完整范围诊断包，不是已验证发行版。页面/源码/相关测试不等于业务动作或人工实机通过。

| Module | BuildIncluded | AutoRelated | Passed | Failed | NotRun | ActualOperations | ManualDesktop | Status |
|---|---|---|---|---|---|---|---|---|
| 工作台 | PASS | 126 | 126 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 素材库 | PASS | 559 | 557 | 0 | 2 | NOT_RUN | BLOCKED | PARTIAL |
| 归片工作区 | PASS | 98 | 95 | 0 | 3 | NOT_RUN | BLOCKED | PARTIAL |
| 新建分片 | PASS | 129 | 129 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 日历 | PASS | 198 | 198 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 策划中心 | PASS | 99 | 98 | 1 | 0 | NOT_RUN | BLOCKED | FAIL |
| 联机拍摄 | PASS | 215 | 213 | 0 | 2 | NOT_RUN | BLOCKED | PARTIAL |
| Color Studio/参考仿色 | PASS | 266 | 254 | 0 | 12 | NOT_RUN | BLOCKED | PARTIAL |
| 暗室R3 | PASS | 27 | 26 | 1 | 0 | NOT_RUN | BLOCKED | FAIL |
| 自由画布 | PASS | 32 | 32 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 在线选片 | PASS | 55 | 55 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 摄影收支 | PASS | 59 | 59 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 项目历史 | PASS | 65 | 65 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 工具箱 | PASS | 15 | 15 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| RAW转档 | PASS | 19 | 19 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 压缩 | PASS | 11 | 11 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 发布导出 | PASS | 77 | 76 | 0 | 1 | NOT_RUN | BLOCKED | PARTIAL |
| 拼图 | PASS | 4 | 4 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 整理图片 | PASS | 1 | 1 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 水印 | PARTIAL | 35 | 35 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 设置 | PASS | 35 | 35 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 帮助 | PASS | 34 | 34 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 授权 | PASS | 0 | 0 | 0 | 0 | NOT_RUN | BLOCKED | PARTIAL |
| 安装升级卸载 | NOT_RUN | 10 | 10 | 0 | 0 | NOT_RUN | NOT_RUN | NOT_RUN |

## 本轮测试原始统计
| Suite | discovered | selected | obtained | passed | failed | skipped | notRun | status | complete |
|---|---|---|---|---|---|---|---|---|---|
| FULL_CORE | 1713 | 1713 | 1713 | 1706 | 0 | 7 | 0 | PARTIAL | True |
| FULL_WPF | 1709 | 1709 | 1709 | 1682 | 7 | 20 | 0 | FAIL | True |
| FINAL_CORE | 1713 | 1713 | 1713 | 1708 | 0 | 5 | 0 | PARTIAL | True |
| FINAL_PUBLISHED_MODULES | 14 | 14 | 14 | 14 | 0 | 0 | 0 | PASS | True |
| FULL_DOCUMENTS | 1 | 1 | 1 | 1 | 0 | 0 | 0 | PASS | True |
| RESEARCH_CANDIDATE_TESTS | 25 | 25 | 25 | 25 | 0 | 0 | 0 | PASS | True |
| FULL_DPI_CONTRACTS | 91 | 91 | 91 | 74 | 17 | 0 | 0 | FAIL | True |
| FINAL_WPF | 1710 | 1710 | 1710 | 1688 | 2 | 20 | 0 | FAIL | True |

## 策划十五分区
| Section | BuildIncluded | Automated | RelatedTests | RelatedPassed | RelatedFailed | ManualDesktop | Status | Blocker |
|---|---|---|---|---|---|---|---|---|
| 项目概览 | PASS | PARTIAL | 18 | 18 | 0 | BLOCKED | PARTIAL | 四卡片/右Inspector全视觉另验 |
| 文字 | PASS | PARTIAL | 36 | 36 | 0 | BLOCKED | PARTIAL | 完整Word功能区/复杂分节未完成 |
| 镜头清单与分镜 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 复合正文摄影字段历史/分镜缩略图等需逐项核实 |
| 参考图 | PASS | PARTIAL | 24 | 24 | 0 | BLOCKED | PARTIAL | Windows真实选图保存未验 |
| 情绪板 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 桌面视觉/自由排列未验 |
| 灯位图 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 拖拽/标尺/自动画布快照需核实 |
| 服化道与道具 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 快捷表格视觉未验 |
| 设备 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 真实工具箱设备目录依赖未连接 |
| 人员 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 通讯录联动未在当前实现范围 |
| 场地 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 地图/自动天象来源未连接，OfflineManual |
| 档期 | PASS | PARTIAL | 51 | 51 | 0 | BLOCKED | PARTIAL | 真实日历双向返回未验 |
| 当天时间表 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 跨日拆段/拖排/自动天象未完成 |
| 预算 | PASS | PARTIAL | 11 | 11 | 0 | BLOCKED | PARTIAL | 币种分开、实际支出只读；分类对照需验 |
| 文件 | PASS | PARTIAL | 18 | 18 | 0 | BLOCKED | PARTIAL | 原位置附件打开实机未验 |
| 文档内容 | PASS | PARTIAL | 36 | 36 | 0 | BLOCKED | PARTIAL | 任意混排SourceIds内联块/双向复合撤销未完成 |
