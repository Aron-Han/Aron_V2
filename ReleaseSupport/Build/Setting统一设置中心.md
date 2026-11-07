# Setting 统一设置中心（方案 A）

入口：主菜单 Setting → Setting。左侧为 General、Limit Setting、General Camera，右侧切换对应页面；原菜单中的重复 Limit Setting、General_Cam 入口隐藏。Camera、Algorithm、Communicate 及其子菜单保留原入口。

## 实现边界

- `SettingsCenterForm` 只负责白底/浅蓝选中导航、标题、原窗体嵌入、分类切换和未保存提醒。使用当前系统 DPI，并限制初始窗口不超出工作区。
- 页面首次进入按需创建，未修改的页面在设置中心会话内复用，减少重建控件和读配置带来的切换停顿。保存成功后使其它缓存页面失效；切换时也检查配置文件大小/修改时间，变化则重建目标页，避免使用旧配置。确认放弃未保存修改后释放该页；关闭设置中心释放全部缓存。
- 用户编辑表格、输入框、复选框等或使用增删按钮后，切换分类/关闭会提醒。只有原保存成功事件才清除提醒；未保存时默认保留当前页面。增删按钮取消也可能产生保守提醒，不代表数据已被保存。
- General 页仅重新排版原 ImageRecord 控件，图片保存、清理周期、FTP、日志显示、许可证和程序名字段均保留，没有新增参数或改动配置格式。独立 ImageRecord 原布局不变。
- Limit Setting、General Camera 复用原窗体、数据绑定、保存校验、VPP 创建行为和事件，未重写主副通道逻辑。
- 原主界面保存成功回调保持：General 刷新图片设置/标题/原监控；Limit 调用 ReloadConfigAndUI；General Camera 重新读取参数并调用原 Load_Job。
- 分类权限使用原菜单对象的 Enabled 状态，不新增权限、不开放被禁用的设置。
- 三个页面原有 Save 按钮实例移入固定页脚，统一 100×32 逻辑像素（当前 150% 为 150×48），相同右/底边距，不随参数滚动。原按钮的 Click、Enabled、保存校验和成功事件不变。页脚提示与按钮占用互不重叠的区域。
- General 页标签和字段采用行内垂直居中，统一上下间距；目录浏览按钮与路径框等高，缩窄至 42 逻辑像素。表格选择工具栏保留原对齐逻辑。
- 今后增加设置分类，可追加 Page 定义及其原保存成功回调，不需要改导航/表格框架。未实现的分类不会显示空页面。

## 验证

`Tests/SettingsCenterTests.cs` 在隔离输出中运行，使用合成 Job/相机/参数，未调用任何真实 Save。验证干净页面复用、保存及外部配置变化使缓存失效、原权限门禁、未保存状态、多个窗口尺寸、原表格列及主副通道值；另验证三个原 Save 按钮统一尺寸/边距/固定页脚、原点击事件仅执行一次、标签中心线、目录浏览按钮对齐及行间距。使用应用的 system-DPI manifest 验证当前 150% DPI。

界面截图：`Tests/UIValidation/SettingsCenter-0.png`（General）、`SettingsCenter-1.png`（Limit）、`SettingsCenter-2.png`（General Camera）。

同时运行原设计器启动保护和 22 项模拟 S7 通讯回归。S7VisionCommService、ProfinetVisionCommService、CommunicationConfig 的 SHA-256 与原基线一致；GoSDK 打包校验通过。测试未执行 Program.Main、真实相机取像或 PLC 连接。

隔离输出：`Aron_V2/bin/SettingsCenterValidation`。该目录用于验证，不应作为带现场配置的部署目录。正常程序运行时不要覆盖 EXE；关闭后在 VS 正常生成 Debug 即可更新 `bin/x64/Debug`，原 Project 配置/VPP 不变。
