# SpektraLab许可与分发门禁（源码固定c1447c6）

本地研究已实际运行；**不等于允许合入或分发光镜商业产品**。此处是固定文件审阅与待办，不是法律意见。完整文件哈希在round3/spektra-inventory.json的licenseFiles/sourceFiles中，原文只留隔离checkout，不复制进光镜发布包。

|对象|固定材料/许可|使用、修改与分发结论|
|---|---|---|
|SpektraLab engine代码|根LICENSE.txt + license/LICENSE GPLv3；windows_UI/THIRD_PARTY明确GPL-3.0-or-later|内部隔离研究不发布完整后端；若分发GPL覆盖程序/组合，须保留告知、相应源码和构建/安装义务并审查组合边界。是否可与光镜现授权组合未获批准。DLL/独立进程并非自动豁免；需双重/商业许可或兼容的产品授权方案。|
|spektrafilm原28个profiles/print LUT/直接衍生|license/SPEKTRAFILM_LICENSE.txt，Andrea Volpato，CC BY-SA4.0；profiles内metadata归属一致|明确涵盖LUT及其他编码同一profile内容的格式；分发原/改/烘焙LUT均保留作者、canonical source、许可/变更声明，改编适用相同许可。前言另要求修改记录，区分其法律地板与礼貌要求；**烘焙LUT不自动脱离义务**。前言明确最终照片/电影归创作者，不把照片强制改成CC。|
|TriX400、Multigrade IV RC及product数据|engine/resources_product/NOTICE.md及profile metadata，Kodak F-4017/Harman2019曲线数字化/拟合|NOTICE说明数据来源与假设，并非独立明确的可商业再分发许可。根GPL是否覆盖这类数据、权利人对原图/曲线数字化的授权边界、作者再授权范围都需书面明确；本轮不把它自动视为CC BY-SA或已清权。仅研究，产品分发BLOCKED。|
|overlay neutral_print_filters|研究目录合并product filter pack，不改原git资源|哈希记录合并后的资源，跟随各源权利；发布需变更声明/源数据权利审核，不因是数字参数就假定无义务。|
|Vulkan volk|third_party/volk，d979819...，MIT|复制/发布保留版权和MIT文本；本次编入引擎。|
|stb|2c980bb...，双许可MIT/公有领域，项目选择MIT|像素后端当前未用host/JPEG路径；如以后引入应附MIT告知。|
|metal-cpp|Apache2.0，file LICENSE.txt|本次Windows未编译，不作为分发依赖；Mac引入再审NOTICE/版权/修改等义务。|
|Vulkan-Headers|b5c8f996...，Apache2.0 OR MIT；LICENSE.md|构建期headers，本机不发布SDK；如分发源码/文件需遵循选定许可并保留原告知。|
|Vulkan运行时/显卡驱动|系统vulkan-1.dll/NVIDIA驱动|调用现有系统安装，不把驱动打包；产品另记录系统要求。|
|LLVM-MinGW/Clang/libc++/mingw运行库|下载固定20250402 zip哈希，包LICENSE.TXT及各组件告知|仅研究工具；不能把整个工具链称单一许可。复制到engine旁的runtime DLL逐个哈希已登记，若分发须补所含组件对应告知及具体例外/源码义务；本轮未发布二进制。|
|CMake/Ninja/glslang|固定工具版本/二进制hash；各自BSD/Apache及第三方组合，工具原包保留本地|只构建研究后端，不混进产品编译环境；再分发工具时另行SBOM/文本审阅。|
|Qt/LibRaw0.22.2|仓库另有UI/native RAW依赖|本次构建OFF，没有下载/链接Qt或LibRaw0.22.2，不能沿用它们的授权或借此改变光镜LibRaw。|

## 两条产品路径

A 完整后端：先解决GPL组合分发和数据权利（分别向SpektraLab代码持有人及原profiles作者/数据权利人确认，不能假定一个人的商业许可覆盖所有来源），修复并复核失败门禁，显式新版本recipe后才能交产品线；本轮暂缓。

B 获授权的轻量颜色预设 + 独立空间效果：可用自有测量/明确可商业与再分发的数据，或在产品可履行CC BY-SA署名/分享相同资源的前提下分发合法衍生LUT；无法履行则换授权/自有数据。颗粒、光晕、模糊、扩散无法由单点LUT等价承载，空间模型须独立测试及版本化。独立实现不得翻译/粘贴GPL代码来“换名”。普通负片+相纸与宝丽来独立，保持既定UI，不新增布局。

## 明确缺口
没有获得权利人的商业书面答复、没有法务组合许可结论、没有可发布二进制包或SBOM认证。内部研究源码/资源hash已固定；这里只推送本会话工具与统计，没有推送第三方engine源码、profiles、RAW或大像素输出。上游README/NOTICE是证据，不当作授权委托。
