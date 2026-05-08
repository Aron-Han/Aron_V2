# 通讯帮助

## 范围、方向和协议支持
本模块说明当前程序的应用交互流程。输入表示 PLC → 视觉，输出表示视觉 → PLC；S7 的 DB 地址与 Profinet 字节偏移必须以现场保存的配置为准。
- S7：已有实际通讯服务，支持按已配置通道处理数据与触发。
- Profinet：已有基于 Cognex/CC24 的服务及输入输出映射，当前有效通道为 CH0～CH3。
- TCP/IP：已有选择和参数入口，但当前没有接入实际 socket 收发 transport。不能仅凭选择 TCP/IP 就认为可与 PLC 通讯；目前只能使用允许的本地模拟部分。
- S7/TCP 配置通道数支持的框架上限为 64；实际可用通道仍取决于协议、配置和项目相机映射。
通讯帮助只读，不提供“点击即写 PLC”的功能。

## Online、连接状态与 Camera Ready
这几个状态分别看，不能相互替代：
- Online/Offline：应用的运行状态及在线请求条件。
- Communicate:S7/Profinet/TCP/IP：所选通讯方式，不等于 PLC 逻辑交互已成功。
- 通讯服务连接：需要结合状态栏颜色、日志与驱动状态核实。
- Camera Ready：相机验证、系统状态与协议相关条件共同决定。
S7 的 Ready 要求相机就绪、系统在线请求有效、PLC Trigger Enable 有效。软件显示 Waiting 不代表 Ready 已经发送。
切到 Offline 会阻止正常自动触发；S7 在操作员 Offline 时可保持连接继续轮询，连接仍在不等于系统仍在线。不要把 Offline 当成所有协议、所有信号都停止读写。
相机丢失、授权异常和恢复过程还有各自的保护处理，先看具体日志。

## 配置入口与修改后生效
入口：Setting → Communicate。通过当前通讯选择界面保存协议和通道数；S7、Profinet 下分别有其设置及输入/输出入口。
1. 确认选中的协议与现场 PLC 连接方式一致。
2. S7 配置连接参数、有效通道数、输入输出区域及点表；Profinet 配置 CC24/设备及相应映射。
3. 保存后查看状态栏的协议名称和日志。保存配置本身不证明已连接，也不等于 PLC 已完成握手。
4. 若界面要求重新初始化或重启，按提示执行；不要承诺所有通讯参数都能在任意检测阶段热切换。
5. 点表重建可能替换自定义点位，操作前核实现场表。不同协议的地址格式不可直接照搬。
配置文件集中在 Project/Config/Communication。S7 使用 S7Config.xml、S7InputTableConfig.xml、S7OutputTableConfig.xml；Profinet 使用其子目录下的 ProfinetConfig.xml、PlcInput.xml。

## 通道、JobID、PosID 与相机角色
CH0 与 Cam1 不是同一个概念。一个通道通过 General Camera 的位置配置确定主相机和副相机。
- JobID 对应 JobN，例如输入 1 对应 Job1。
- PosID 对应 PosN，例如输入 2 对应 Pos2；位置号支持多位数字。
- 在发送前确认该 Job 存在，目标位置及相机映射有效，且相应 VPP 已配置。
- 主副通道都启用时，按照各自的通道字段参与流程，不能只看相机编号猜通道。
程序处理无效 Job/Pos 时会记录错误，保留上一组有效运行状态；因此“界面仍显示 Job1/Pos1”不代表 PLC 刚才发送的 Job/Pos 已被接受。错误请求后也不能假设系统会自动禁止所有后续 Trigger，应先纠正输入并确认状态。

## 设置用户数据：SetUserData、清结果和回显
用户数据包括 Clear Result、JobID、PosID、PartN（产品码）。
### 正常处理顺序
1. PLC 按所选协议准备字段并发出相应的数据更新请求。
2. 应用读取快照，先处理清除和确认相关请求。
3. Clear Result 大于 0 时，优先清结果，不继续切换 Job、Pos 或产品码。
4. 未请求清结果时，校验 Job/Pos，必要时加载对应通道的 Job VPP，更新有效运行状态和产品码。
5. 按协议写回 Job/Pos 回显。
### S7 注意事项
S7 在 SetUserData 上升沿读取一次 ClearResult/JobID/PosID/PartN。保持高电平后只改字段，不等同于提交一组新的请求。
SetUserData Ack 跟随请求电平，表示接收握手，不能用它单独证明 Job/Pos 合法或切换成功。还要检查日志、运行 Job/Pos 和对应回显。
无效输入时，旧回显也可能仍在。看到回显 1/1 必须区分它是本次成功写出的，还是上一轮保留的值。

