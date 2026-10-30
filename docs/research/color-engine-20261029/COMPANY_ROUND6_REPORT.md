# 第六批：CompanyRAW授权自然照片隔离实用评估（2026-10-30）

状态 **PARTIAL_DEVELOPMENT_EVALUATION / PRODUCT_DISABLED**。本轮从`8a9f14a8b0281133cb5b829b5debece8589da303`接续，fetch无增量。不再追查已闭合曲线下降，不重跑BT64小图矩阵；原兼容补丁/规范曲线区别保留。产品UI、项目模型、任务桥、默认链、注册及MatrixDecodeResearchV2源码均不改。

## 1. 素材范围与实际可用性

只使用`D:/AI AGENT/PixelTart-TestAssets/CompanyRAW`这一个明确授权根，不扫描其它照片目录、不索要色卡。读取该目录5项的文件身份和当前LibRaw元信息；选择当前生产decoder可支持的少量素材。结果：

|授权项|本批实际状态|
|---|---|
|TNAN8886.CR3，Canon EOS R6|生产decoder成功，5496×3670=20,170,320像素，原向1；唯一实际用于Spektra的开发场景|
|DSC09916.ARW|当前LibRaw OpenFile实际拒绝，未换库/强行unsafe适配|
|DSCF0347.RAF，X-T5|实际生产调用Unpack后因原1GiB解码估算门禁拒绝；filters9亦不在其Bayer合同，未放宽|
|DSCF0370.RAF，X-T5|同类metadata，预算同样超门禁且filters9，未重复无意义解码；照片内容未检查/未应用|
|LMAN1714.RAF，GFX100S|实际生产调用Unpack后因原1GiB估算门禁拒绝，不降bit或尺寸|

以上失败不是新引擎不支持其最终RGB的结论，而是本轮实际可用生产输入链的边界。没把旧D800研究工具改成其它相机探针。D80014仍NOT_RUN_SOURCE_ABSENT，本轮无扫描/重试；家庭RTX5060Ti补丁留待对应设备会话。

**照片内容已实际查看**：暮色/夕阳逆光，三名主体人物为深色剪影，远处另有人影，车辆、建筑、电线/杆。人物存在不等于肤色可评估：脸/皮肤没有足够照明，所以usable skin coverage=false。只有1个开发场景，不伪造保留验证集；没有强高光stress覆盖，也不是平衡日光人像/色卡样本。

## 2. 输入A/B严格分开

新增research caller仅调用未修改的`ProfessionalRawDecoder`与`ProfessionalRawPipeline.RenderOutputPixel`；不是MatrixDecodeResearchV2。实际生产nativeWB+demosaic+matrix仍uint16后转float，既有裁切不恢复。WB仅生产一次，EV0，无余量调参。

- **A**：真实生产`LibRaw Linear ProPhoto D65` RGB float32，按既定研究矩阵桥`LibRawProPhotoD65 inverse → linear sRGB D65 → XYZ D65 → Bradford D50 → engine标准ProPhoto D50`，`input_cctf_decoding=false`。不叠WB，不做Display。原色/白点/系数/源与输出hash见input-manifest。
- **B**：同一master实际生产Display EV0一次输出的sRGB编码float，`input_color_space=sRGB,input_cctf_decoding=true`；创意输入，不称原生RAW胶片模拟。没有先手抄显示公式，也不在engine前重复Display。
- 原始像素与生产Display用相同方向/坐标；全分辨率A/B文件真实准备保存，但**准备出全尺寸输入不等于完成全尺寸Spektra输出**。
- 独立Colour0.4.6固定commit沿第四批，Bradford组合矩阵误差1.07e-17，4个源坐标写出核对exact。A最小G−8.43e-7/B−2.45e-6，1,533,340个负通道均来自原同通道<1e-4暗值；既有舍入矩阵链微负保留，未夹成0，也不称新恢复信号。
- 生产master R/G/B最大0.27135/0.17673/0.08968，无满值1；零通道比例1.12%/5.10%/4.27%。不能从这张暗图宣称强高光裁切验证。

