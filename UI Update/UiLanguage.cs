using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Aron_V2
{
    // UI captions only: no culture/number parsing changes, editable values,
    // parameter keys, protocol data, log payloads or vendor controls.
    internal static partial class UiLanguage
    {
        private sealed class Entry { internal string Source; internal bool Applying; }
        private static readonly ConditionalWeakTable<object, Entry> Entries = new ConditionalWeakTable<object, Entry>();
        private static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Enable one protocol"] = "选择通讯协议", ["Active channel count"] = "启用通道数",
            ["Profinet is fixed at 4 channels."] = "Profinet 固定为 4 个通道。",
            ["Configurable: 1 to 64 channels."] = "可配置 1～64 个通道。",
            ["Uses the standard S7 handshake, address tables, heartbeat and reconnect logic."] = "使用标准 S7 握手、地址点表、心跳及重连逻辑。",
            ["Uses the Cognex CC24 Profinet card. Input/output buffers are limited to four channels."] = "使用 Cognex CC24 Profinet 通讯卡，输入输出固定支持 4 个通道。",
            ["Uses the same logical handshake as S7. Socket frame encoding is configured by the project protocol."] = "使用与 S7 相同的逻辑握手，Socket 数据帧编码由项目协议配置。",
            ["Connection Setting"] = "连接设置", ["Save & Apply"] = "保存并应用",
            ["Communication selection saved."] = "通讯设置已保存。",
            ["Pos:"] = "位置：", ["Job:"] = "作业：", ["New parameter"] = "新参数",
            ["Clone Limit parameters to"] = "克隆 Limit 参数到", ["From:"] = "来源：",
            ["S7 Setting"] = "S7 连接设置", ["S7 Input Setting"] = "S7 输入设置", ["S7 Output Setting"] = "S7 输出设置",
            ["Profinet Input Setting"] = "Profinet 输入设置", ["Profinet Output Setting"] = "Profinet 输出设置", ["TCP/IP Setting"] = "TCP/IP 连接设置",
            ["DB Address"] = "DB 地址", ["Result Address"] = "结果区地址", ["UserData Address"] = "用户数据区地址",
            ["Data Type"] = "数据类型", ["Format"] = "格式", ["Length"] = "长度", ["Current Value"] = "当前值", ["Note"] = "备注",
            ["Read All"] = "读取全部", ["Read Selected"] = "读取选中项", ["Read Channel"] = "读取通道",
            ["Write Selected"] = "写入选中项", ["Write Channel"] = "写入通道", ["Delete Selected"] = "删除选中项",
            ["Connect Test"] = "连接测试", ["Beat Once"] = "发送一次心跳", ["Start Heartbeat"] = "启动心跳", ["Stop Heartbeat"] = "停止心跳",
            ["S7 Basic Parameters"] = "S7 基本参数", ["Heartbeat Parameters"] = "心跳参数", ["Manual Address Read / Write Test"] = "地址手动读写测试",
            ["Read"] = "读取", ["Write"] = "写入", ["Save succeeded."] = "保存成功。",
            ["JobID and PosID are required and cannot be deleted."] = "JobID 和 PosID 是固定信号，不能删除。",
            ["Channel must be 0..3."] = "通道号必须为 0～3。",
            ["Custom output requires both Parameter and VPP Output."] = "自定义输出必须填写参数名称和 VPP 输出引脚。",
            ["Please select one row first."] = "请先选择一行。", ["Please select one channel row first."] = "请先选择一个通道行。",
            ["Please enter a valid IP address."] = "请输入有效的 IP 地址。",
            ["VPP input:"] = "VPP 输入引脚：",
            ["Add parameter"] = "新增参数", ["Parameter name:"] = "参数名：", ["OK"] = "确定",
            ["Clone"] = "克隆",
            ["About"] = "关于", ["Logo"] = "Logo 设置", ["Import image"] = "导入图片",
            ["Choose a logo or import a local image."] = "选择一个默认 Logo，或导入本地图片。",
            ["Save updates the window and taskbar icon. The EXE file icon updates on the next build."] = "保存后更新窗口与任务栏图标；EXE 文件图标将在下次编译时更新。",
            ["Add Output"] = "新增输出", ["Delete Output"] = "删除输出", ["VPP Output"] = "VPP 输出引脚",
            ["Job Control"] = "作业管理", ["New Job:"] = "新作业：", ["Clone from:"] = "克隆来源：",
            ["Help"] = "帮助", ["Software Operation"] = "软件操作", ["Communication Help"] = "通讯帮助",
            ["Export diagnostics"] = "导出诊断包", ["Diagnostic package saved. Review logs before sharing:"] = "诊断包已保存，发送前请检查日志中的现场信息：",
            ["External plugins"] = "外部插件", ["Plugin management"] = "插件管理", ["Plugin"] = "插件", ["Status"] = "状态",
            ["Pre-weld setup"] = "焊前寻址设置", ["Cell replay and compensation"] = "Cell 回放与补偿",
            ["Enable optional plugins for this project. Save and restart to apply. Disabled plugins are not loaded."] = "选择本项目需要的插件，保存后重启生效。未启用的插件不会加载。",
            ["Plugin selection saved. Restart the application to apply."] = "插件选择已保存，重启程序后生效。",
            ["Switch Offline with configuration permission before saving."] = "请使用有配置权限的账户切换为离线后保存。",
            ["Search help:"] = "搜索帮助：", ["Clear search"] = "清空搜索", ["Copy topic"] = "复制本页",
            ["No matching help topics"] = "未找到匹配主题", ["Try a shorter keyword, an error code, a signal name, or clear the search."] = "请尝试更短的关键词、错误码、信号名称，或清空搜索。",
            ["Clipboard is busy. Please try again."] = "剪贴板正忙，请稍后重试。",
            ["Wait for camera recovery or Online/Offline switching before saving the algorithm."] = "请等待相机恢复或在线/离线切换结束后再保存算法。",
            ["Algorithm save blocked: channel is busy."] = "算法保存已阻止：以下通道正在执行操作。",
            ["Use the top Save button to validate and save this algorithm."] = "请使用上方 Save 按钮，校验通过后保存算法。",
            ["Online/Offline switching is in progress. Please wait."] = "正在切换在线/离线状态，请稍候。",
            ["The application is stopping. Job Control is unavailable."] = "程序正在关闭，暂时不能修改作业。",
            ["Camera recovery is in progress. Please wait."] = "正在恢复相机，请等待恢复结束。",
            ["Channel operations are still in progress:"] = "以下通道仍在执行操作：",
            ["The system is Online. Switch Offline before editing Job Control."] = "系统当前在线，请先切换为离线再修改作业。",
            ["Mode"] = "模式", ["Job already exists."] = "该作业已存在。", ["Camera already exists."] = "该相机已存在。",
            ["Job not found."] = "未找到作业。", ["Camera not found."] = "未找到相机。", ["Keep at least one Job."] = "至少需要保留一个作业。",
            ["Retained VPP directory exists. Choose another Job ID; existing files will not be overwritten."] = "此作业目录已有保留的 VPP，请使用其它作业编号；已有文件不会覆盖。",
            ["Create empty Job"] = "创建空白作业", ["Clone Job"] = "克隆作业",
            ["Operations save immediately and refresh the settings pages."] = "操作立即保存，并同步更新各设置页面。",
            ["Clone includes Limits, camera bindings and all configured VPPs. Deleted VPP files are retained."] = "克隆包含限值、相机绑定及全部配置的 VPP；删除后磁盘 VPP 文件保留。",
            ["Login"] = "登录", ["Setting"] = "设置", ["Settings"] = "设置中心", ["Database"] = "数据库", ["Tool"] = "工具", ["Manual"] = "手动",
            ["General"] = "常规设置", ["Limit Setting"] = "限值设置", ["General Camera"] = "相机常规设置", ["General_Cam"] = "相机常规设置",
            ["Display Layout"] = "显示窗体设置", ["Rows:"] = "行数：", ["Columns:"] = "列数：", ["Cameras:"] = "相机数量：", ["Layout:"] = "实际行列：",
            ["Save applies immediately. Cameras keep their order. Extra rows are added if needed; no camera is hidden."] = "保存后立即生效，相机顺序保持不变。行列不足时自动增加行数，不会隐藏相机。",
            ["Save applies immediately. Cameras keep their order. Unused rows and columns collapse automatically; no camera is hidden."] = "保存后立即生效，相机顺序保持不变。根据相机数量自动收紧多余行列，不会隐藏相机。",
            ["Camera"] = "相机", ["Algorithm"] = "算法", ["Communicate"] = "通讯", ["Communication"] = "通讯", ["Input Setting"] = "输入设置", ["Output Setting"] = "输出设置",
            ["Rotate Center"] = "旋转中心", ["Trigger 0"] = "触发 0", ["Trigger 1"] = "触发 1", ["Receive Data"] = "接收数据",
            ["Save"] = "保存", ["Save Changes"] = "保存修改", ["Load"] = "加载", ["Apply"] = "应用", ["Cancel"] = "取消", ["Close"] = "关闭", ["Copy All"] = "复制全部",
            ["Add row"] = "添加行", ["Add Row"] = "添加行", ["Add raw"] = "添加行", ["Delete row"] = "删除行", ["Delete Row"] = "删除行",
            ["Add Camera"] = "添加相机", ["Delete Camera"] = "删除相机", ["Add new Job"] = "新增作业", ["Delete Job"] = "删除作业",
            ["Image storage"] = "图像存储", ["FTP upload"] = "FTP 上传", ["Display and application"] = "显示与程序",
            ["Save type:"] = "保存格式：", ["Enable Save image"] = "启用图像保存", ["Delete cycle (min):"] = "保留时间（分钟）：",
            ["Image directory:"] = "图像目录：", ["Enable FTP:"] = "启用 FTP：", ["UpLoad Image to FTP"] = "上传图像到 FTP",
            ["IP Address:"] = "IP 地址：", ["User:"] = "用户名：", ["Password:"] = "密码：", ["FTP Root:"] = "FTP 根目录：",
            ["Log display:"] = "日志显示：", ["Show Upload image info"] = "显示图像上传信息", ["Show Data Received from PLC"] = "显示 PLC 接收数据",
            ["Show image cleanup log"] = "显示图像清理日志", ["Lincese:"] = "许可证：", ["Program Name:"] = "程序名称：", ["Language:"] = "界面语言：",
            ["JobN:"] = "作业：", ["JobID:"] = "作业：", ["CamN:"] = "相机：", ["Cam:"] = "相机：", ["Camera:"] = "相机：", ["Position:"] = "位置：", ["Channel:"] = "通道：",
            ["Cam count:"] = "相机数量：", ["Mode: Visionpro"] = "模式：Visionpro", ["Mode: SDK"] = "模式：SDK",
            ["Model"] = "作业", ["Job"] = "作业", ["Cam"] = "相机", ["Pos"] = "位置", ["Position"] = "位置", ["Parameter"] = "参数", ["Description"] = "说明", ["Value"] = "数值",
            ["Main Used"] = "启用主通道", ["Main Channel"] = "主通道", ["Second Used"] = "启用副通道", ["Second Channel"] = "副通道",
            ["MainUsed"] = "启用主通道", ["MainChannel"] = "主通道", ["SecondUsed"] = "启用副通道", ["SecondChannel"] = "副通道",
            ["Log"] = "日志", ["ALL"] = "全部", ["Warning"] = "警告", ["Auto scroll"] = "自动滚动", ["Time"] = "时间", ["Type"] = "类型", ["Message"] = "消息",
            ["Record Details"] = "记录详情", ["Previous"] = "上一页", ["Next"] = "下一页", ["Load failed"] = "加载失败",
            ["Trigger"] = "触发", ["Manual Trigger"] = "手动触发", ["Manual Receive Data"] = "手动接收数据",
            ["PLC interaction:"] = "PLC 数据交互：", ["No - local only"] = "否 — 仅本地执行", ["Yes - PLC interaction"] = "是 — 参与 PLC 交互",
            ["Clear Result:"] = "清除结果信号：", ["Position number:"] = "位置号：", ["Job number:"] = "程序号：", ["Product code:"] = "产品码：",
            ["No suppresses PLC output for this operation only; automatic polling continues. Yes uses the normal processing flow. Transport ACK follows actual PLC input edges."] = "“否”仅抑制本次操作的 PLC 输出，不停止现有自动轮询；“是”复用正常流程。驱动底层 ACK 仍跟随实际 PLC 输入边沿，不伪造输入信号。",
            ["Serial"] = "序列号", ["Result"] = "结果", ["Channel"] = "通道", ["CreatedAt"] = "创建时间", ["Payload"] = "原始数据", ["PosN"] = "位置",
            ["Log Details"] = "日志详情", ["TIME"] = "时间", ["LEVEL"] = "级别", ["SOURCE"] = "来源", ["Read-only · Automatic word wrap"] = "只读 · 自动换行",
            ["Reset Count"] = "计数清零", ["Trigger Manual"] = "手动触发", ["Replay"] = "回放", ["Waiting"] = "等待", ["Offline"] = "离线", ["Online"] = "在线",
            ["Camera Offline"] = "相机离线", ["Checking cameras"] = "正在检查相机", ["Communication Error"] = "通讯异常",
            ["User Login"] = "用户登录", ["User Name"] = "用户名", ["User Name:"] = "用户名：", ["Password"] = "密码", ["Change Password"] = "修改密码", ["Register"] = "注册", ["Delete"] = "删除",
            ["Please Select User"] = "请选择用户", ["Account or password error"] = "用户名或密码错误", ["Administrator Certification"] = "管理员验证", ["Register a new user"] = "注册新用户",
            ["Clear All"] = "清空全部", ["Start:"] = "开始：", ["End:"] = "结束：", ["Filter"] = "筛选", ["Page Down"] = "下一页", ["Page Up"] = "上一页", ["Export"] = "导出",
            ["Connect"] = "连接", ["Disconnect"] = "断开", ["Test"] = "测试", ["Browse..."] = "浏览…", ["Test Capture"] = "测试取像", ["Diagnostics"] = "诊断",
            ["Sensor IP"] = "传感器 IP", ["Timeout (ms)"] = "超时（毫秒）", ["SDK provider"] = "SDK 提供方", ["SDK directory"] = "SDK 目录",
            ["Parameters_Config"] = "限值设置", ["FormCamGeneralConfig"] = "相机常规设置", ["Out_put_Parameters"] = "输出参数", ["FormPlcInputParameters"] = "PLC 输入参数",
            ["Changes apply after Save. Existing files and settings are retained."] = "点击保存后生效，已有文件与设置保留。",
            ["Saved. Changes use the existing application save behavior."] = "已保存，设置按原有逻辑生效。",
            ["Unsaved changes — use this page's Save button before switching."] = "有未保存的修改，请先保存再切换分类。",
            ["Error"] = "错误", ["Confirm"] = "确认", ["Unsaved settings"] = "未保存的设置", ["Open settings failed"] = "打开设置失败",
            ["Yes"] = "是", ["No"] = "否",
            ["Delete camera {0} and all of its Pos configurations?\r\nVPP files on disk will be kept. Adding a camera with the same name later will not overwrite existing VPP files."] = "确定删除相机 {0} 及其所有 Pos 配置？\r\n磁盘中的 VPP 文件将保留；之后新增同名相机也不会覆盖已有 VPP。",
            ["Save Successful！"] = "保存成功！", ["Save Successed！"] = "保存成功！", ["Saved successfully！"] = "保存成功！",
            ["Read-only  ·  Automatic word wrap"] = "只读 · 自动换行", ["Copied · Complete original message"] = "已复制完整原始消息",
            ["Clipboard busy. Please try again."] = "剪贴板正忙，请重试。",
            ["Startup - Initialization details"] = "启动 - 初始化详情",
            ["Log Settings"] = "日志设置", ["ALL: visible categories"] = "全部页签：显示类别",
            ["System / Startup"] = "系统 / 启动", ["Camera / Acquisition"] = "相机 / 取像", ["Algorithm / Inspection"] = "算法 / 检测",
            ["This page may contain unsaved changes. Discard them and continue?\r\n\r\nChoose No to return and use the page's Save button."] = "当前页面可能有未保存的修改，是否放弃并继续？\r\n\r\n选择“否”返回页面，点击保存后再切换。"
        };
        internal static string Current { get; private set; } = "en";
        private static bool _started;
        internal static string Normalize(string code) { return code == "zh-CN" ? "zh-CN" : "en"; }
        internal static void Start(string code)
        {
            SetLanguage(code);
            if (_started) return;
            _started = true;
            Application.Idle += (s, e) =>
            {
                foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
                    if (form.GetType().Assembly == typeof(UiLanguage).Assembly && !Entries.TryGetValue(form, out _)) ApplyTo(form);
            };
        }
        internal static void SetLanguage(string code)
        {
            Current = Normalize(code);
            foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
                if (form.GetType().Assembly == typeof(UiLanguage).Assembly) ApplyTo(form);
        }
        internal static string Text(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;
            int spaces = source.Length - source.TrimStart().Length;
            // A single & is a WinForms mnemonic; && is a literal ampersand.
            // Designer captions also frequently contain trailing whitespace.
            string key = System.Text.RegularExpressions.Regex.Replace(source.Trim().Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&"), @"\s+", " ");
            if (Current == "zh-CN" && Chinese.TryGetValue(key, out string value)) return source.Substring(0, spaces) + value;
            if (Current == "zh-CN")
            {
                if (source.StartsWith("From: ", StringComparison.Ordinal)) return "来源：" + source.Substring(6);
                if (source.StartsWith("Protocol:", StringComparison.Ordinal)) return "通讯方式：" + source.Substring(9);
                if (source.StartsWith("Loading page ", StringComparison.Ordinal)) return "正在加载第 " + source.Substring(13) + " 页";
                if (source.StartsWith("Page ", StringComparison.Ordinal)) return "第 " + source.Substring(5) + " 页";
                if (source.StartsWith("Cameras:", StringComparison.Ordinal)) return source.Replace("Cameras:", "相机数量：").Replace("Layout:", "实际行列：");
                if (source.Contains("Current operation:")) return source.Replace("Total:", "总耗时：").Replace("Current operation:", "当前步骤：");
                foreach (var prefix in new[] { new[] { "Total:", "总数：" }, new[] { "Pass:", "合格：" }, new[] { "PassRate:", "合格率：" } })
                    if (source.StartsWith(prefix[0], StringComparison.Ordinal)) return prefix[1] + source.Substring(prefix[0].Length);
                if (source.EndsWith(" events", StringComparison.Ordinal)) return source.Substring(0, source.Length - 7) + " 条日志";
            }
            foreach (string protocol in new[] { "S7", "Profinet", "TCP/IP", "TCPIP" })
                foreach (string state in new[] { "Online", "Offline", "Error" })
                    if (key == protocol + " " + state) return protocol + " " + (Current == "zh-CN" ? state == "Online" ? "在线" : state == "Offline" ? "离线" : "异常" : state);
            return source;
        }
        internal static void ApplyTo(Control control)
        {
            if (control == null || control.IsDisposed || (control.GetType().Namespace ?? "").StartsWith("Cognex.", StringComparison.Ordinal)) return;
            bool first = !Entries.TryGetValue(control, out var entry);
            if (first) entry = Entries.GetValue(control, c => new Entry { Source = control.Text });
            bool caption = control is Label || control is Button || control is CheckBox || control is RadioButton || control is GroupBox || control is TabPage || (control is Form && !(control is FormMain));
            if (caption)
            {
                Translate(control, entry);
                // These captions are painted separately from the original bound
                // statistics Text; refresh even when its numeric text is unchanged.
                if (control is InspectionMetricLabel) control.Invalidate();
                if (first) control.TextChanged += (s, e) => { if (!entry.Applying) { entry.Source = control.Text; Translate(control, entry); } };
            }
            if (first) control.ControlAdded += (s, e) => ApplyTo(e.Control);
            var strip = control as ToolStrip;
            if (strip != null && !(strip is StatusStrip))
            {
                foreach (ToolStripItem item in strip.Items) ApplyItem(item);
                if (first) strip.ItemAdded += (s, e) => ApplyItem(e.Item);
            }
            var grid = control as DataGridView;
            if (grid != null)
            {
                foreach (DataGridViewColumn column in grid.Columns) ApplyColumn(column);
                if (first) grid.ColumnAdded += (s, e) => ApplyColumn(e.Column);
                return;
            }
            control.SuspendLayout();
            try { foreach (Control child in control.Controls) ApplyTo(child); }
            finally { control.ResumeLayout(true); }
        }
        private static void Translate(Control control, Entry entry)
        {
            entry.Applying = true;
            try { string value = Text(entry.Source); if (value != control.Text) control.Text = value; }
            finally { entry.Applying = false; }
        }
        private static void ApplyItem(ToolStripItem item)
        {
            bool first = !Entries.TryGetValue(item, out var entry);
            if (first) entry = Entries.GetValue(item, i => new Entry { Source = item.Text });
            Action update = () => { entry.Applying = true; try { item.Text = Text(entry.Source); } finally { entry.Applying = false; } };
            update();
            if (first) item.TextChanged += (s, e) => { if (!entry.Applying) { entry.Source = item.Text; update(); } };
            var menu = item as ToolStripMenuItem;
            if (menu == null) return;
            foreach (ToolStripItem child in menu.DropDownItems) ApplyItem(child);
            if (first) menu.DropDown.ItemAdded += (s, e) => ApplyItem(e.Item);
        }
        private static void ApplyColumn(DataGridViewColumn column)
        {
            var entry = Entries.GetValue(column, c => new Entry { Source = column.HeaderText });
            column.HeaderText = Text(entry.Source);
        }
    }
}
