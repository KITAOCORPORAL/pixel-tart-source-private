# 重现此次只读源码审查

本轮不含RAW/参考程序/像素测试命令。源码清单、官方URL、完整commit、Git blob SHA256和精确函数行见SOURCE_LOCK.json。产品输入固定e2437d2300b37872d5b66482a721e3b6463f1ce4，通过git show读取，不合入产品分支。

官方源码只放独立本地资料根，选择全新不存在的目标目录，不覆盖旧资料：

```powershell
git clone --depth 1 --branch 0.21.1 https://github.com/LibRaw/LibRaw.git <new-local-root>/libraw-0.21.1
git clone --depth 1 --branch 5.11 https://github.com/RawTherapee/RawTherapee.git <new-local-root>/rawtherapee-5.11
git init <new-local-root>/sdcb-binding
git -C <new-local-root>/sdcb-binding remote add origin https://github.com/sdcb/Sdcb.LibRaw.git
git -C <new-local-root>/sdcb-binding fetch --depth 1 origin 43e9c1771c2b1387c2170e084e433d8bcef51cab
git -C <new-local-root>/sdcb-binding checkout --detach FETCH_HEAD
```

检查peeled commit与SOURCE_LOCK一致；不根据tag名字自动信任移动后的引用。源码文件hash以git show HEAD:path输出的blob bytes为准，不以Windows换行转换后的worktree bytes代替。

实际执行的验证只有：读明确DLL/包元数据并SHA256；静态PE import解析；官方Git源/函数/许可阅读；继承产品Git blob的身份绑定；新文档/引用/范围检查。没有cmake/configure/make/dotnet build/test，也没有加载native API。网络网页工具未返回可用正文，参考内容来自官方Git固定commit，不使用第三方博客或搜索摘要。

当前本地参考根：E:/Kitao-Research/raw-reopen-001-contract-20261104。完整第三方源码/二进制、原RAW、TIFF、私有缓冲不提交本仓库。不要执行上述源码仓库中的构建/示例来补数值结果；必要执行只由产品线统一安排。