代理明确1200×801，Pillow F BOX逐通道平均：A在线性D50域、B在已编码创意域。缩图域和处理顺序已声明，不拿两路像素差当算法精度误差或合并收益。

## 3. 执行范围与资源预算

沿固定Portra400/PortraEndura、Ektar100/UltraEndura、TriX400/MultigradeIVRC配方；黑白显式m0/y68/printExposure1/TH-KG3。grain/glare关闭，halation/DIR/autoExposure同样关闭，不扩seed。用第四批已有隔离兼容补丁DLL，不改曲线/资源。

### 已运行

- **6个代理**（3胶片×A/B，1200×801）：全部重复exact、新进程按完整resolved/input/backend/resources身份重新open/render exact、TIFF16逐像素回读exact。
- **24个native采样窗口**（4个同坐标640×640窗口×3胶片×A/B）：均实际渲染/保存/回读。Portra人物边缘A/B两窗口另做重复及新进程重开。
- 因而30个独立图像配置成功，含8个新进程重开共38个成功调用；8次同session重复检查；所有成功输出TIFF16回读通过。另有收尾driver身份门禁加入后的1次PortraA proxy新进程核验，输出相同，不计新配置。主清单38次成功CLI执行包含30配置+8重开，每配置中8次额外repeat故主GPU render共46次；另2次被动trace与1次收尾检查共49次GPU render。不是38个独立场景。
- 四窗口：人物头部边缘、暗部车辆、天空渐变、真实电线边缘。早先id`sky_wire`实际看图只有天空，已明确改为天空覆盖，并补登记x3500/y1350的`wire_edge`，不假装原窗口已有电线。
- **native窗口不是全画面真实导出的crop**：输入只含该窗口，缺外部空间上下文/画幅尺度，不能代替完整输出验收。

### 六个完整输出均拒绝，未偷降尺寸

公司GTX1650 4GiB当前桌面其它工作占用下，GPU free约1.07–1.08GiB。本研究保守准入`76bytes/pixel +96MiB`（源/negative/工作RGB/RGBA16/readback余量的启发估计），另保留256MiB GPU空闲余量并限研究2GiB。

5496×3670估算 **1,633,607,616bytes≈1.52GiB**；当时实际准入限 **883,949,568–892,338,176bytes≈0.82–0.83GiB**。6全尺寸case均在engine创建/上传前`REFUSED_RESOURCE_BUDGET`，没有强制占GPU、没有静默缩图、没有全尺寸render耗时。

这是**当前负载+研究预算下的预先拒绝**，不是运行到OOM，也不证明GPU/引擎永远不支持全尺寸。`strip_budget_bytes`本冻结源码不自动决定rows；本轮没新做全尺寸条带/其它并发模型，不把旧小图stripe exact推广为这张全幅已支持。

## 4. 实际观察与数值分账

本地接触表和同坐标窗口已实际查看，不只是生成后未读。所有图像仍只在`E:/Kitao-Research/color-engine-round6-20261030/observations`，Git不含私人照片/PNG/TIFF/float像素。

- Portra在该暮色场景偏暖，Ektar红紫倾向更明显，Tri-X为灰阶；暗前景细节在film观察里更少可分辨，但这不是最终0裁切（所测代理/窗口各RGB 0与65535比例均0）。没有独立真值或用户偏好，因此不称色偏bug/物理失配或优于生产。
- **A/B差异先查域/顺序**：代理Portra/Ektar/TriX最大code差11235/8848/3619集中同一小红色高饱和区域。该位置A隐含linear sRGB约[.08255,−.00769,.00044]，B经生产中性轴gamut/display+编码域平均后decode约[.01717,.00059,.00323]；resolved参数差仅input space与decode开关。两路本就不同：不能把该差直接归因新算法或第二次WB，也未提出修复。
- **默认scanner unsharp**仍{0.7full-tier pixel,0.7amount}，不是grain/glare。针对Portra人物native窗口，使用现有被动trace库（最终RGBA输出与主库hash相同）读取`print_linear`与`print_scan_finish`：A最大线性RGB差0.00863、均值0.000481；B最大0.00841、均值0.000475。该点集没有新<0/>1，实际观察到边缘与噪声对比变化。图里delta×50仅为诊断，不是照片或候选修复结果；未编造公开的unsharp关闭参数。
- 肤色、强高光、独立自然验证集、物理拟合、肉眼缺陷阈值及全画面边缘上下文均未覆盖。截图观察和数值一致性不能相互替代。

