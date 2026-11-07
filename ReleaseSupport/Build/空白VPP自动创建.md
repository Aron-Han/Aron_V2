# 空白 VPP 自动创建

已为当前现场配置创建 `Aron_V2/bin/x64/Debug/Project/VPP/Job1/Cam4/Pos1/Inspection.vpp`。文件是 VisionPro 9.6 SR2 的空白 CogToolBlock，没有工具、输入或输出，正式使用前需配置算法。

## 创建规则

- 新增 SDK 相机：为位置补建缺失的 `Inspection.vpp`，不创建 `Camera.vpp`。
- 新增 Visionpro 相机：为位置补建缺失的 `Camera.vpp` 和 `Inspection.vpp`。Camera 工具不绑定取像设备。
- 新相机尚未保存位置时，先按原界面占位位置 Pos1 创建；不改变原位置、主副通道或启用配置。保存其它位置时补建该位置文件。
- GeneralConfig 的 Add Camera/Save，以及 Limit Setting 新建 Job、相机或位置的入口均已接入。
- 删除相机、位置或 Job 仍只删除配置，不删除 VPP；同名重新创建、切换 SDK/Visionpro 均保留已有文件。
- 已有文件一律不覆盖，包括空文件或损坏文件。只补缺失文件；使用唯一临时文件和不覆盖的移动操作，防止失败或并发时覆盖原文件。
- 创建失败会提示错误，不报告配置保存成功。可能已成功创建的其它空白文件保留，重试会跳过它们。

## 安全验证

`Tests/BlankVppTests.cs` 覆盖新增、同名重建、切换模式、空文件保留、失败清理、并发、路径校验、实际序列化及两个界面入口。16 份原有现场 VPP 的 SHA-256 在创建前后相同。

测试不执行工具 Run/Trigger/Live，不连接 SDK 相机或启动 PLC。文件创建不修改运行时缓存、取像流程、主副通道解析或通讯逻辑；空白文件不代表可用于正式生产的算法。
