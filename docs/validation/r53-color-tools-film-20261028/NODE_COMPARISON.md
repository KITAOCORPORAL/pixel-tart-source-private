# RawWide / LinearWideToolsV1对照及准入
- 当前src基线068f0873（实际产品b8436706）；调查dbd19d66冻结047e51bf。已按REPORT/CAPTURE_ONE_MANUAL/code-evidence/递归更正逐项核对，不合并研究分支。
- RawWide默认off（KITAO_RAW_WIDE_OUTPUT_RESEARCH=1且诊断构建），保留现有LibRawRGB16已截断master。LibRaw0.21.1@cccb97647fcee56801fa68231fa8a38aa8b52ef7 src/tables/colorconst.cpp prophoto_rgb和convert_to_rgb_loop提供真实源输出矩阵；逆矩阵乘linearSRGB→XYZ D65推导ICC primaries，不误用常规D50primaries加D65标签。既有LCMS2.12@924a020d09bfe468c665467caf24aadeb41ff77c cmswtpnt/cmscnvrt，通过PCS Bradford到D50 linearROMM，再实际目标ICC转换；显示独立到sRGB，不走PV4Display导出。
- 源uint16截断/量化和上游饱和不恢复；X-T5内存准入和X-Trans拒绝保留。C1未运行、不拟合LUT。公司只核对已命名CompanyRAW，无家庭N根假定。

## 白平衡V1
- 输入linearROMM D50 float32，RGB→XYZ D50，LCMS Bradford相对白点→D50，XYZ→RGB。temperature/tint为相对xy偏移（每单位.00025/.0002），不是Kelvin、不是相机原生WB；零参数精确identity。负/>1不clip。
- 参考LCMS2.12 MIT cmsAdaptToIlluminant；RawTherapee5.12@a8a3d1bc7b419ed1c974c39276c97e618a4f9a1b rtengine/color.cc D50/D65 Bradford（GPL3研究不入产品）。60组native LCMS/独立numpy矩阵max2.23e-15，产品适配器再读取独立矩阵测试。

## 影调V1
- 新WideToolVersion=1，线性Y=.2880402R+.7118741G+.0000857B，18%灰pivot。contrast为有界log2(Y/.18)EV，shadow/highlight分别Y相关平滑权重的±2EV增益；各通道同增益保色比。非正Y仅应用原线性曝光，负/>1保留，不宣称恢复前端裁切。Normal强度×源坐标mask，旧WideToolVersion0曝光按原逐通道double顺序保留。
- 实读darktable5.2.1@be9171cf8bb2feab8bd115efd9b96a4ce7103039 src/iop/basicadj.c get_contrast幂函数；RawTherapee5.12 curves.cc histogram自适应NURBS toe/shoulder。域/参数不相同，27组差异保留NUMERIC_REFERENCES.json，不硬称等价。独立实现，不复制GPL，不加第二引擎。
- **影调完整双参考引擎执行尚缺，准入OPEN_DIAGNOSTIC_ONLY**，不以数学/保存重开当科学色准批准。白平衡/影调本轮作为可用诊断小集合；通用HSL/曲线/色轮仍拒绝且UI禁用。

## 版本/输出/性能
- ColorStateV1/PV4/5/6默认不变，V2新工具以node WideToolVersion1入同定义，撤销/变体/保存/预览/全导出/filmCurrentEdits复用。
- 先测20MP V2约7.49s后，建立有误差预算的linear area proxy800；mean<=1.5/P95<=4/max<=32（8位sRGB），冷proxy/热快速/精细分开。导出始终full。最终实数见PERFORMANCE.json。
- LCMS已有二进制/架构复用，无新增发布依赖；参考GPL源码/工具隔离，不随包。CalibrationEligible=false。真实profile/bitdepth检验只是合同一致性，不等相机或相纸准确。
