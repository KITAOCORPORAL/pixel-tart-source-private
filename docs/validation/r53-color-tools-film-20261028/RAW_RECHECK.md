# RAW/C1冻结调查对当前家庭基线的核对
基线研究dbd19d66（源047e51bf）只按需读取，未合并研究分支；当前继承068f0873/产品b8436706。
1. **仍存在**：ProfessionalRawDecoder OutputBps16，LibRaw0.21.1 convert_to_rgb_loop对int结果CLIP到uint16，MakeDcrawMemoryImage ushort归一float；RawWide不恢复此处及更早scale_colors丢失信息。
2. **仍存在于旧PV4**：ProfessionalRawPipeline.Display肩部/色域收敛/sRGB OETF后导出；家庭CMS修复针对已渲染RGB，不使旧RAW自动宽域。本轮新RawWideOutputV1显式绕过Display用于文档输出，显示只是LCMS working→sRGB。
3. **此前V2仅渲染RGB**：ColorState准入拒绝非RenderedInput；本轮显式RawWide扩展将现有LinearProPhotoD65 master转LCMS linear ROMM D50，同V2渲染和CMS输出，未换前端解码。
4. **仍存在**：X-T5在rawWidth*rawHeight*28>1GiB准入先失败，当前三色Bayer guard还拒绝X-Trans；不删内存门禁/不误称支持。公司只核对已确认命名CompanyRAW资产，不假定有家庭N:/pixart/RAW。

递归更正盘点978文件/356候选/345open/11fail/314机型归一，不能把旧212写总库；26压缩包未展开。按原清单场景标签多未审查，不能断言无人像/高光场景；公司存在Canon TNAN8886.CR3及4个已知Sony/Fuji样本，不重新索要根路径。

C1 Session/TIFF/曲线实验仍NOT_RUN，未拟合LUT。优先研究变体由KITAO_RAW_WIDE_OUTPUT_RESEARCH=1显式启用，默认关闭，且仅完整诊断构建显示入口；旧PV4/PV5/PV6无扩展结果不变。
