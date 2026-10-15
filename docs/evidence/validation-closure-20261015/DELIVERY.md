# Kitao validation closure — PARTIAL / OPEN GATES

用户当地日期2026-10-15（Europe/Berlin）。主机原始采样+08时间戳原样保留。未向main合并、不强推；基线af78bb56b1eeec4e7c1a0b2de6f320dbbe33d458，从远端integration/kitao-company-validation-20261015核对后建立新独立research/kitao-validation-closure-20261015工作树。其他用户脏文件/分支/原失败/旧Release保留。

## 身份
产品源码冻结仍3d526ae7e7589a0d735689883dcf62a4ef272720，src/tests相对af78bb零diff；本批新增只为诊断工具和证据，不升级产品版本。阶段提交：

1962c12ddfc3ed319b5cb78e6e6a6c4ef00385b4 research(perf): attribute Structure halo allocations and retain strict regression gates
7bc1e80f09cd8e22b93ef50438d5f0638a969522 research(raw): distinguish shared LibRaw from independent dcraw execution
5a56483c776e706a0467284ba5ee3318a2dedc73 docs(native): verify sealed payload and stop on active shared desktop
e358b8496cdfb81eae101e94b92d5be267b9f6ec research(raw): observe authorized originals and resolve Fuji active-area version rule
f80de9627e0c22161c05f7c65fc4f31f0fd5e26e research(icc): trace fixed LUT PCS scaling and effective intent boundaries


最终证据SHA为包含本文件的提交（用git rev-parse HEAD获取；不在文件中自引用SHA）。完整文件清单/类型/sha256见FILES_AND_TYPES.json，排除自身和验证结果的自引用规则明确；封存Release290文件清单另见03_RELEASE_RECHECK.json。

Release：2.3.0-company-ab+3d526ae7e7589a0d735689883dcf62a4ef272720，Release win-x64 self-contained非单文件，路径D:/AI AGENT/kitao-company-validation-20261015/artifacts/company-validation/sealed-release/win-x64。EXE SHA256003ac23a2f1e440e4ac71e94e9f0270300819d8215ecdcea8f96471d68de1663。此次290文件重验全匹配，不重打包、不换DLL、不把候选RAW塞进Release。测试host是原封存published/core，与Release产品DLL原来逐SHA绑定，当前重查见03。

## 真实关闭
1. 固定七ICC/输入/三参考组件hash全部匹配；新增16组阶段轨迹与恒等M样本。约半值定位为固定skcms缺M/mft2路径的PCSXYZ还原差异；v4 p/s约0.00349属于LCMS强制BPC的配置不等价。规范身份、源码入口、绝对不支持格/域外点分开。不是引擎整体修复或支持批准。
2. 三RAW授权hash前后相同；LibRaw0.22.2容器/unpack/process实际成功。Fuji尺寸表7752×5184被新版本raw_width7872/crop0识别分支覆为7752×5178，rotate开关实验无影响。RT内部dcraw保留5184/不同origin，不后裁图。
3. 查出RT默认共享LibRaw；追加EnableLibRaw=false，Canon/Fuji独立实现运行成功、Sony真实拒绝。黑白位/WB系数/矩阵来源按可观测日志登记，取不到的像素阶段UNAVAILABLE，不从RGB16反推sensor。
4. Structure halo重复1.77524倍、临时数组估算2.247GB接近实测2.250GB；full/tilefloat哈希相同但tile更慢/峰值更高，取消实测约104ms。确定不在门禁未满足时改像素代码。
5. 封存Release290文件匹配；发布Core完整真实重跑1702发现/1702结果：1696 Passed/0Failed/6NotExecuted。P3worst61.894ms<原400ms，PV45672样本golden通过。

## 开放项（不得称完成全部验收）
- Windows启动、工作台→策划/Color、插图、原目录搬迁重开、撤销重做、DOCX、照片切换/取消/保存重开：共享桌面前台Weixin且idle875ms，未输入/启动，逐项NOT_RUN。物理DPI/WPS往返独立NOT_RUN。后台不替代。
- RAW扣黑后与矩阵前像素、WB后矩阵前像素、Sony color4详细语义/第二独立解码支持、LibRawmatrix运行时provenance仍OPEN/UNAVAILABLE。RT系数日志并非像素dump。不同pipeline的final误差仅诊断，不叫同配置精度通过。
- ICC上游确认、一般非线性B/profile准入、完整absolute适配等价仍OPEN。PV7与synthetic reader研究默认关闭不变。
- Structure无生产优化patch；两成熟节点参考门禁缺失。十秒自然等待WS/private未回基线，nativeallocator所有权UNAVAILABLE；ledger0不是系统释放。
- strict测试FAILED因为6NotExecuted。本轮WPF未执行；历史SEALED_WPF1603Passed/23NotExecuted、历史关键ReleaseWPF36Passed单列继承，旧FINAL_WPF中间1Failed记录保留，不混成最新绿灯。
- 不要求购买色卡；CalibrationEligible=false，无A7M4绝对色准/宝丽来精确复刻声明。

## 复现与保全
工具命令及路径在tools/validation-closure/README.md；每阶段文件以01–04前缀区分。原始TRX/log及失败编译/计时/格式问题都保留，完整结果见04_TEST_CLASSIFICATION、04_TESTS。所有新研究TIFF/依赖只在本地artifacts；本Git仅原创wrapper、生成PP3与结构化证据。无外部源码/候选binary/用户照片入库。最终用普通push新研究分支，再ls-remote核对，不改integration/main。
