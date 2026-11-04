# RAW-REOPEN-001 隔离原生观测准备

当前仅静态准备，不是可运行RAW观测器或修复。native build/ABI/native self-test均NOT_RUN。PreparationHost只支持无RAW自检，刻意没有解码入口。旧V2.1的生产DLL hash/ABI限制不可删掉后直接换库冒充兼容。

复用研究线0e0723df0c8eaf665d2bccdfe150c45621534d3b的SOURCE_LOCK；LibRaw固定cccb97647fcee56801fa68231fa8a38aa8b52ef7。上游及生成源码、二进制在仓库外唯一共用根；没有搭另一套RT/LibRaw研究实现。

- Inspect-Inputs.mjs：验证18个锁定LibRaw文件和原NuGet DLL/binding字节，静态读取PE imports；不加载DLL。
- Prepare.mjs：接受干净固定源码、NEW输出根；唯一锚点缺失/重复拒绝，去除插入标记必须恢复原文件字节。FILES.json冻结全部生成输入。
- rr001_observer.h：原创Windows BCrypt SHA256逐行观测辅助，没有算法移植。CFA9/colors3、shrink0/fuji_width0、width=iwidth/height=iheight契约之外INVALID，不猜布局。
- Build-Isolated.ps1：需要已有x64 MSVC Developer shell与cl/nmake/dumpbin/dotnet；只构建上游DLL target，不构建产品/样例。原COPT加/openmp，绝不改OMP/KMP。可选RawSpeed/DNGSDK/LCMS/JPEG未开启，不等于生产组件组合。bcrypt是新增观测依赖。预检/构建失败保留、不自动重试。
- PreparationHost：用新库自报offsetof/sizeof与固定binding交叉验证，正常原binding resolver/initializer，不注册第二resolver；原生合成检查与init/recycle/close，不含Open/Unpack/Process/MemoryImage。实际模块路径/hash仍需核验。宿主编译通过不等于原生自检通过。

命令形式（占位符不是执行记录）：
```powershell
node <tool>/Prepare.mjs --self-test <shared-fixed-source> <NEW-selftest-root>
node <tool>/Prepare.mjs --prepare <shared-fixed-source> <NEW-isolated-source>
& <tool>/Build-Isolated.ps1 -PreparedRoot <NEW-isolated-source> -BindingRoot <fixed-binding-directory> -OutputRoot <NEW-build-root>
```

未完成门禁：实际C++编译/PE/依赖/ABI/无RAW自检；研究线新增阶段复核；新bounded两槽RAW host/runner（尚未实现），复用原默认配置语句，冻结源码/patch/binary/参数/输入SHA/启动及binding后环境。当前不能运行RAW，不要用旧runner删掉hash限制顶替。

全部满足后按当时用户授权最多两独立进程各一次，持久consumed slot、失败不补跑、不另跑原DLL。相同只能写新构建本次未复现。后续会话权限/次数依据当时指令，不自动把本轮额度延期。

不修改生产native/默认/算法初始化/线程/quality，不用calloc、单线程或缓存掩盖。不自动启动浏览器/产品/后台任务。当前只记录hash和状态，无私有像素写盘，不为选择分歧阶段追加显影。
