# 软件操作

## 阅读说明与快速查找
本帮助适用于随当前程序发布的版本，按实际界面和代码行为编写。菜单同时列出英文名称，便于在中英文界面中定位。
- 左侧“软件操作”介绍配置、检测、查询与部署；“通讯帮助”介绍 PLC 交互、手动模拟及排障。
- 在上方搜索框输入关键词，例如“新增 Job”“resultSend”“Unavailable”“Ready”“0x8007045A”。搜索覆盖两个模块的标题和正文；清空搜索可恢复完整目录。
- 帮助窗口只浏览文档，不取图、不执行算法、不修改配置、不连接 PLC。窗口可以保持打开，原自动检测继续运行。
- 文档不是实时诊断结果。判断具体设备故障时，结合当前版本、日志时间、通道和现场信号。

## 启动前检查与启动日志
1. 现场复制整个 Debug 部署目录，保留 Project、Data、LmiSdk、x64 和程序旁的依赖文件。
2. 确认安装匹配的 VisionPro 9.6 SR2 运行环境和所需功能授权。当前工程托管程序集版本为 71.2.0.0，不能只凭 DLL 文件名判断版本。
3. 确认相机供电、网络、序列号绑定、PLC 通讯参数符合现场配置。
4. 启动窗口显示各步骤、当前操作、耗时和 VPP 文件路径。长时间停顿时，记录最后一条步骤和具体 Job/Cam/Pos 文件路径。
启动会按现有流程检查真实相机和准备通讯，不是纯离线回放工具。不要为了查看帮助而把“启动软件”理解成不会访问设备。
### 卡在 Loading Inspection 或 Loading Camera
Inspection.vpp 与 Camera.vpp 是两种不同文件。算法加载慢可能涉及文件体积、工具反序列化、授权或版本；取像工具还可能涉及驱动及设备绑定。仅凭“Load VPP”不能确定根因。保留完整异常和所加载 DLL 信息，不要通过混放其它版本 DLL 试错。

## 主界面、状态和相机显示布局
每张相机卡片从上到下为：Cam 名称与状态、图像、Total/Pass/Pass Rate、Job/Pos/Engine、Reset Count/Trigger Manual/Replay。
- 黄色 Waiting 表示等待状态，单凭它不能判断相机或 PLC 已经就绪。
- Job、Pos、Engine 用于查看当前选择和通道归属。原有可点击选择功能仍在，请依据当前角色权限操作。
- Reset Count 重置相机统计，点击前确认需要清除计数。统计不是数据库历史记录。
- 进入 Setting → Setting → Display Layout，设置 Rows 和 Columns，点击 Save 生效。行列不足时程序自动增加行数，不隐藏已配置的相机；过密布局会出现滚动区域。
- 底部日志区域可拖动分界条调整高度，图像区域随之调整。六相机常用三列两行，可依据显示器尺寸调整。
- 下方状态栏分开显示当前用户及 Communicate 协议名称；右上角显示 Online/Offline。协议名称、通讯连接、相机就绪和 PLC 正常应答是不同信息。

## 登录、角色和语言
入口：Login。根据现有账号登录；Change Password、Register、Delete 等操作按界面权限使用。菜单灰色通常与角色权限有关，不能通过改文件名绕过权限。
入口：Setting → Setting → General → Language。选择中文或 English 后点击 Save，全局界面文字按保存的语言切换；Job/Cam/Pos 标识、引脚名称、用户数据和原始日志不会被翻译。
本版帮助正文为中文，保留英文菜单、信号和错误关键词方便检索。

## 设置中心与保存规则
入口：Setting → Setting。左侧包含 Job Control、General、Limit Setting、Display Layout、General Camera。
- General：存图、FTP、语言、日志显示和程序名称等。
- Limit Setting：按 Job/Camera 编辑参数、说明和值，可添加或删除参数行。
- General Camera：编辑位置及主副通道分配，可添加或删除位置行。
- Display Layout：调整主界面相机控件行列。
- Job Control：集中新增、克隆、删除 Job 和相机。
普通设置页使用右下角 Save；切换页面时如有未保存编辑，会提示处理。Job Control 的结构操作立即保存，不另设 Save。
结构变更后，其它设置页在再次选择时读取新配置，无需为查看新增 Job/Camera 重启软件。涉及 SDK 设备绑定的保存仍有“生产前重启”提示，不能把所有种类的硬件修改都视为即时重新连接。

