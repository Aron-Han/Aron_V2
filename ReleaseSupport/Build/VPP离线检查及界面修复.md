# VPP 离线检查与界面修复（2026-10-23）

## 实际文件

检查范围为当前 VS 输出 `Aron_V2/bin/x64/Debug/Project/VPP`：8 份 Camera.vpp、8 份 Inspection.vpp，共 1,164,596,328 字节。文件中发现的 Cognex 程序集版本均为 71.2.0.0（VisionPro 9.6 SR2）。16 份均已成功反序列化，测试前后逐文件 SHA-256 一致，没有保存、转换或覆盖原 VPP。

对照实际 Parameter.xml，Cam1/Cam2 的 Pos1–3、Cam3 的 Pos1/Pos3 均有对应文件；首次检查时 SDK 相机 Cam4/Pos1 无需 Camera.vpp，但缺少 Inspection.vpp。后续按用户要求已创建空白 Inspection.vpp（3791 字节），并验证可加载，未复制其它相机算法或禁用此相机。自动创建规则见《空白VPP自动创建.md》。选择缺失文件时编辑器显示带路径的内嵌错误提示，不退出窗体，仍可切换其它 Job/Camera/Position。

逐文件日志位于 `Tests/VppValidation/9b6f29801e844f6f9bceee4a4a1c4f80`（前六份）和 `Tests/VppValidation/bdc4dabc36a34fa3ad7cca25bb9232b0`（第七至第十六份）。首轮 Cam2/Pos1/Camera.vpp 的测试触发 60 秒保护，已只终止该测试子进程，没有终止主程序或修改文件；独立重试约 5.2 秒通过。该次超时原因未定，不能把首轮超时计为通过。

单文件大致耗时：Camera 约 4–5 秒，Inspection 约 15–17 秒。算法文件本身约 70–172 MB，包含较大的持久化数据；本次没有删除图像、修改 VPP 内容或改变生产加载/缓存策略。每份采用独立进程串行检查，并设置 60 秒/1400 MB 保护，退出即释放其内存。

## 窗口保护

- Camera：实际 VPP 可绑定取像编辑器；错误状态会清理旧编辑器标记，窗口保留并允许重新创建，避免重试仍引用已移出的控件。
- Algorithm：编辑器改为按需初始化，初始化、Subject 绑定和 Load/Save 入口增加外层异常保护；失败显示在当前窗口，不让异常直接逃出选择事件。原 VPP 路径、运行缓存键、通道解析和主副通道重载逻辑保留。
- Subject 绑定期间暂停 electric runs，完成后恢复原设置，不改变用户后续手动运行功能。离线测试整个绑定阶段保持暂停，没有调用 Run、Trigger、StartLive。
- 算法工具树绑定还发现本机 VPRO_ROOT 缺失。原生路径准备现同时设置**进程级** VPRO_ROOT，指向已通过版本校验的安装根目录，供图标及工具资源使用；系统/用户环境变量、注册表、VisionPro 安装和 GoSDK 文件不变。
- 缺失 VPP、损坏 VPP 的测试分别得到明确的 DirectoryNotFoundException / SerializationException，编辑器模拟错误和重试路径另行验证。

测试只调用依赖预检、反序列化及编辑器，不执行 Program.Main、FormMain.Shown、Bootstrap、取像触发、算法运行或 PLC 服务启动。Cognex 编辑器内部会枚举取像驱动（可能有 GenTL 日志），这不是发起采集。未验证实机取像、PLC 联调或任何 VPP 重新保存行为。

## Cam 数量

Cam count 标签及数值移到 Delete Camera 后，纳入已有 AlignedSelectorToolbar 的字体/垂直居中逻辑。Add row/Delete row 保留在下一行。布局测试验证与 Delete Camera 中心线误差不超过 1.5 像素，并保留原数量计算、增删相机及配置保存功能。

## Communicate:Null

实际 `Project/Config/Communication.xml` 已保存为 Profinet。Null 的原因是 `_displayProtocol` 原先只在 InitCommunication 中赋值，而相机离线会阻止启动通讯，导致该字段保持空值。

修复为主窗体初始化时只读取已保存协议用于显示，连接状态仍由原服务决定：未连接保持红色。没有提前调用 InitCommunication、跳过相机检查或修改握手/重连逻辑。隔离测试分别保存并重新构造主窗体验证 S7、Profinet、TCP/IP 均显示正确协议，同时通信服务仍为 null、相机检查仍未完成。测试不 Show 主窗体，以免触发其真实启动检查。

原 S7VisionCommService.cs、ProfinetVisionCommService.cs、CommunicationConfig.cs 的 SHA-256 与修改前一致。
