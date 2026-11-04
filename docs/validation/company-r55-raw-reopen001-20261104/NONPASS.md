# 非通过与缺项

- RAW-REOPEN-001 仍 OPEN / P1：在两个独立单次默认解码进程中复现 native RGB16 不一致，不是修复通过。
- 首个已观测不同阶段：MakeDcrawMemoryImage返回值，早于托管复制/float/方向归一。具体 native 内部子阶段及根因尚未证明。
- 原App预览/保存重开/导出精确断言未放宽；原pass4 FAIL及输出保留。本轮无产品修复，因此未重跑修复后App验收。
- 真实RAW完整开发严格2次，均仅company-02；另有3个诊断启动失败，其中1个只完成OpenFile，未Unpack/Process。两次no-RAW序列化自检中第一次失败、第二次通过；不是相机解码次数。
- 初版重复DLL resolver、缺少诊断目录native依赖、反射XmpData Span、零输入IntPtr序列化和早期编译错误为观测器问题，保留原记录，不作为产品根因或隐去的相机失败。
- 继承OMP_NUM_THREADS=6；实际worker数未测，无强制单线程或线程策略调整。
- 未新增Canon/Sony解码；未改gamma、肤色/高光、profile、默认质量；未跑三图/20MP/研究六图、日历/DOCX/灯台/锐化。
- LibRaw与RawTherapee固定版本成熟实现数值对照 NOT_RUN；native算法/线程/参数/decoder变更门禁尚未满足。