## Job Control：新建、克隆、删除 Job
入口：Setting → Setting → Job Control。
### 新建空白 Job
1. 在 New Job 填写未使用的规范编号，如 Job2；当前允许 Job1～Job255。
2. 点击 Create empty Job。空白 Job 不继承相机和 Limit 参数。
3. 在该 Job 下 Add Camera，随后完成 General Camera、Camera、Algorithm 和 Limit 配置。
### 从现有 Job 克隆
1. 在 New Job 填写目标编号，在 Clone from 选择来源。
2. 点击 Clone Job，等待复制结束。
3. 克隆包含相机绑定、位置和通道分配、Limit 参数、相应 Camera.vpp / Inspection.vpp。SDK 相机不需要 Camera.vpp。
4. 新 Job 沿用来源的输出映射。克隆会保留相机 IP/SN 等绑定，必须核实它们是否符合目标项目。
源文件缺失时中止克隆。目标编号已有保留的 VPP 目录时，不覆盖、不混合，改用其它目标编号。
### 删除 Job
选择 Job 后点击 Delete Job，并核对确认提示。正在被运行通道选用的 Job 不能直接删除，至少保留一个 Job。删除配置及对应输出映射后，磁盘 VPP 文件仍保留。
### 提示不能操作时
检查真实在线状态、是否存在忙通道、相机恢复或在线切换过程。程序会提示具体原因。Offline 时相机未连接不等于必须连接相机才能新增空白 Job；但检测、恢复或切换未结束时仍可能阻止结构修改。

## Job Control：新增和删除相机
Add Camera / Delete Camera 已统一放在 Job Control，不在 General Camera 中操作。
1. 选择目标 Job，点击 Add Camera。
2. 选择 Visionpro 或 SDK；SDK 品牌目前为 LMI，后续品牌通过预留接口扩展。
3. 新相机自动建立 Pos1，主副通道默认不启用。
4. Visionpro 创建缺失的 Camera.vpp 和 Inspection.vpp；SDK 创建缺失的 Inspection.vpp。
5. 到 General Camera 分配位置、主副通道，再配置取像和检测工具。
空白 VPP 不是已经配置好的生产程序，不能仅凭文件存在就开始检测。
删除相机移除配置，保留 VPP。以后重新使用同名相机，已有 VPP 不覆盖；因此重新创建后也要检查原文件内容。相机编号不因删除而重新排号。

## General Camera：位置与主副通道
入口：Setting → Setting → General Camera。选择 JobN、CamN，表格每行对应一个 Pos。
- Main Used = 1：该相机在该位置作为相应 Main Channel 的主相机。
- Second Used = 1：该相机在该位置作为相应 Second Channel 的副相机。
- Used = 0：不参与对应角色的检测。通道从 0 开始编号，不等同于从 1 开始的 Cam 编号。
- Add row / Delete row 管理位置配置。更改 Pos 名称前，核实对应磁盘 VPP 路径；不要把改名理解为自动搬迁已有算法文件。
- 点击右下角 Save 保存。主相机选择及通道分配还应与 PLC 输入输出映射一致，避免同一通道存在相互冲突的主相机配置。
Camera 和 Algorithm 上方不再手工选择 Channel，而是根据 Job、Camera、Position 的配置确定适用通道。

## Visionpro 取像配置
入口：Setting → Camera → Visionpro。这里只列出该 Job 中配置为 Visionpro 的相机。
1. 选择 JobID、Position、Camera，核对 SN。
2. 在 Cognex 取像工具中选择设备、视频格式及适用的曝光、触发等参数。
3. 使用上方 Load 读取指定位置的 Camera.vpp；Save 保存当前取像工具。
4. 保存后核实当前 Job/Cam/Pos，避免改到其它位置。
SN 用于相机身份核验；为空时按现有配置规则处理，填入时应与真实设备一致。
VisionPro 控件内的运行、实时显示等按钮可能访问真实相机；不连接设备时不要把这些按钮当作离线算法回放。

