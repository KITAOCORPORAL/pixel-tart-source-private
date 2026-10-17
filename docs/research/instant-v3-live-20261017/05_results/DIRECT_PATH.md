# 真产品原尺寸直出与预览闭环

正式Core模块/现有ProfessionalRawExport实际入口，Sony_DSC08015、Canon、冻结Fuji ×5基础效果共15组、45次直接TIFF16/PNG16/JPEG写出与数值回读，不先生成研究TIFF再供PNG/JPEG。每组+.2EV、固定seed和乳剂非默认，真实ColorStudioSessionStore保存/重开recipe逐JSON一致、64×64区域；源/冻结SHA核对。黑白/Blue独立身份不变。

PNG16/TIFF16逐码最大0；JPEG既有MAE5/RMSE15门槛，最大MAE1.44254；ICC sRGB精确、orientation1。结果REAL_DIRECT_PRODUCT_RESULTS.json记录逐格式receipt/全配方/输出hash/占用/耗时。原件、PNG/TIFF/JPEG全留ignored artifacts，不入Git。

另用相同正式Core产品preview，从已实际直出TIFF逐行解码/线性光area独立累加对照：15组全部通过，最大2.115396983526363e-6，小于预定2/65535的文件量化容差；不是缩小输入后重算效果。REAL_PREVIEW_ORACLE.json。该比较用于验证浮点预览与已量化文件，不假设逐位相同。

后台真实工作台合成专项覆盖五类选择/非默认/撤销重做/项目重开/切图中取消；真实原尺寸数据走同模块/保存/导出API。前台EXE点选及物理DPI另报告，不能以本报告替代。进程峰值2071248896bytes；源/输出/scratch实际ledger最大485013504，非真实OS释放证明。不同代输出cache禁止旧任务覆盖新target，运行时revision和源pin由产品测试覆盖。
