# Job Control 作业管理

入口：Setting → Setting → 左侧 Job Control（中文：作业管理）。

- 创建空白 Job：填写 Job1～Job255 中未使用的编号，点击 Create empty Job。空白作业没有相机和 Limit 参数，可继续 Add Camera。
- 克隆：填写目标编号，在 Clone from 中选来源，再点击 Clone Job。复制全部相机绑定、位置、通道分配、Limit 参数和已配置位置的 Camera.vpp / Inspection.vpp；SDK 相机仅复制 Inspection.vpp。VPP 按原始字节复制，不打开、运行工具或连接相机。
- 新 Job 的输出映射沿用来源 Job；空白 Job 沿用 Job1（没有 Job1 映射时使用第一份映射）。原有 Job、PLC 输入映射、S7/Profinet 配置及 GoSDK 目录不修改。克隆保持原 VPP 的输入输出端口。
- Add Camera：使用原 Visionpro / SDK 品牌选择器。自动建立 Pos1，生成缺失的空白 VPP；默认主、副通道都不启用。到 General Camera 分配通道、到对应 Camera / Algorithm 页完成工具配置。
- Delete Camera / Delete Job：移除配置和对应的输出映射，保留磁盘 VPP。以后重新使用相同相机编号，不覆盖旧 VPP。克隆到已有保留目录会阻止执行，需要更换目标 Job 编号。
- 不能删除正在被运行通道选用的 Job，至少保留一个 Job。结构操作要求 Offline 且检测已结束。
- 每次操作立即保存，不另设 Save。克隆在后台复制文件，期间阻止离开当前页。Limit 与 General Camera 的增删 Job/Camera 入口已移除，分别保留参数和位置/通道编辑功能。
- 保存后设置中心使旧配置页失效，下次切换立即重新读取；打开的输出设置更新 Job 列表并保留当前 Job 的未保存表格。主界面新增显示控件、隐藏已移除的相机槽位，不重编号，保留已有控件和统计。无需重启查看配置变化。

配置路径为程序目录 `Project/Config/Jobs/Parameter.xml` 和 `Project/Config/Communication/VPPOutput.xml`，VPP 路径为 `Project/VPP/JobN/CamN/PosN/`。

验证：`Tests/JobControlTests.cs` 使用隔离 XML、假空白文件写入器与设计模式窗体，覆盖普通/SDK 克隆、逐字节 VPP 检查、保留文件、缺失文件失败回滚、输出映射、异步克隆按钮、页面失效刷新、中英切换、主界面控件保留。没有启动生产程序、相机采集或 PLC 连接。空白 VPP 未配置前不能作为可用生产检测程序。