## Trigger：取像、检测与结果输出
### 正常业务顺序
1. 满足系统、相机、授权、通道配置和协议触发条件。
2. 接收所选通道的 Trigger 事件，取得该通道当时的 Job/Pos 快照。
3. 先执行副相机组的取像与检测，再执行主相机组。
4. 主相机使用相应图像、数据字符串和 Limit 参数运行 Inspection.vpp。
5. 按协议和输出映射发布结果，并完成相应检测结束处理。
该顺序不是“先把全部相机取完图再统一运行全部算法”。同一通道使用锁避免重复并行执行；其它通道可有各自的执行进度。
### S7 输入与握手
Trigger Enable 是触发许可；Trigger Camera 的有效上升沿请求检测。Trigger Ack Camera 为相应触发握手，不能当成检测已经结束。
初始化时对保持为高的旧 Trigger 有防重放处理；应按照现场握手约定复位并产生新的有效边沿，不要依靠一直保持高电平反复触发。

## S7：Ready、Result Valid 与结果确认
以下说明对应当前 S7 实现，Profinet 的驱动管理细节不能直接套用成同样的位操作。
- 检测开始：Ready 置低，清本次 Result Valid 和 Exposure Complete。
- 结果发布：先清 Result Valid，校验本次检测的数据并逐项写入；全部数据写入成功后才置 Result Valid。任何引脚缺失、值转换或写入失败均不发布有效结果，查看 S7 运行日志。
- 检测结束：置 Exposure Complete，按“相机就绪 + 系统在线 + Trigger Enable”条件恢复 Ready。
- PLC 的 Inspection Result Acknowledge 有效请求用于清除 Result Valid。
- PLC 的 Clear Exposure Complete 有效请求用于清除 Exposure Complete。
Ready 恢复不以“先收到结果 ACK”为前提。结果 ACK 与完成位清除是各自的交互，不能把它们画成“ACK 后才能 Ready”的固定串行步骤。
输出写入调用成功，只能说明该次写操作成功，不能直接证明 PLC 逻辑已读取、采用结果；需要相应的 PLC 确认信号或现场证据。

## Profinet：输入布局和输出映射
Profinet 使用 CC24/Cognex 驱动管理部分底层握手，应用复用通道数据处理及检测流程。当前为四个通道，结果发送区为 240 字节。
### 输入
核对各通道 Clear Result、JobID、PosID、PartN 的字节起点、长度、类型。输入方向是 PLC 发给视觉，偏移以当前配置为准。
### 输出
区分固定握手字段与 VPP 输出字段。核对输出地址、长度、类型、所属通道和 VPP 引脚来源；关注越界及同一数据区重叠。
修改映射后需使 PLC 与视觉两侧一致。不能因为表格允许填写就认定对方 PLC 已使用相同布局。
驱动管理的触发/用户数据 ACK，应按实际驱动和 PLC 输入边沿判断；Manual 模拟不会伪造这些底层输入边沿。

## 算法输出与 PLC 数据
### 固定应用接口
Result_Cam 用于结果码处理；resultSend 用于汇总显示及副相机向主相机传递数据。Global.Result_Send 是程序内发送字节缓冲区，不是算法输出引脚。
### S7 主相机
旧点表的 Result X / Result Y / Result A 仍分别读取 Result_X / Result_Y / Result_A，结果码仍由原通道汇总逻辑生成，原有地址和格式保留。未主动绑定的旧数据行继续沿用该行为。
### S7 自定义输出配置
1. 在 Offline 状态打开 Setting → Communicate → Output Setting。
2. 选中目标通道的一行，点击 Add Output（新增输出），填写通道、参数名称、DB 地址、Format 和 String 的 Length。
3. 直接在 VPP Output 一列填写 VPP 输出引脚的准确名称（区分大小写），无需填写 Job、Camera、Position，也无需弹窗绑定。
4. 点击 Save 保存点表，再回到 Communication 点击 Save & Apply，使当前服务加载新点表。软件无需重启；应用通讯设置会沿用已有的通讯服务重新初始化流程，应在 Offline 状态操作。
5. 选中自定义数据行可 Delete Output；旧 X/Y/A/Result 数据行也可删除或重新绑定。固定握手和 JobID / PosID 行禁止删除、改名、改通道、绑定 VPP 和修改类型，地址仍按现场配置编辑。
数据行按所属通道分组。每个通道内参数名称唯一，DB 地址不允许重叠。绑定按通道生效，从该通道本轮检测的 VPP 输出快照读取，不借用上一轮缓存。多个主相机共用通道时，优先使用原通道结果汇总所选相机的同名引脚；该相机无此引脚时使用其它本轮主相机提供的同名引脚。需要分别发送不同相机的值时，应使用不同的引脚名。各 Job / Position 的 VPP 应保持该通道所绑定的输出接口一致。
支持 Bool、Byte、Int16、Int32、Float、Double、String。字符串按配置字节长度使用 ASCII，不允许超长或非 ASCII 值，短字符串补零清除旧尾部；数值越界、无穷大或 NaN 阻止发布。
保存点表时检查名称、绑定名称、地址和长度，保存算法时保护该通道已绑定引脚及类型；发送时再次检查本次引脚和实际值。配置尚未运行时输出值可以为空，不会因空值伪造或执行一次检测。
缩小通道数只隐藏停用通道，保存时保留其绑定。保存连接设置不会再重建已有点表；修改 DB 起点后，应在 Input / Output Setting 明确调整对应地址。
### Profinet / VPPOutput
按所配置的 Source / VppOutput 引脚映射数据，而不是仅靠表格 Name 自动推导引脚。Required 输出必须存在，数据类型和字节长度须与 PLC 一致。
保存算法时会做接口保护，但这只验证声明接口，不证明算法输出值正确、PLC 已收到或其它设备已完成动作。

