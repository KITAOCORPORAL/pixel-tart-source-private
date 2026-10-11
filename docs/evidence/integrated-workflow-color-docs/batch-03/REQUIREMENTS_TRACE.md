# 第三批需求追踪

日期：2026-10-11（Europe/Berlin）。START_HEAD=f10126ff7676a37b0ea2d0f4f84761f04657c235。远端未前进，工作区开始干净。没有新增工作树或合并主分支。

| 需求 | 实现与范围 | CODE | TEST | RELEASE_RUNTIME | USER_ACCEPTANCE |
|---|---|---|---|---|---|
| 重新定位 | AssetRelocationService：SHA256 验证、保留 AssetId、审计路径；素材库/画布/策划/联机/Studio 接入 | PASS | PASS | NOT_RUN | NOT_RUN |
| 跨库映射 | CrossLibraryRelocation：明确目标库/目标已有素材，保留源身份与映射；不自动覆盖冲突 | PASS | PASS | NOT_RUN | NOT_RUN |
| 工作台入口 | 沿用工作台→项目素材库入口，未增加独立工作台定位面板 | PARTIAL | PARTIAL | NOT_RUN | NOT_RUN |
| 摄影师标注 CAS | 递增版本+三字段比较，独立连接竞争、ABA、过期提交拒绝 | PASS | PASS | NOT_RUN | NOT_RUN |
| 标注恢复 | 产品侧草稿保留期望版本；素材侧失败/重启重试测试 | PASS | PASS | NOT_RUN | NOT_RUN |
| 所有旧元数据写入统一 CAS | 星级备注旧接口已条件更新；旧批量颜色入口未全部改为用户读取版本提交 | PARTIAL | PARTIAL | NOT_RUN | NOT_RUN |
| 多 EditVersion | 默认版本迁移、创建/切换、会话保存、版本级编辑历史、参数快照 | PASS | PASS | NOT_RUN | NOT_RUN |
| 导出清单 | 成功编码后 hash+原子 sidecar；快速导出与发布准备接线 | PASS | PASS | NOT_RUN | NOT_RUN |
| 富文本 | 字符格式、标题外观、对齐、列表、图像、基础表格，RTF 持久化，撤销/重做 | PASS | PARTIAL | NOT_RUN | NOT_RUN |
| 分页/打印/PDF | FlowDocument 分页预览、PrintDialog、原 PDF 复用富文本内容 | PASS | PARTIAL | NOT_RUN | NOT_RUN |
| DOCX | 固定 3.5.1，支持子集往返+schema 校验，未知内容损失报告 | PARTIAL | PASS | NOT_RUN | NOT_RUN |
| 画布进正文 | 内嵌已保存 PNG；文档包保留原 CanvasId 和历史版本 | PASS | PARTIAL | NOT_RUN | NOT_RUN |
| 色彩边界 | RAW/JPG 域契约、白平衡重复处理拒绝、候选显示/输出边界 | PASS | PASS | NOT_RUN | NOT_RUN |
| 真实 RAW/灰阶/色卡 | 未启用本轮授权语料，不借旧结果 | NOT_RUN | NOT_RUN | NOT_RUN | NOT_RUN |
| 品牌 | 显示名/主窗口/启动文字；内部程序集、路径、AppId 不变 | PASS | PASS | NOT_RUN | NOT_RUN |
| 启动动画 | 黑底彩色三角、真实初始化提示、就绪淡出、减少动态及资源释放 | PASS | PARTIAL | NOT_RUN | NOT_RUN |
| DESIGN.md 对照 | 本机工作区和仓库未找到该文件 | BLOCKED | NOT_RUN | NOT_RUN | NOT_RUN |

用户没有批准视觉效果，USER_VISUAL_REVIEW=NOT_APPROVED。上述 PASS 仅限该列和描述范围，不等于整个软件完成。
