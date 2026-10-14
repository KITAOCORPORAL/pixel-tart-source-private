# B3 PV7实验原型合同

独立Pv7Recipe ProcessingVersion7/Algorithm1，domain/matrix/profile/WB/exposure各进缓存key，未知身份拒绝。可调用实验库，默认没有产品/项目解码选择，无旧项目迁移，无PV4/5/6修改。

接收明确LinearSrgbD65或EncodedSrgbD65（后者decode一次），已WB禁止非1 RAW增益；未WB明确通道gain后线性EV。线性超范围保留到输出肩/中性轴gamut mapping；输出一次sRGB OETF。预览float与Export16同一冻结result，量化round-even显式。不是高光真实纹理恢复，也不是相机ICC。

SyntheticProfileBoundary仅许可独立数学构造D50线性ProPhoto fixture，使用已有Bradford→D65线性sRGB；未知profile/厂商声称拒绝。没有实现二进制ICC全解析，不称全ICC。合成矩阵参考来自仓库LinearRgbColorScience声明，不把LibRaw ProPhoto D65解释为标准ProPhoto D50。

数学验灰阶单调/中性、曝光等价、single-transfer、负/超范围/原色/肤色邻域有限输出、WB重复拒绝、profile拒绝及D50/D65。仍只是实验quality hypothesis；未验证实际A7M4准确度。无ICC设备测量，无UI默认入口。

失败修复：初次CacheKey自身序列化触发递归StackOverflow导致host中止，记录保留；CacheKey JsonIgnore，仅recipe实际版本字段参与hash，复跑5项（含72golden）通过。新算法未写旧recipe。
