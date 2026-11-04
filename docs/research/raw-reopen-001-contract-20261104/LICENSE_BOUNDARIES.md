# 许可证与分发边界（源码对照，不是兼容性批准）

日期：2026-11-04。固定身份及文件SHA见SOURCE_LOCK.json；native/managed包声明见PRODUCTION_DEPENDENCIES.json。

## 范围

只在本地隔离目录阅读官方源码并提交自编文字、函数位置、hash及元数据。不复制第三方函数实现、完整header、GPL代码、原生DLL、SDK资源或RAW进产品仓库/包。本轮没有第三方构建或再分发，也不声称现有产品已满足全部许可义务。

|组件/文件|已读声明|本轮使用方式与后续边界|
|---|---|---|
|LibRaw0.21.1 COPYRIGHT、LICENSE.LGPL、LICENSE.CDDL及研究函数文件头|二选一LGPL2.1或CDDL1.0；COPYRIGHT另列dcraw、DCB/FBDD、X3F、DNG SDK等来源与不同许可|固定源码仅研究。若未来引入修改版或再分发库，须固定实际源码/补丁/构建对应关系、选择许可并评估通知、许可文本、对应源码/修改、链接或替换等适用义务；本轮不作发布批准|
|Sdcb.LibRaw0.21.1.7 LICENSE.txt / NuGet nuspec|MIT；native runtime包声明与托管MIT不同|MIT仅覆盖相应绑定代码，不覆盖LibRaw及其依赖。未来复用应保留版权/许可；不能用绑定MIT消解native义务|
|RawTherapee5.11 LICENSE + rtengine/xtrans_demosaic.cc文件头|该研究demosaic文件为GPL3或后续版本；应用含不同文件/组件来源|仅源码/行为对照，不复制、翻译、改名或链接进产品。外部进程/DLL隔离也不能自动视为许可解决。若未来产品采用需另做整体兼容与分发评估|
|RT内置LibRaw子树|头版本0.21.0-Beta1、子树自身双许可/版权|不能把整个RT子树都写成同一种许可；也不能把它当生产0.21.1的精确二进制源。依赖共同性与许可分别记录|
|实际native依赖lcms2/zlib/jpeg/MSVC/OpenMP|本轮确认PE依赖名称，部分随包字节hash|这不是逐依赖license/版本/构建审阅完成。运行库再分发和对应notice尚需产品打包线核对，不由本轮文档自动批准|

## 尚缺的来源/合规材料

- Sdcb native runtime0.21.1包没有源commit、vcpkg port/patch、编译器/flags清单；不能声称固定上游tag就是该DLL完整对应源码证明。
- 实际运行时DLL/依赖加载路径没有在产品旧收据中捕获；包内存在文件不自动保证加载身份。
- 若下一阶段需要原生源码instrumentation，只在独立本地研究范围，保存补丁、完整源码版本、构建flags、DLLhash和实际启用的可选组件；不能替换产品默认DLL冒充旧结果。任何外部源码变更或分发另外核对适用许可。

结论：**REFERENCE_ONLY / NO_PRODUCT_REUSE_OR_DISTRIBUTION_APPROVAL**。POLICY的两个成熟实现代码相关性已查明，但共同LibRaw输入依赖、不同demosaic域与未运行数值比较必须保留，不能包装成完全独立的两个数值oracle。
