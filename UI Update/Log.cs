using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aron_V2
{
	public static class Log
	{   
        public static void AppendColoredText(System.Windows.Forms.RichTextBox box, string text, Color color)
        {
            string line = $"{DateTime.Now:HH:mm:ss.fff} {text}";

            if (box.InvokeRequired)
            {
                box.Invoke(new Action(() =>
                {
                    AppendTextToRichTextBox(box, line, color); // 调用执行追加的方法
                }));
            }
            else
            {
                AppendTextToRichTextBox(box, line, color); // 当前线程直接执行
            }
        }

        private static void AppendTextToRichTextBox(System.Windows.Forms.RichTextBox box, string line, Color color)
        {
            box.SelectionStart = box.TextLength;   
            box.SelectionLength = 0;               
            box.SelectionColor = color;          
            box.AppendText(line + Environment.NewLine); 

            // 删除超出最大行数的内容
            if (box.Lines.Length > Global.maxLines_Richbox)
            {
                int excess = box.Lines.Length - Global.maxLines_Richbox;
                box.Clear();
                // 删除多余行
                box.Lines = box.Lines.Skip(excess).ToArray();
               
            }
            box.SelectionColor = box.ForeColor;    
            box.SelectionStart = box.Text.Length;  
            box.ScrollToCaret();                   
        }
    }
	
}