## SDK 相机与 LMI 配置
入口：Setting → Camera → SDK-3D。SDK-2Dline、SDK-2D 是其它类别的预留页面，只显示对应种类的相机，不能因存在菜单就认定已有对应品牌驱动。
当前 LMI 使用 GoSDK，绑定按每个 Job/Camera 保存，由该相机的各个 Position 共用。
1. 在 Job Control 添加 SDK / LMI 相机，再进入 SDK-3D。
2. 选择 JobID、Camera、Position，填写 Sensor IP、Timeout 和需要核验的 SN。
3. 多台物理相机分别使用对应的 IP；使用 Accelerator 时按设备实际虚拟传感器地址配置。
4. LMI 当前接入要求 Uniform Surface 输出，SDK 模式不使用 Camera.vpp；检测仍使用 Inspection.vpp。
5. Save 保存绑定。当前界面提示生产前重启，请按提示执行并重新验证相机。
Test Capture 会取图，Diagnostics 也可能加载 SDK、连接传感器；只有在允许访问现场设备时才使用。它们使用当前草稿，不要求先保存。
LmiSdk 已随 Debug 部署，不需要在界面另选 SDK directory。不要混放不同版本 GoSDK/kApi DLL。LMI 接入没有替代设备厂商对具体型号、固件和 SDK 版本兼容性的要求。

## Algorithm：编辑、引脚保护与保存
入口：Setting → Algorithm。选择 JobID、Camera、Position，编辑相应 Inspection.vpp。
### 固定输入输出
- 前两个输入按顺序接收图像和数据字符串。已有算法保存时还保护这两个输入原有的名称及顺序。
- Result_Cam：检测结果码，供显示和主相机结果处理读取。
- resultSend：汇总字符串，供显示及副相机到主相机的数据传递读取。
- Global.Result_Send 是程序内部的 PLC 发送缓冲区，不是 VPP 输出引脚；不要把 resultSend 改名为 Result_Send。
主相机还检查配置中必需的输出。S7 主相机需要 Result_X、Result_Y、Result_A；Profinet 按对应通道配置的 VPP 输出检查。
### 保存
使用应用上方 Save 或 Ctrl+S。内嵌 VisionPro 原生保存/另存为已收起，避免绕过校验。
编辑使用独立草稿。缺少引脚、固定输入改名/改序或类型不合适时，保存被阻止，弹窗和日志列出问题，原文件及运行缓存不替换。恢复引脚及实际连接后再保存，不要只补一个没有连接的同名引脚。
校验成功后保存文件，并更新该 Job/Cam/Pos 相关主副通道的独立缓存。通道忙、恢复中或状态切换中时请等待完成。
校验不运行算法、不要求输出已有值，也不证明检测逻辑或引脚连接结果一定正确。VPP 内脚本若自行访问外部设备，其行为不属于应用通讯层的隔离范围。
Load 会按现有逻辑重载文件和运行缓存，不是只读预览；请在合适的停机调试时机使用。

## Limit 参数与 General 常规设置
### Limit Setting
选择 JobN/CamN 后编辑对应位置的参数名称、Description 和 Value，点击 Save。参数名称应与算法输入引脚一致；参数名不同不能自动匹配。添加参数不等于自动给算法创建对应输入。
只在 Job Control 新增或删除 Job；Limit 页面不再承担这些操作。
### General
- Save type：目前为 BMP/PNG，配合 Enable Save image 决定正常流程的存图行为。
- Delete cycle (min)：图像保留与清理周期设置，修改前确认目录和现场保存要求。
- Image directory：“...”选择图像目录，现场须具有写入权限和足够磁盘空间。
- FTP：配置地址、用户、密码和根目录，并勾选相应开关；修改后 Save。填写配置不等于 FTP 服务器已连接成功。
- Log display：控制部分日志显示；Language 和 Program Name 同页保存。
手动局部检测的存图、数据库行为与正常自动流程可能不同，见“手动触发、回放与无相机调试”。