## 5. 公司时间/内存（不外推）

生产Canon decode约1542ms，完整方向+生产Display行准备及写盘约13047ms；该进程OS峰值约609MB。首次caller写盘后hash发生FileShare未关闭错误，source未变、输出保留为失败；在新目录修正为close-before-hash后通过，未覆盖旧失败证据。

6代理首call范围：

|测量|本批实际范围|
|---|---|
|engine初始化|138–163ms|
|open及upload|275–294ms|
|render含GPU计算/CPU读回|87–92ms|
|复制结果到owned NumPy|2.65–5.12ms|
|raw RGBA16落盘|3.57–3.97ms|
|TIFF16写出|2.69–3.34ms|
|TIFF16回读|3.74–4.57ms|
|每fresh-process RSS采样峰值|约147–158MiB|
|engine render后allocated ledger|约59–70MiB，不是VRAM驻留峰值|

native窗口render约45–60ms（640×640），与代理不混作同分辨率基准。公开ABI不提供内部upload/compute/readback独立时间，不能虚拆；结果copy/文件写出另计。未测GPU独占物理VRAM峰值，不沿用家庭性能，不泛化到高像素/其它场景。

## 6. 产品接入评估与许可

**技术结论：可继续隔离开发样本观察，但当前不能批准产品后端接入。** 需要明确：

1. 输入域/白点/显示与场景两路线声明及hash，不重复WB/tone；图像来源与解码/预算失败应向产品明确报错，不自动切旧管线。
2. 真实full-resolution峰值/资源准入、条带/保边上下文、取消与释放、代理/最终尺寸分离；不能仅凭代理快就放行全幅任务。
3. 相同冻结resolved+隐藏默认、backend/resource/input身份；grain/glare关时才有本轮确定性证据。任意grain_seed仍UNSUPPORTED，固定初始rng_state不是用户seed。
4. alpha丢弃/opaque、RGBA16最终clamp非HDR，stride以像素计；复制后result_free，独立result可活过session/engine；cancel节点边界、render完成后release；session保守串行。
5. 需要更多已授权且照明/场景明确的自然开发/验证覆盖（真正肤色、强高光、普通日光/室内等）与偏好/失败评估；本轮不索要色卡、不伪造验证组。

R55_CAPABILITIES更新为本批V2，产品只“负片/印相模拟·研究中”，默认关闭、不注册、UI/项目模型/任务桥/默认链不改、不整分支合入。

**许可单列**：LightTable非商业条款≠Spektra GPL组合许可≠逐profile/数据源权利；任何一项不能替代另一项。外部引擎/资源/大图仍本地，代码与逐资源分发均未批准。独立进程、DLL、LUT非自动许可解决。

## 7. 未运行与保留

全分辨率Spektra输出（预算拒绝）、全部自然图context一致性、真正肤色/强高光/验证集、D80014与真实取消、家庭补丁、seed扩展、物理色准、产品UI/桥接和全PV回归均未运行/未批准。曲线已闭合原因不再追查，BT64组不重跑，WB余量冻结。

初始/收尾树状态、实跑数值、失败及manifest见company-round6。最终commit和远端读回SHA在交付回复提供，不在自身文件中伪造循环hash。
