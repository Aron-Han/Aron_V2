using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Aron_V2
{
    internal sealed class HelpTopic
    {
        internal string Id, Module, Title, Body;
        internal bool Matches(string query)
        {
            string text = Title + "\n" + Body;
            return (query ?? "").Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .All(word => text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
    internal static class HelpContent
    {
        internal static List<HelpTopic> Load()
        {
            var topics = new List<HelpTopic>();
            foreach (string module in new[] { "SoftwareOperation", "Communication" })
            {
                string resource = "Aron_V2.Help." + module + ".zh-CN.md";
                using (var stream = typeof(HelpContent).Assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) throw new InvalidOperationException("Help resource missing: " + resource);
                    using (var reader = new StreamReader(stream, Encoding.UTF8)) topics.AddRange(Parse(module, reader.ReadToEnd()));
                }
            }
            return topics;
        }
        internal static IEnumerable<HelpTopic> Parse(string module, string markdown)
        {
            HelpTopic current = null; var body = new StringBuilder(); int index = 0;
            foreach (string line in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    if (current != null) { current.Body = body.ToString().Trim(); yield return current; }
                    current = new HelpTopic { Id = module + ":" + index++, Module = module, Title = line.Substring(3).Trim() };
                    body.Clear();
                }
                else if (current != null) body.AppendLine(line);
            }
            if (current != null) { current.Body = body.ToString().Trim(); yield return current; }
        }
    }
}