## 手动触发、回放与无相机调试
### Trigger Manual 与 Manual → Trigger
Trigger 会调用真实相机取图，不是生成模拟图像。没有连接相机、相机启动检查失败或授权未就绪时，不可执行真实手动取图。
日志 Camera startup check is not complete; manual trigger is unavailable. 表示相机启动验证未完成。若同时出现 Cognex License NG，还要单独核实授权。
Manual → Trigger 的 PLC interaction：No 仅关闭本次应用流程的 PLC 输出，仍需要相机和授权；Yes 会参与真实 PLC 交互。详情见“通讯帮助”。
### Replay
没有相机、只想调试算法时，使用 Replay 加载本地图片。图像格式与算法输入必须匹配，VisionPro 对所用工具的有效授权仍然需要。回放不等于 PLC 的完整自动流程。
### Receive Data
用于模拟输入字段的应用处理，不负责生成图像。No 可用于本地数据状态调试，但会保留本次选择的 Job/Pos/产品码，后续触发会使用这些状态。

## 日志查询和 Database 数据库
### 日志
- ALL 查看当前筛选允许显示的信息；Warning 聚焦警告；Communicate 聚焦通讯信息。
- 右侧齿轮设置 Auto scroll 和 ALL 分类筛选。筛选改变可见内容，不表示被隐藏的事件没有发生。
- 双击日志打开详情，支持自动换行和 Copy All。报障时保留完整异常、前后事件和时间，不只截最后一行。
### Database
入口：Database。选择 Start/End，点击 Filter 查询；左侧为记录，右侧显示选中记录详情及完整 Payload。
- 使用 Previous/Next 翻页；Export 导出当前功能对应的数据。
- Clear All 会删除数据库记录，按确认提示操作；Reset Count 不等于 Clear All。
- 数据库当前不是“每条记录都能自动打开对应原图并复现算法”的完整追溯系统，不能仅凭有记录就认定原图和当时 VPP 版本已关联保存。

## 现场部署目录与版本更新
复制整个 bin/x64/Debug，不只复制 EXE 或单独挑选 DLL。
- Project/Config/General：存图、语言、界面布局、日志显示选项。
- Project/Config/Jobs/Parameter.xml：Job、相机、位置及 Limit。
- Project/Config/Security：用户、角色、权限。
- Project/Config/Communication：通讯方式、VPP 输出映射，及 Profinet/S7/TcpIp 子目录。
- Project/VPP/JobN/CamN/PosN：取像及检测文件。
- Data/Database：VisionData.db 正式数据库；LegacyOrders.db 为保留的旧历史数据。
- Data/Stats：相机统计；Logs：应用与 VisionPro 诊断日志。
- LmiSdk 和 x64/SQLite.Interop.dll：必要运行库。
- Visual Inspection.exe.config 和 VisionPro 引用清单必须随程序保留在 EXE 旁。
旧路径会按迁移规则整理，已存在的新分类配置不会被旧副本覆盖。迁移冲突不能通过删除正在使用的数据文件解决。
更新现场程序时先明确需要保留的项目配置、VPP、用户、数据库和统计，避免用开发机数据覆盖现场数据。

