# 家庭接续工具

这些工具只支持本轮已授权的家庭诊断批次，不扫描私人图库，不打开浏览器，不覆盖原工作树和预约。

- `Build.ps1` 从最后一次 src/tests 提交冻结源码，拒绝产品未提交修改和覆盖既有构建目录；四组件绑定同一版本。原 d07 和中间 d392 构建保留。
- `Verify-Http.py --artifact-root <构建回执目录>` 启动自己拥有的完整包选片服务，在新的隔离服务根验证真实 HTTP。包含重试、鉴权、确认、独立服务重启和撤销。每次创建新目录，保留失败回执。它不证明普通桌面或手机操作通过。
- `Prepare-Phone.py` 只接续既有家庭原预约，复用已经存在的家庭 HTTPS 路由，发放幂等的新设备授权并做只读核验。除 ScheduleDevices 之外的全部27张业务表摘要前后必须相同。私有链接、密钥、设备回执、DB 备份都留在 artifacts 下，绝不入 Git/包。它不会运行 seed/roundtrip/pc-adjust，也不会打开网页。原公司 revision4 不在家庭现场，不能借家中 revision2 冒充。
- `Seal.py` 核对冻结源码、八份组件版本、定向测试及 HTTP 回执，封装原扩展和经筛选的安全报告。对已有 ZIP 拒绝覆盖，核验每个 ZIP 成员摘要并排除已知凭据/运行数据。封装后收据不嵌入自身 ZIP。

报告入口：docs/validation/home-acceptance-20261122/REPORT.md。真实普通 EXE JPEG 和 iPhone 双向修改仍留在人工断点。
