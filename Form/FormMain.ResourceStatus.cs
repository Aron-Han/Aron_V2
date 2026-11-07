using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aron_V2
{
    // Two native snapshots per second; no performance-counter initialization,
    // process enumeration, hardware access or communication dependencies.
    internal sealed class SystemResourceSampler
    {
        [StructLayout(LayoutKind.Sequential)] private struct FileTime
        {
            internal uint Low, High;
            internal ulong Ticks => ((ulong)High << 32) | Low;
        }
        [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
        {
            internal uint Length, Load;
            internal ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
        private ulong _idle, _total;
        private bool _sampled;
        internal void Read(out double? memoryPercent, out double? cpuPercent)
        {
            memoryPercent = cpuPercent = null;
            try
            {
                var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf(typeof(MemoryStatus)) };
                if (GlobalMemoryStatusEx(ref memory) && memory.TotalPhysical > 0)
                    memoryPercent = Math.Max(0, Math.Min(100, 100d * (memory.TotalPhysical - memory.AvailablePhysical) / memory.TotalPhysical));
                FileTime idle, kernel, user;
                if (!GetSystemTimes(out idle, out kernel, out user)) return;
                ulong total = kernel.Ticks + user.Ticks;
                if (_sampled && total > _total && idle.Ticks >= _idle)
                {
                    double elapsed = total - _total, idleElapsed = idle.Ticks - _idle;
                    cpuPercent = Math.Max(0, Math.Min(100, (elapsed - idleElapsed) * 100 / elapsed));
                }
                _idle = idle.Ticks; _total = total; _sampled = true;
            }
            catch (Exception ex) { System.Diagnostics.Trace.WriteLine("System resource status: " + ex.Message); }
        }
    }

    public partial class FormMain
    {
        private void InitializeResourceStatus()
        {
            var sampler = new SystemResourceSampler();
            var memory = new ToolStripStatusLabel { Name = "memoryStatusLabel" };
            var cpu = new ToolStripStatusLabel { Name = "cpuStatusLabel" };
            float scale = DesktopTheme.DisplayScale;
            statusStrip1.ShowItemToolTips = true;
            foreach (var item in new[] { memory, cpu })
            {
                item.AutoSize = false;
                item.Font = statusStrip1.Font;
                item.Width = (int)(160 * scale);
                item.Height = Math.Max((int)(22 * scale), item.Font.Height + (int)(6 * scale));
                item.Padding = new Padding((int)(12 * scale), 0, (int)(12 * scale), 0);
                item.Margin = new Padding(0, (int)(2 * scale), (int)(8 * scale), (int)(2 * scale));
                item.TextAlign = ContentAlignment.MiddleLeft;
                item.ForeColor = DesktopTheme.Ink;
                statusStrip1.Items.Add(item);
            }
            statusStrip1.Paint += (s, e) => {
                using (var pen = new Pen(DesktopTheme.Border))
                    foreach (var item in new[] { memory, cpu })
                        if (item.Available && item.Bounds.Width > 0)
                            e.Graphics.DrawLine(pen, item.Bounds.Left, (int)(6 * scale), item.Bounds.Left, statusStrip1.Height - (int)(6 * scale));
            };
            Action refresh = () => {
                double? memoryValue, cpuValue;
                sampler.Read(out memoryValue, out cpuValue);
                bool chinese = UiLanguage.Current == "zh-CN";
                memory.Text = (chinese ? "内存：" : "Memory: ") + (memoryValue.HasValue ? memoryValue.Value.ToString("F1") + "%" : "--");
                cpu.Text = "CPU: " + (cpuValue.HasValue ? cpuValue.Value.ToString("F1") + "%" : "--");
                memory.ToolTipText = chinese ? "整台电脑的物理内存占用率，每秒刷新" : "System physical memory usage; refreshed every second";
                cpu.ToolTipText = chinese ? "整台电脑的 CPU 占用率，每秒刷新" : "System CPU usage; refreshed every second";
            };
            var timer = new Timer { Interval = 1000 };
            timer.Tick += (s, e) => refresh();
            refresh(); timer.Start();
            Disposed += (s, e) => timer.Dispose();
        }
    }
}
