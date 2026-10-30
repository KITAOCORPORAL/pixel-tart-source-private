# 本轮后端研究契约（默认关闭）

状态：RESEARCH_ONLY / 不准入默认产品。用户界面仍遵循 Grok 原布局，本契约只描述“生成新变体 → 现有灯箱读取 → 保存重开”的数据边界，**未验证或修改灯箱实际集成**。旧 PV4/5/6 和旧 recipe 不迁移。

## MatrixDecodeResearchV1

- 允许输入：已授权清单里两个 D800 SHA 白名单、固定 LibRaw0.21.1/Sdcb0.21.1.7 二进制与已验证布局。
- 源域：LibRaw 黑位/WB/AHD 后的相机域 uint16，不能说 sensor float。
- 研究解码器在矩阵前 progress 回调中直接计算 float，不再依赖阶段落盘；后端仍执行原生整数矩阵用于流程收尾。实际存储：RGB float32，LibRaw 特定 ProPhoto D65 线性，标称1=65535编码，允许负值及>1。不贴 D50 ICC。
- 版本/身份：sourceSha256、DLL hash、recipe全字段、pixelHash、尺寸/方向必须同时保存；新变体不替换源文件或既有编辑版本。默认不启用，recipe独立于产品序列化模型。
- 预览/导出：显式研究适配器调用产品实际 RenderPreview、Display/ApplyExposure、FillRgb16Row 及 TiffExport。适配器的历史 OutputBits=16 只记录原始/demosaic整数精度，**不是float输出位深**，不得把此研究桥接默默注册成PV4默认master。输出预览 sRGB；TIFF16在显示映射之后量化，浮点master另存。
- 保存：recipe.json + master.f32 + hash；重开必须校验定义/尺寸/域/hash，在新进程重新生成预览与导出，不再解码源。研究状态与浮点文件均不包含商业资源。
- 错误：未知版本/DLL/source/layout、参数不生效、非有限、hash/长度不符明确拒绝，无旧路径回退。CancellationToken只覆盖现有产品后处理；本最小解码CLI未做取消验收，标NOT_RUN。

例（研究工作树根，输出目录必须不存在）：
```powershell
dotnet run --project tools/color-research-decoder/ResearchDecoder.csproj -- <明确RAW路径> <清单SHA256> <新本地目录> matrix
dotnet run --project tools/color-research-decoder/ResearchDecoder.csproj -- reopen <变体目录> <新重开目录>
```
`headroom` 是 **WbHeadroomResearchV1** 独立候选；highlight=1最大WB归一，在float乘 max/min 恢复尺度。与 matrix 禁止合并收益；本轮不批准产品应用。

## SpektraWindowsResearchV1

- 精确后端：SpektraLab commit c1447c6ab618fa4b1cb11fd2fe12826855007059；DLL、73个运行资源、shader和工具哈希见 round3/spektra-inventory.json、spektra-tools.json。许可证门禁未批准。
- 输入：C ABI packed float32 RGB 或 RGBA；输入4通道的alpha不保留，输出是强制opaque。原始图像/profile/sourceHash、输入变体hash与适配矩阵必须记录。
- 线性宽域：LibRaw特定D65工作域先逆其矩阵到线性sRGB，XYZ D65→Bradford D50，再转引擎blob内 **标准ProPhoto D50** 矩阵；`input_color_space="ProPhoto RGB", input_cctf_decoding=false`；0.18不是色卡校准，曝光尺度按源处理定义。
- 创意输入：产品已显示映射sRGB D65，`input_color_space="sRGB", input_cctf_decoding=true`，不能称为原生RAW胶片模拟。引擎 auto_exposure=false，EV=0，外部不重复WB；film/print自己的非线性是配方内容，不宣称与RAW相同。
- recipe：film_stock + print_stock + 全部 resolvedParams + backend/resource hash；deterministic 关闭 grain/glare，测试点色彩同时关闭halation和DIR。实际组合 Portra400/PortraEndura、Ektar100/UltraEndura、TriX400/MultigradeIVRC，黑白grade2由显式滤色/印相曝光字段组成。不得混成宝丽来。
- seed：现有C ABI没有grain_seed，发送会拒绝。同一session连续grain render会变化；新Pipeline固定初始状态可用于此次first-draw条带比较，但不是可自定义seed合同。要产品稳定seed须权利/兼容批准后显式版本扩展，当前标UNSUPPORTED。
- 输出：spk_result.rgba16 指向结果拥有的CPU RGBA16；按row_stride_px×4访问，复制后 `spk_result_free`。texture是opaque CPU ownership handle，不是VkImage。输出sRGB D65编码，本实验提供嵌入sRGB profile的8bit观察PNG、RGBA16原始文件；最终to_rgba16 clamp[0,1]后round，不能称HDR浮点输出。输入alpha丢弃/opaque必须对产品透明图显式拒绝或另存蒙版，不能隐瞒。
- 创建：spk_engine_create(resources,NULL)，spk_open复制输入。spk_render包含GPU计算和CPU读回，公开API不能分别计时内部上传/读回；本次open计时含上传，初始化/首次/热/CPU复制/文件写出单列。
- 释放：session_release/engine_destroy不使已返回result失效，须独立result_free；out_json用spk_string_free，engine-owned JSON不得free。
- 错误/取消：未知参数/stock返回错误，先读spk_last_error；spk_cancel(session,NULL或progress_id)另线程调用，在节点边界返回 -7。不能在取消完成前释放session。具体线程安全更广范围未验收。

最小例：`tools/spektralab-research/run_matrix.py <固定checkout> <build> <研究overlay资源> <验证过的stage目录> <float文件> <新输出目录>`。脚本实际演示9组合、stride、完整recipe、哈希、结果释放、重复、取消及生命周期。这里引用外部测试绑定而非复制GPL到产品；该研究调用样例本身的分发仍需许可审查。

不交付为可接入批准提交：GPU CTest 10/11通过，BT传递函数边界1失败；无跨Metal/Python全图oracle、无自然图保留集、无主观偏好。产品线只能在另轮门禁通过后显式接入新变体，不整分支合入。

## 2026-10-30 公司接续说明（不覆写第三批历史）

本V1为家庭第三批冻结记录，SpektraLab已构建并完成9组渲染；旧preflight中的“未构建”仅属更早时间点。公司结果和更严格的研究定义/生命周期读取规则以`BACKEND_CONTRACT_V2.md`为准。V1少数字段不是完整算法重建合同；旧archive只读、不自动升级。公司GTX1650常规CTest原/补丁11/11不抵消家庭RTX5060Ti原版BT失败；公司受控-Os最小复现与隔离补丁见COMPANY_ROUND4_REPORT。
