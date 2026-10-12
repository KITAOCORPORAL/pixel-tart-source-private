# 测试矩阵与复现

## 执行方式

- Core：dotnet test tests/RAWSelectionAssistant.Tests/RAWSelectionAssistant.Tests.csproj -c Release
- WPF：dotnet test tests/RAWSelectionAssistant.WpfTests/RAWSelectionAssistant.WpfTests.csproj -c Release
- 专项：FullyQualifiedName包含BatchEight/BatchSeven/ProfessionalRawWorkspace；细分记录见TEST_SUMMARY.json。
- 真实输入：沿用本地获授权batch06/corpus-final-input，两相机副本；环境变量KITAO_BATCH08/07/06_CORPUS、EVIDENCE显式启用。未写原文件，输出位于临时目录和artifacts，照片/输出不提交。
- Release：win-x64 self-contained、非single-file、无debug，InformationalVersion绑定源码c3c1ed7。nuget漏洞信息源不可达产生NU1900警告，构建成功不能宣称漏洞审计成功。
- 发布复核：把发布KitaoPhotoSelector.dll、RAWSelectionAssistant.Core.dll复制到隔离测试目录，hash逐一与发布包比对；运行真实同一测试DLL。没有启动应用EXE。

## 覆盖

|测试|验证|
|---|---|
|BatchEightInstantFilmTests|8×8 profile/EXIF分块与序列化、关闭PV4身份、域/重复WB拒绝、随机几何/子流、相纸方向、3/4/5秒边界、全局资源|
|BatchEightInstantFilmProductTests|三个编码器读回、Publishing相同definition、完整JPEG/同seed保存重开、Source实际JPEG magic、Publishing TIFF中间尺寸、取消保全、DPI参数/多块/过期结果、相机实际状态时间、生命周期|
|BatchEightPublishingOracleTests|TIFF16独立双线性像素oracle；EXIF6→24×40，DPI96/144/240×alpha0/.5/1×九位置，透明logo独立合成oracle；缺图失败|
|BatchSeven/ProfessionalRawWorkspace回归|旧PV4真实2相机、多格式、旧版本保存重开、停止/过期结果及unsupported拒绝|
|Core/WPF全部|旧版本黄金、旧胶片、版本/恢复及既有产品测试；skip单列，不冒充通过|

## 修复过程保留

1. 起点草稿TIFF导出未应用新层/纸框；统一输出尺寸/方向与逐行oracle，旧关闭TIFF字节保留。
2. 新测试首次Publishing临时输入使用.bin被真实格式门拒绝，测试改正确.tif，不放宽格式门。
3. Dispose重复调用使CTS已释放，改幂等退出。
4. 骰子Geometry反序列化为新集合，record引用相等导致合法会话被拒绝：改完整定义值比较，增加实际seed会话回归。
5. 现有本地化/Popup规则回归失败：新增三语资源并使用PixelTart.Popup.V2，无修改断言。
6. 1.35+3s浮点减法边界未返回Completed：按绝对完成时刻判断，3/4/5秒全部重测。
7. JPEG的Source原先会把TIFF写成.jpg、Publishing中间.png：生产路由修正，新增magic/扩展名/receipt尺寸验证。

原失败TRX/log保留在本地artifacts/batch08/tests和.validation/batch08-start，TEST_SUMMARY列出历史失败，不删除失败事实。最终重复执行通过才能记PASS。运行中产出的既有ui-guardian报告只备份到artifacts后恢复起点字节，不把生成的自动状态写回历史人工证据。

RELEASE_STARTUP、DESKTOP_DPI、USER_VISUAL均NOT_RUN。数值/状态与原始测试树布局检查不是桌面视觉批准。

## 最终实跑结果

|运行|通过|失败|跳过|耗时/说明|
|---|---:|---:|---:|---|
|Core完整 core-delivery|1643|0|6|50秒|
|WPF完整 wpf-delivery|1534|0|16|6分34秒，最终源码/测试|
|最终WPF交付专项|14|0|1|13秒；真实项另运行|
|源码真实RAW2机|3|0|0|4分37秒|
|发布Core专项|33|0|0|165ms|
|发布WPF专项|13|0|3|14秒，三个真实项另运行|
|发布真实RAW2机|3|0|0|4分36秒|
|Release x64 self-contained|PASS|||构建成功；NU1900漏洞源查询警告保留|

跳过为opt-in真实/视觉/GPU/性能门，具体名称列在TEST_SUMMARY；TRX的notExecuted属性不总反映Inconclusive计数，真实跳过数以total-executed和逐项outcome共同核验。数值状态结果不改变NOT_RUN桌面验收。
