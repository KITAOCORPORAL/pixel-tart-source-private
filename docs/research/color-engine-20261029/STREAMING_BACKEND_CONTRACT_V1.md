# 第八批研究 ABI 契约：SpektraStreamingResearchV1

此版本**已实现的外部隔离原型**，不是原上游/产品ABI注册。base engine c1447c6ab618fa4b1cb11fd2fe12826855007059，新source/DLL hash见company-round8/provenance.json。产品UI/项目/任务桥/默认链/PV4/5/6不改。

## 唯一允许的路径

- route=A，标准ProPhoto RGB D50 linear float32，方向1，允许完整数值域（必须finite，可负/>1）。准备清单及SHA锁住输入文件/像素/维度/source/bridge，backend自hash、实际完整resource集合hash亦核对；不调用spk_open上传完整source。
- 唯一stock/纸Portra400/PortraEndura，完整99字段resolved严格等于原已验证PortraA。隐藏默认scanner unsharp仍sigma.7full-tier pixel/amount.7，lens blur0。grain/glare/halation/DIR/autoExposure/sceneLatitude/geometry/diffusion/overscan等均原配置，未知字段/effect拒绝，不静默忽略。debug spatial-deactivation没有启用。
- 固定配置的frame-global需求检查为inactive或常量：autoExposure/boost_ev/halationHeadroom均关，print exposure normalization沿原host常数。没有“用局部统计近似全局量”。若将来启用需要global reduce，必须新版本先完整有界reduce再冻结；V1不支持。

## 原型入口（外部DLL导出）

```c
int spk_stream_research_v1(
 const char* resources, const char* request_json,
 uint32_t width, uint32_t height, uint32_t requested_rows,
 uint64_t host_cap, uint64_t device_cap, uint64_t staging_cap,
 int (*read_rows)(void*,uint32_t y,uint32_t count,float* borrowed_dst),
 int (*consume)(void*,uint32_t y,uint32_t count,const uint16_t* borrowed_rgba16),
 int (*poll_cancel)(void*), void* user, char** owned_report_json);
```

request字段严格10项：version/route/domain/orientation/sourceSha256/inputPixelSha256/backendSha256/resourceManifestSha256/resolved/negativeStore。backend/resource SHA256由Windows BCrypt真实文件hash核验，不只记录未比较。compiledPortra合同/资源集合在generator固化。不是通用参数API。

回调在单个native worker同步触发，borrowed缓冲仅在本次回调有效；consumer必须复制/写走后返回，不可保存指针。回调返回0成功、非0失败；Python捕获全部BaseException并返回非零，不将异常穿过C边界。read可多次但按film pass有序覆盖完整输入；consumer按frame row有序覆盖一次完整输出，可自行再分更小I/O块。

取消经poll与原节点边界/strip检查生效，不抢占shader。调用方设置线程安全flag、等native worker join/return后才能卸载DLL/释放用户对象。全局atomic busy守卫一次一调用，并发新请求−30；每次native call自建engine/pipeline，用完销毁，允许同进程顺序重复（实测2次exact/life0）。report_json为调用者owned，用同DLL spk_string_free。

返回0仅native路径完成；−7取消、−21read失败、−22consumer失败，未知合同/资源/预算等非零。native报告success与caller文件/host-budget验收分别判断；**不得只看native0就认整条路径安全**。

## 实现和所有权

1. 第一遍read_rows只分配strip host及GPU。调用原film_segment（原node_upsample、exposure、log、film curve等，不改数学），带完整frame source_long_edge/frame_long_edge/pixel pitch。结果直接GPU读回并写本地negative row store。无完整source/negative GPU驻留。
2. 第二遍从negative store读strip+原print_scan_finish_demand求出的halo=2，原print_segment保留原Gaussian/FIR、reflect、near-identity output matrix和sRGB编码。内部band边缘被halo隔离，首尾只在真实frame边缘反射；不用独立窗口改变frame尺度。
3. 原spk_to_rgba16只对bounded extended rows运行，再只consume中心有效count行。没有内部完整GPU/CPU result，不经materialise malloc全结果。negative磁盘文件可随height增长，明确单列，不冒称一切存储O(strip)。文件是临时中间数据，失败/取消/成功均移除；完整raw/TIFF行消费者由caller负责。
4. GPU allocator在真实VkMemoryRequirements.size请求前硬拒绝device/staging上限，计入所有存活pooled/persistent/staging及替换期间旧+新；同worker TLS计数，destroy后live0。device上限≤2GiB研究cap且当前free减256MiB；rows由device、**两份staging及替换余量**、host缓冲约束真正缩减。
5. native host统计是显式strip float/uint16 vector+工作余量预算，不包含Python/map/driver全部RSS；caller另有512MiB进程监测和输入map+callback独立准入。首个20MP之后发现post-native映射/segments读回RSS超cap，详见full-budget-caveat，不能隐瞒为host全闭合。
6. 输出写raw/TIFF先.partial，行完整/哈希/尺寸/TIFF回读/host guard都通过才rename最终文件。失败保留partial与失败report，无最终TIFF。首轮full用旧caller曾最终rename后才发现post-nativehost超限：已保留原始report并审计标记success=false，原像素重命名.initial-host-overbudget证据，不能被当全门禁通过产物。

## 已测能力与限制

10组自然small、4组合成stress与2顺序重复bit-exact；rows预算控制、height215/430同width/rows峰值完全相同、并发拒绝/取消/失败/live0实测。20MP只有1次native：完整尺寸/像素hash/TIFF一致，native41.8MiBdevice/24.3MiBstaging；没有20MP whole oracle。初次总进程RSS629MiB超过512MiB，完整修正I/O独立进程36.7MiB通过，但修正版native+I/O全20MP合并进程未重跑，安全端到端host门禁仍PARTIAL。

不扩三stock/B/seed/随机空间效果、未改input bridge/profile；D80014/家庭GPU/肤色高光缺口保留。代码/资源商业分发许可仍单列，独立进程/DLL不自动解决。
