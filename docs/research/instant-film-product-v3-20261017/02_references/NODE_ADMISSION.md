# 逐节点准入：实际子阶段运行，但未批准产品迁入

业务日期2026-10-17 Asia/Shanghai。编译器Zig0.13.0来自固定PyPI wheel，SHA b64b2aa936ebbfeecfbb2611df331a5474edebe1246b98c2f56d6ae4d58b71fc，完整按段下载后总hash校验；工具仅本机ignored。首次单连接下载超时保留，不假装下载成功；没有安装全局编译器。

固定darktable fb0eb997e62c6dd52966cd35911c74afa150e209、RawTherapee db575c6690d30ce6428c0e1383bec8b8f1fb8d21原文件hash核对；stb 2c980bb59875b0d32144a71867fbdebb2f77cd20实际读取LICENSE和stb_perlin.h。生成研究harness提取原函数，不是重写公式冒充成熟实现；外部GPL源/生成文件/native exe只在artifacts，不入Git/产品。

|节点|实际执行|域/配置与行为|准入|
|---|---|---|---|
|曝光/影调|darktable exposure.c::process manual black0/scale2；RT curves.h::basecurve a2,b0,D1,hr/sr.5|灰阶/饱和单通道/负/>1；DT线性曝光，RT带肩部，创作候选有EOTF/toe/shoulder；不要求整图相同|OPEN：未配平完整pipeline/授权照片、完整时间内存和逐阶段容差|
|有限空间|darktable gaussian.c::dt_gaussian_blur sigma1 clamp[-1,2]；RT gauss.cc::gauss3x3|flat/impulse/edge/negative/high；DT递归无限尾、RT有限3×3、候选binomial5×5原图radius2，尾部/边界不等价如实记录|OPEN：有限支持差异已解释，尚缺真实照片及候选逐节点比较矩阵|
|噪声/材质|darktable grain.c simplex octave子阶段；stb_perlin.h Sean Barrett Perlin fBm3|固定坐标、重复一致、均值/方差/相邻相关；不是整乳剂物理模型|OPEN：独立实现来源已纠正；完整频谱/材质/真实照片/容差未闭|

RawTherapee grain源自darktable，只算同一来源，未用它凑双参考。STB为MIT或public-domain选择，darktable/RT GPL3研究隔离，不复制到生产。完整生成命令/原文件hash/exe hash/配置/19条运行观察及失败见RUNTIME_SUBSTAGES.json。无完整filmic/RawTherapee整模块通过声明。

这轮可完成所有权/全坐标/条带和实际编码，但没有资格以这些子阶段成功批准把候选像素代码迁入正式依赖图。产品V3入口暂不添加，避免假选择器。旧V1/V2/PV4/PV5/PV6不改。
