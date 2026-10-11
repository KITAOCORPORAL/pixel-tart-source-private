# 本批需求追踪

| ID | 需求 | 实现/证据 | 状态 |
|---|---|---|---|
| R01 | 现场/AGENTS/fetch/保留工作树 | N可用/分支head1ca73a2干净；fetch无前进；原集成head a9f61ba及审计目录保留 | PASS |
| R02 | batch06全部证据/生产调用链 | md及JSON证据读取/核对；真实decoder→master→workspace→quick/Publishing/managed | PASS |
| R03 | Sony峰值重现/阶段定位 | batch06旧组件约2.01GiB；分阶段旧新相同文件/hash | PASS measured |
| R04 | 流式TIFF编码/验证/hash | rows、删除full像素副本、无整文件验证buffer、pixel digest；full尺寸16bit同hash | PASS |
| R05 | 缓存/并发/释放 | 512MiB/master拒超限、活动master、64MiB detail cache、native/encoder并发1、取消/临时/COM/stream释放 | PASS限本地；全应用/多工作区预算PARTIAL |
| R06 | 抗混叠预览/域/顺序 | full显示定义后display-linear sRGB面积积分；棋盘格/细线/oracle | PASS numeric；用户视觉NOT_RUN |
| R07 | 真100%与快速拖动 | 原尺寸SourcePixelSize、DPI物理100%、原像素块140msdebounce、代理占位标识 | PASS核心块；超budget整屏多块PARTIAL；桌面NOT_RUN |
| R08 | cache键/过期保护 | hash/decode/EV/editID/zoom/dpi/quality/Rect；revision/token/target/version/dispose | PASS命令/自动；桌面连续手势NOT_RUN |
| R09 | 快速3格式/ICC/receipt/回读 | TIFF16/PNG16/JPEG95-444 sRGB，SourceDefinition/actual validator持久；2真实相机 | PASS |
| R10 | Publishing3格式/actual params | inheritedPV4 definition；original TIFF16/PNG8/JPEGquality/DPI；unsupportedICC/PNG16拒绝 | PASS original；resize/watermarkpixel验证PARTIAL |
| R11 | 无损/有损分开/预览区别 | lossless row oracle、JPEG MAE5/RMSE15；area缩小非逐像素0 | PASS |
| R12 | cancel/receipt/process interruption/recovery | 三格式cancel原文件保护；5真实OS退出重启；原managed流程 | PASS |
| R13 | PV1/2/3/PV4兼容 | 黄金不变；DisplayV1/full数学不改；stream结果旧SHA一致；preview新quality标 | PASS |
| R14 | 真实灰卡/阶梯/未知机/已WB | 已授权本机集合查找未见可信标定，不拿火焰替代 | NOT_RUN（真实质量），合成/拒绝回归PASS |
| R15 | 更早信号/高光仅调查 | 两相机Unpack mosaic指针/黑白/CFA/bit读取、官方API清单 | PASS调查；HDR实现NOT_RUN/OUT_OF_BATCH |
| R16 | Core/WPF/Release/push身份 | 见TEST_AND_RELEASE_MATRIX/RELEASE_IDENTITY/提交回复 | 以实际结果为准 |
| R17 | 不开Browser/CU/Explorer/不泄漏素材 | 允许文件/命令操作；无照片/db/credential/binary提交 | PASS |

## 限定范围结论

本批不是全专业系统交付：流式TIFF/3格式原尺寸/原像素分块与自动质量数值可完成；超大屏全视口多块、HQ交互速度、Publishing缩放/水印像素容差和整应用内存预算仍PARTIAL；桌面/视觉/实拍标定明确NOT_RUN。范围内逐项说明，不借HDR/全ICC/GPU未实现模糊本批成败。
