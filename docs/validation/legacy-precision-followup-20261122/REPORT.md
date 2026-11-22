# legacy 高精度生命周期：11-22续接增量

2026-11-22（Europe/Berlin）。补丁 `11a424c90199b32cbf73cc6a14eefd651612b213` 依赖 `cf5a63d3`；证据基线fb00ef26。状态 **Sony重开和真正处理后100%首次导出通过；完整产品资源门禁仍未全部通过**。产品线当前HEAD `468b386aa24d506b0019043e256bd7f67094fd36` 在相关Core/RAW方向/预览链相对d07无差异；未改写该工作树及其未提交工具。

## 已复用与新增定位

已复读14ed39f1的REPORT/RESOURCE_FINDINGS/HANDOFF，沿用既有公平高光结论和四项只读页，未重跑180面板或候选，Knee/Amount/V2不动，锐化V1封存。原0806D680 payload和公司私有面板仍缺失；继续使用先前已冻结的40MP合成与授权Sony RGB48，输入SHA重新核对。真实相机RAW/发行DLL/账本/停止余额未动。

cf5解决了限定导出，但仍有两条必然分配：方向8的RGB48先转整帧float，再拷贝成规范方向float；RAW显示端只取Pixels却生成整帧ProcessingPixels。新补丁把既有/相同的方向置换与ushort/65535f合成一次分配，保持旧DecodeMaster/DecodeFrozenMaster签名和行为；只有新的upright入口由legacy VM使用，解码选项仍AutoRotate=false/ProfessionalDecode。暂未发布的自有图像在源文件验证失败/取消时释放，不处置外部解码数组。

显示专用RenderDisplay对既有准入点栈复用16行原函数，直接float→RGB24，严格沿用ToVisualRgb24的*255四舍五入，没有经TIFF RGB48二次量化。源master只借用，条带和显示结果各自拥有；临时小代理只在本作用域释放。预览/导出的节点数学、顺序、旧配方、ICC、发行解码器不变，空间/图/复杂混合仍走原高精度处理。

## 宿主与验证

原宿主重复RequestPreviewZoom可能使DetailPreviewWork指向较短任务；第一次新增处理预览检查因此过早失败，原FAILURE保留。修正为等待实际_detailRequest与渲染结束，要求应用Zoom=1、实际匹配图为完整尺寸、编辑开启、视口可见且已加载。非物理显示标定，也非普通EXE人工验收。

Core75/WPF20全通过。8种方向逐float及元数据完全相等；7类点操作/混合/部分条带与原输出一致，另覆盖中性共享、取消、验证失败与输入继续可用。分配实测记录见TEST_RESULTS，均为受控样本，不外推成40MP堆根证明。

40MP实际处理图RGB24的全量SHA与原Core oracle一致；首次TIFF比较120,559,104个16位通道样本、差异0。Sony两次显示SHA均与原Core oracle一致，两次TIFF各比较98,841,792个样本、差异0；规范尺寸4688×7028/方向1、RGB16及3144字节ICC身份一致。40MP中性完整周期同样逐样本一致。实际视口绑定与Zoom值、样本数和TIFF字段见PIXEL_DISPLAY_COMPARISON。

## 实际资源，MiB

2GiB=2048MiB；50ms Private和OS峰值工作集同时守卫；没有强制GC、全局cache清理或扩大预算。

|运行|Private峰|工作集峰|结论|
|---|---:|---:|---|
|cf5处理后40MP 100%|2201.15|1738.83|STOP / master-loaded|
|新补丁处理后40MP 100%→首次导出|1692.05|1747.04|通过|
|cf5 Sony完整周期控制|2041.25|2060.57|STOP / reopened|
|新补丁Sony保存释放→同进程重开→再导出|1549.02|1588.93|通过|
|新补丁40MP中性完整周期|1981.5|1987.44|通过|
|最终二进制40MP处理图→导出→同进程重开|2377.96|2080.57|STOP / payload-read-2|

cf5处理预览STOP的phase仍为上一个完成事件master-loaded，不把它误说成解码阶段失败。真正处理后40MP首次预览和导出已避开原两份全帧float；但释放后再读payload依旧越限，完整循环尚不能升级为通过。已知持有者在事件中按id去重，WeakReference.IsAlive与Private不下降都不是泄漏证明；该剩余峰值可能涉及尚未收集的垃圾/LOH提交和WPF/native资源，未取得强根证据，不猜定责。

## 产品线交接

正式合入仍由产品线处理。可审查增量11a424c9以及先前cf5；FOLLOWUP_ONLY.patch适用于cf5，COMBINED_D07_MEMORY.patch含两者，均在独立临时索引原基线apply --cached --check通过。未创建安装器、未覆盖发行DLL。未满足整产品资源门禁，不能因为Sony改善或首轮成功把40MP重复重开宣传为已解决。

下一入口为runs/verified-40-processed100-cycle/STOP.json和events.jsonl，在第二次读入前采集实际根/分配与WPF/native提交证据，区分活跃引用和未回收内存；不通过清全局cache、强制GC、改预算或新增弱缓存接线绕过。原全部STOP、cf5/fb00及本轮失败目录均保留。

最终剩余STOP已按stage的Core/App SHA绑定到11a424c9。此前尝试用Portable PDB检查候选/最终文件，因Release关闭PDB而失败；未据此宣称源码一致性，工具失败材料在本地pdb-readback-attempt保留。随后使用最终stage与同宿主完整周期直接复现，原候选STOP也保留。
