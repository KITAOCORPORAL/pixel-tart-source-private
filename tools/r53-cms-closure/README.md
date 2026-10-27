# 完整CMS闭环诊断包脚本

放发布根与app/和samples/相邻。默认CMD/BAT启动完整EXE并新建隔离根，不依赖PS执行策略、不打开浏览器/Explorer。PS1可复用带诊断标识的根。Prepare-CMS-Synthetic-Samples.ps1需PS7.5+，只复制合成ICC图和重绑session source path，日期/版本/ICC/recipe保留，不覆盖目录。

产品源码b8436706，证据入口docs/validation/home-r53-cms-closure-20261027/FINAL_REPORT.md。不是安装包，不能发行。