## 常见异常与排查顺序
### 0x8007045A / Comm.dll / Core.dll
该错误表示 DLL 初始化失败，不能仅凭错误码认定一定是版本不一致。核对 VisionPro 安装、进程位数、程序集版本、原生依赖、运行日志和实际加载路径。查看 Logs/VisionPro；不要混用其它版本 DLL。
### 0x80131040 / 程序集清单与引用不匹配
核对程序、VisionPro 环境和 VPP 制作版本；从完整且一致的部署包恢复。文件名相同不代表程序集版本相同。
### Cognex License NG / CodeMeter / PMAlign
核实授权设备、Security Service、指定许可证及所需工具功能授权。程序能启动或基础控件能打开，不代表 PMAlign 等工具已授权。
### SN 不匹配 / 取相板卡不再存在 / Camera offline
检查实际相机身份、供电、网卡、驱动、取像工具和 SDK 绑定。相机恢复或取图测试会访问设备，先确认现场允许。
### 保存失败：缺少引脚
按弹窗恢复 Result_Cam、resultSend、固定输入或配置要求的其它输出及其连接。校验通过再保存；不会自动补无连接引脚。
### 通讯不动作
先到“通讯帮助 → 通讯故障检查表”，区分系统状态、相机就绪、协议服务、PLC 输入和 Job/Pos 校验，不把所有情况都归为 PLC 没信号。
### 可选外部插件与焊前寻址
插件统一放在 EXE 旁的 Plugins/模块名 子目录，正常 Debug 位置为 bin/x64/Debug/Plugins。公共接口 Aron.PluginContracts.dll 保留在 EXE 旁。默认不启用：没有 Enabled.xml 或未勾选时不会加载该 DLL。
从设置中心的 External plugins（外部插件）选择本项目需要的模块。Offline、具有设置权限且通道/插件任务空闲时保存，重启后生效。启用 PreWeld 后，Tool → External plugins 出现 Pre-weld setup 与 Cell replay and compensation；Tool 仅显示插件功能，没有管理入口，没有可用插件功能时隐藏该菜单。
插件参数位于 Project/Config/Plugins，运行 XML 位于 Data/Plugins/PreWeld，日志位于 Logs/Plugins/PreWeld。预焊寻址使用原 Cell/Mark 字符串、去重/排序、高度合并、补偿和回放规则；设置收集 Job 与汇总 Pos，同一产品码下该位置的所有启用相机完成才生成 XML。手动/Replay 不收集生产数据；插件不发送 PLC 信号、不增加 Socket，不改变检测结果及原握手。旧的四区 Welding/Socket 功能未包含。旧规则项保留英文标识。
完整开发、部署、旧参数搬迁与使用边界见源码 Build/外部插件与焊前寻址.md。生产前核对插件加载状态与现场 VPP 输出；异常/队列过载会停止该插件并报警，原检测流程继续，修复后重启。更新 DLL 需关闭程序，保留 Project/Data/Logs；插件不是进程崩溃隔离沙箱。

### 导出诊断与异常报告
Help → Export diagnostics（帮助 → 导出诊断包），无需联网，不启动相机或 PLC。诊断包保存到 Logs/Diagnostics/Packages，包含当前快照、最近 3 天内的有限量日志、脱敏后的配置副本和 EXE/PDB/SDK 文件身份校验值。导出完成会提示完整路径；配置是在导出时读取，不一定等同故障当时的配置。安全账户目录、数据库、图像、VPP 和运行库二进制不打包。
Logs/Diagnostics 下的 events_*.jsonl 是独立于界面的诊断事件；Crash/crash_*.txt 是捕获到的异常报告，包含异常堆栈、最近 200 条事件和最后观察到的流程节点。记录 UTC 时间、运行会话、版本、线程和检测操作编号；节点只表示最后观察位置，不直接证明该位置就是故障原因。正常退出会尽力刷盘；原生 DLL 崩溃、断电、强制结束或进程损坏不能保证生成异常报告。
若程序目录不可写，诊断目录依次回退到当前用户 LocalAppData/Aron/Logs/Diagnostics、系统临时目录下 Aron/Logs/Diagnostics。普通 Application/VisionPro 日志仍有各自的写入规则。
诊断事件后台有界写入，队列过载会丢弃待写事件并计数，快照注明丢弃数量和最后写入失败。诊断事件/异常报告按约 20 MiB 分片、30 天/约 512 MiB 清理，最近事件另有内存缓冲。导出 ZIP 不自动清理，现场需按空间保管。
已知密码类 XML 字段会脱敏，日志仍可能有产品码、网络地址等现场信息，发送前检查。故障时提供时间、通道、操作步骤及诊断 ZIP；涉及算法时另附相应 VPP 和当时输入图像，源代码行号分析还需保留匹配版本源码和 PDB。
