# 真实Chrome五项验收准备

2026-11-10。接续fb37c703，产品ed5d15d4、安装修复a3c4a35c。启动包为11.10实际目录，注册/数据根保持。当前状态PREPARED_WAITING_USER_REAL_CHROME_UI_FEEDBACK，最终五项回执未签发。

发现更新本机srcset记录 31acf2cd-3d8f-4fe2-9b3a-f79c6e4f61ad，AssetId 486ffaf3-379b-4ad4-9c51-ce7bfbb9e7f2，1600×1200，托管字节与srcset-original-1600.jpg SHA256完全相同，与320缩略图不同。实际Chrome HTTP页currentSrc为srcset-thumb-320.jpg；DOM naturalWidth80是srcset密度修正值，物理缩略图320×240，不能拿这个DOM值当入库图像尺寸。当前入库数据已核验，不需重复发送。

已准备并读回：测试库2条素材、项目A/B、有效且尚未使用的临时“待失效测试项目”、灵感板A；基线项目关联2条/板引用2条。三测试页200，最高原图1600 JPEG200，故意不可取得原图403。注册指向实际包，Native探针Ready，但探针仅是安装就绪证据。

界面反馈代理不能直接读取chrome-extension页，需用户依据USER_ACTIONS.md操作/报告，其他启停、临时归档及SQLite/托管文件/哈希证据由代理承担。现有页面和队列不重载、不清理。用户先做1、2、3A、3B；4/5按代理控制的状态分阶段执行。

原始本轮证据 E:\Kitao-Deliveries\chrome-capture-v1-20261110\chrome-five-case-round-20261110T075002Z；源与扩展未改，仍是冻结ed5d15d4，未重建包。新增协调工具只作用于明确隔离company01根，归档临时目标前确认名字/ID/未使用；正常关闭桌面时验证PID/exe，重开保持同数据根。源码若因缺陷改变再按用户要求更新全模块诊断包和普通JPEG闭环。

五项全部通过前，批量侧栏不启动。

步骤4另备未入库的960×640合成原图，页面http://127.0.0.1:51293/pending.html，hash及服务身份见PENDING_ONLY_FIXTURE.json；只在桌面被代理正常关闭后采集，重开同项后预期素材总数从2到3且新hash只1条。此为独立测试资料，不修改桌面/扩展源码。
