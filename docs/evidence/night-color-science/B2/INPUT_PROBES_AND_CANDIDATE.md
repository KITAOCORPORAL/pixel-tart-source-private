# B2 输入域探针与候选边界

LinearInputProbe在明确sensor RGB样本、每通道black/white/gain/matrix下测黑位前后、WB后、矩阵后极值/负数/>1和每通道sensor white计数；不裁切。仅合成/明确传入数据，不伪装LibRaw内部raw_image观察。PV4目前DcrawProcess→uint16已裁切，零/65535计数是解码后proxy，不可还原饱和纹理。后段没有真实高光恢复宣称。

现有官方候选0.22.2固定execSHA8DF090A80C784FA03182B91AB1679FD08E5FC139D54B0BFB8B003024EF741129、DLLSHA6A459C22039ABF0EAC4D263673337C8ED5F223ACBD372FCF77610DEBF80AC8CD已从源码和历史证据核对；使用hidden process、180s默认、cancel/timeout杀自身子树、source前后hash、RGB16/几何读取，-w -6 -T -o1 -g2.4 12.92。

候选域LibRaw0222-sRGBPrimariesGamma2p4Toe12p92是已WB已编码sRGB原色dcraw曲线，NOT PV4线性ProPhoto、NOT exact ICC sRGB；保留旧Sony FileUnsupported/-2不静默回退。只有显式后端API调用，没有默认产品选择，不捆绑candidate DLL。

本机未找到固定候选二进制，原授权CompanyRAW文件路径也未在家里按固定hash找到；N盘其他RAW不是自动获得原件授权身份，未使用。Canon/Fuji/Sony同文件旧新对比本轮NOT_RUN；历史候选Canon5496x3670、Sony3080x4624、Fuji7752x5178，旧Fuji5184高度差仅历史观测，不冒充本机结果。原许可证/source-offer义务未完成，分发BLOCKED，不下载未验证替代或纳入Release。

机器可读实际样本清单后续仅列合成输入和已有许可Cube++；真实相机测量缺口显式保留。官方在线验证工具本轮未返回可读网页，依据仓库固定候选provenance作为历史资料，未声称重新检索成功。
