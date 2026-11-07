# Manual 手动流程模拟

## 入口

Manual 菜单的旧 Trigger 0 / Trigger 1 合并显示为 Trigger。原菜单对象与 TestDualTrigger 权限门控保留。Receive Data 现在使用结构化输入弹窗。

- 通道号按当前通讯配置列出：Profinet 为 0～3，S7 使用 ActiveChannelCount，TCP/IP 使用已配置通道数。
- PLC interaction 默认 No，必须由操作员明确选 Yes 才参与现有 PLC 输出流程。
- 弹窗不会在打开时执行任何取像、检测、数据接收或 PLC 写入。

## Trigger

选择通道，确认后从该通道当前 Job、Pos 及主相机映射获取快照，按原先的副相机组 → 主相机组顺序取像和检测，复用通道锁和共用处理入口。

- Yes：要求所选通讯服务已启动、Online、相机和授权校验正常。使用现有 NotifyInspectionStart → 检测/结果写入 → NotifyInspectionEnd 流程。
- No：可在软件 Offline 时执行，但仍要求相机/授权就绪；不调用本次流程的 PLC 开始、结果或结束输出，不更新待发送 PLC 结果缓冲；不执行本次流程的图像/数据库持久化分支。原检测、统计和图像显示逻辑保留。
- No 的取像失败仅记录本地模拟失败，不启动会影响实际通讯的全局相机恢复流程。

No 使用 AsyncLocal 范围标志，随 async/await 和 Task.Run 传播，不修改全局通讯开关，不抑制其它通道的自动检测。异常后恢复标志并释放锁。

## Receive Data

输入通道、Clear Result（0/1）、位置号、程序号（JobID）、产品码（PartN）。应用时形成 SetUserData=true 的同一 CommunicationChannelInput。

日志记录完整逻辑快照，并对产品码中的引号、换行和分隔符转义。该日志不是伪造的网络字节帧：S7 字段来自不同地址，Profinet 字节布局由现有映射决定。

复用正常处理顺序：清除结果优先，置位时不会继续切换 Job/Pos/PartN；未置位时按原校验与 VPP 缓存加载规则处理 Job/Pos 和产品码。

- Yes：执行现有清结果或 Job/Pos 回显等 PLC 输出。
- No：更新本地选择状态及产品码，不发送回显，不清改真实 PLC 输出/待发送缓冲；ClearResult 只模拟其优先级并在日志明确说明 PLC 输出保持不变。

保存后的手动状态会供下一次触发使用，不是临时沙盒。建议现场调试前先确认所选 Job、Pos 和产品码。

## 边界

- No 仅抑制**本次手动操作**的程序内 PLC 输出，既有服务的自动轮询、心跳和其它正常通道保持原状。
- 不向 PLC 输入寄存器写入假的 Trigger/SetUserData 信号，也不伪造驱动管理的输入边沿 ACK；底层 ACK 仍由实际通讯驱动和真实 PLC 输入控制。Yes 复用的是正常收到事件后的应用处理流程。
- TCP/IP 目前尚无实际 socket transport，因此仅允许本地模拟，Yes 会阻止并记录原因。
- VPP 内自行编写的外部 I/O 脚本不属于程序通讯层的隔离范围。
- 本次未修改 S7/Profinet 驱动、地址映射或 GoSDK。

## 验证

`Tests/ManualSimulationTests.cs` 在独立输出目录使用设计器上下文主窗体、假通讯服务、假检测委托和假 VPP 加载委托，不调用实际 Cam_Inspection、SDK、PLC 或 VPP 反序列化。覆盖本地模式零写入、正常输出顺序、跨通道并发、失败恢复、清结果优先级、Job/Pos/PartN、输入日志、通道 63、就绪校验及中英文弹窗。