## Manual → Trigger：是否进行 PLC 交互
1. 打开 Manual → Trigger，选择当前协议允许的通道。
2. PLC interaction 默认 No，核对模式后再执行；仅打开弹窗不会触发检测。
### No - local only
仍会调用真实相机取图和检测，需要相机及授权就绪。允许软件 Offline 时执行符合条件的本地检测，但不发送本次应用流程的开始、结果、结束等 PLC 输出，不修改待发送结果缓冲。本地模拟的图像/数据库持久化分支与正常自动流程不同。
No 只限制本次操作。已有通讯服务的轮询、心跳及其它通道的自动流程不会因此全局停用。它也不能隔离 VPP 脚本自己执行的外部 I/O。
### Yes - PLC interaction
要求对应通讯服务已启动、Online、相机和许可证就绪。复用正常事件处理后的应用流程，会对现场 PLC 产生真实交互，只能在现场允许的调试条件下使用。
没有连接相机时，应使用本地图片 Replay 调试算法，不要把 No 理解成“虚拟相机”。TCP/IP 当前不允许 Yes，因为实际 transport 尚未接入。

## Manual → Receive Data：输入模拟
1. 打开 Manual → Receive Data，选择通道。
2. 填 Clear Result（0/1）、PosID、JobID、PartN，并选择 PLC interaction。
3. 执行后，日志显示拼接后的逻辑字段及处理后的状态。该内容是应用输入快照，不是一帧可直接发给任意协议的网络字节流。
### No
模拟本地 Job/Pos/产品码处理，不写 PLC 回显，也不清改真实 PLC 输出。Clear Result 仍保留优先级，但日志会说明 PLC 数据保持不变。
### Yes
按正常应用流程清结果或写 Job/Pos 回显，需满足相应服务和就绪条件。
该功能不是临时沙盒：成功切换后的本地 Job/Pos/产品码会被后续触发使用。它不伪造 PLC 的 Trigger/SetUserData 输入位，也不保证生成底层驱动管理的 ACK。

## 通讯故障检查表
### 系统不在线 / 没有 Ready
依次核对 Online 请求、许可证、相机启动校验、恢复/切换过程、所选协议和服务状态。S7 还需核对 PLC Trigger Enable。先确认实际条件，不能只凭按钮红绿或 Waiting 判断全部链路。
### PLC 发了 Trigger，软件不检测
核对实际通道、在线及相机就绪、Trigger Enable、有效触发边沿、通道是否忙、Job/Pos 与主相机配置。查看前后日志，区分未收到触发与已收到但被保护条件拒绝。
### 程序号/位置号没有反馈
比较 PLC 请求值、应用当前运行值和最后一次输出回显。核对 Job/Pos 是否存在、VPP 是否能加载、是否请求了 Clear Result、S7 是否产生新的 SetUserData 上升沿。ACK 电平不代表配置校验成功。
### 检测完成但 PLC 数据不对
核对固定引脚及配置的 Source、通道、字节地址、长度、类型、字节序和 Required 输出；再核对 Result Valid/完成信号与 PLC 确认。不要把旧结果当本次结果。
### 一直提示 Unavailable
查看相机启动检查及授权日志。Manual Trigger 仍需真实相机；无相机时使用 Replay。Cognex License NG 和相机断线是需要分别确认的条件。
### 保存通讯配置后显示 Communicate:Null
确认选择并保存了协议，配置文件位于当前正在运行的程序目录，而不是另一个 Debug/旧包；查看保存错误、权限和初始化日志。协议名称恢复正常仍不表示 PLC 已连接。

## 排障信息收集与当前边界
建议记录：软件 Build 版本、协议、通道号、Job/Cam/Pos、错误时间、完整异常、该时间前后的日志，以及相关 PLC 输入/输出和握手信号。
请区分“读到的输入”“计划写入”“写入成功”“PLC 已确认”，这些不是相同证据。
- WorkFlow 实时流程监视器尚未作为已完成功能发布，本帮助不能替代实时信号监视。
- TCP/IP 实际网络传输尚未实现，不提供可直接用于现场的虚构报文格式。
- 帮助不是 Agent，不自动诊断现场，也不自动修改参数、重置 PLC 或触发设备。
- 不确定具体原因时，保存证据后再排查，避免连续修改多个通讯参数而失去可比较的状态。
