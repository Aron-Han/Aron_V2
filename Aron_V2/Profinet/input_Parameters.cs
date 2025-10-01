using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2.Profinet
{
    public static class input_Parameters
    {
        public const int IdxClrCh0 = 0;   // '0'/'1'
        public const int IdxClrCh1 = 1;
        public const int IdxClrCh2 = 2;
        public const int IdxClrCh3 = 3;

        public const int IdxJobCh0 = 4;   // '1'..'9'（你用一位表示Job号）
        public const int IdxJobCh1 = 5;
        public const int IdxJobCh2 = 6;
        public const int IdxJobCh3 = 7;

        public const int IdxPosCh0 = 8;   // '1'..'9'
        public const int IdxPosCh1 = 9;
        public const int IdxPosCh2 = 10;
        public const int IdxPosCh3 = 11;

        // 你原来写的 PartCode 起始不一致，这里先保留
        public const int IdxPartCh0 = 12;  // 长度4
        public const int IdxPartCh1 = 52;  // 长度4
        public const int IdxPartCh2 = 52;  // 长度4
        public const int IdxPartCh3 = 52;  // 长度4
        public const int PartLen = 4;

        public const int MinNeededLen = 13 + 1; // 最少要覆盖到 12 索引 +1

        public static int CharDigitAt(byte[] s, int idx)
        {
            int c = s[idx];
            return c;
        }

        public static string SafeSlice(byte[] s, int idx, int len)
        {
            int take = Math.Min(len, s.Length - idx);
            string part = "";
            for (int i = 0; i < take; i++)
            {
                part += s[idx + i].ToString();
            }
            return (take > 0) ? part : "";
        }

        public static void ClearResultBufferByConfig(int channel)
        {
            // 当前 Job（字符串形如 "Job1"）
            var job = Global.Model_JobID[channel];
            if (string.IsNullOrEmpty(job)) return;

            // 读取/缓存你保存的输出配置
            var cfgPath = string.IsNullOrEmpty(Global.VppOutputCfgPath)
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VppOutput.xml")
                : Global.VppOutputCfgPath;

            var cfg = XmlConfigHelper.LoadVppOutput(cfgPath);
            if (cfg == null || cfg.Jobs == null) return;

            var j = cfg.Jobs.FirstOrDefault(z => string.Equals(z.Name, job, StringComparison.OrdinalIgnoreCase));
            if (j == null || j.Cameras == null) return;

            // 汇总这个 Job 下本 channel 的所有段
            var segs = new List<Tuple<int, int>>(); // (start, length)
            foreach (var cam in j.Cameras)
            {
                if (cam == null || cam.VPPOutput == null) continue;
                foreach (var it in cam.VPPOutput)
                {
                    if (it == null) continue;
                    if (it.Channel != channel) continue;
                    if (it.Start < 0 || it.Length <= 0) continue;
                    segs.Add(Tuple.Create(it.Start, it.Length));
                }
            }

            // 真正清零
            lock (Global.PlcBufferLock)
            {
                foreach (var s in segs)
                {
                    int start = s.Item1;
                    int len = s.Item2;
                    if (start >= Global.Result_Send.Length) continue;
                    if (start + len > Global.Result_Send.Length) len = Global.Result_Send.Length - start;
                    for (int k = 0; k < len; k++) Global.Result_Send[start + k] = 0;
                }
            }


        }

        public static byte ToDigit(string oneDigit)
        {
            if (string.IsNullOrEmpty(oneDigit)) return 0;
            char c = oneDigit[0];
            return (byte)((c >= '0' && c <= '9') ? (c - '0') : 0);
        }
    }
    public static class PlcEchoRegion
    {
        // 约定：0..7 为保留回显区
        public const int IdxJobCh0 = 0;   // 一个字符 '0'..'9'
        public const int IdxJobCh1 = 1;
        public const int IdxJobCh2 = 2;
        public const int IdxJobCh3 = 3;

        public const int IdxPosCh0 = 4;   // 一个字符 '0'..'9'
        public const int IdxPosCh1 = 5;
        public const int IdxPosCh2 = 6;
        public const int IdxPosCh3 = 7;

        // 如需回显“清除信号”，可以再约定其它字节（例如 8..11），
        // 或者把清除信号编码进同一字节的 bit 位，这里给出字节方案：
        public const int IdxClrCh0 = 8;   // 0/1
        public const int IdxClrCh1 = 9;
        public const int IdxClrCh2 = 10;
        public const int IdxClrCh3 = 11;
    }
}
