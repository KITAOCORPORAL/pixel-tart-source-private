# Third-Party Notices

像素蛋挞使用下列第三方开源组件。本文仅用于随软件分发时提供许可与来源说明，不替代各组件的正式许可文本。

## Sdcb.LibRaw 0.21.1.7

- 用途：.NET 的 LibRaw 调用封装。
- NuGet 许可表达式：MIT。
- 版权所有：Zhou Jie / Sdcb.LibRaw contributors。
- 来源：https://github.com/sdcb/Sdcb.LibRaw
- 本版本对应仓库提交：`43e9c1771c2b1387c2170e084e433d8bcef51cab`。
- 随包许可副本：`Licenses/Sdcb.LibRaw-0.21.1.7-MIT.txt`。

## Sdcb.LibRaw.runtime.win64 0.21.1 / LibRaw

- 用途：Windows x64 RAW 解码运行库，包括 `raw_r.dll` 及其 JPEG、Little CMS 与 zlib 运行时依赖。
- NuGet 许可表达式：`LGPL-2.1-only OR CDDL-1.0`。
- 来源：https://github.com/sdcb/Sdcb.LibRaw 和 https://www.libraw.org/
- 上游 LibRaw 0.21.1 源码：https://github.com/LibRaw/LibRaw/tree/0.21.1
- 随包 LGPL-2.1 副本：`Licenses/LibRaw-0.21.1-LICENSE.LGPL.txt`。
- 随包 CDDL-1.0 副本：`Licenses/LibRaw-0.21.1-LICENSE.CDDL.txt`。
- 随包版权与额外许可说明：`Licenses/LibRaw-0.21.1-COPYRIGHT.txt`。

### 原生运行包内依赖

- `jpeg8.dll`：libjpeg-turbo 2.1.3；IJG、Modified BSD 与 zlib 兼容许可，见 `Licenses/libjpeg-turbo-2.1.3-LICENSE.md.txt` 和 `Licenses/libjpeg-turbo-2.1.3-README.ijg.txt`。
- `lcms2.dll`：Little CMS 2.12；MIT，见 `Licenses/Little-CMS-2.12-COPYING.txt`。
- `zlib1.dll`：zlib 1.2.11；zlib License，见 `Licenses/zlib-1.2.11-README-LICENSE.txt` 的 Copyright notice 段落。

上述许可文件直接取自对应的 Sdcb.LibRaw 固定提交、LibRaw 0.21.1、libjpeg-turbo 2.1.3、Little CMS 2.12 与 zlib 1.2.11 上游标签，随应用输出目录及 Publish 目录一同分发。NuGet 原生运行包本身不包含独立 LICENSE 文件，只声明聚合 SPDX 许可表达式；因此本项目显式附带上游许可与版权文本。

像素蛋挞没有将这些开源 RAW 解码库描述为相机厂商 SDK。相关 DLL 保持独立动态链接文件随安装包分发。若需获得本候选版本所使用组件的精确包内容、校验值或对应源码位置，请通过“帮助 → 建议与问题反馈”联系产品维护者。
