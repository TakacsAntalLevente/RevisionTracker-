using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RevisionTracker.Core
{
    /// <summary>Exports the tracking table in the same column layout as the old Excel (opens directly in Excel).</summary>
    public static class CsvExporter
    {
        public static void Export(IEnumerable<Project> projects, Categories categories, string path)
        {
            var sep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
            if (string.IsNullOrEmpty(sep)) sep = ",";
            var sb = new StringBuilder();
            sb.AppendLine("sep=" + sep);
            Row(sb, sep, "Project", "Filename", "TTN", "REV", "TAG", "file", "WC", "WC type", "WC name", "Versions", "Last saved", "Last label", "Notes");

            foreach (var proj in projects)
            {
                foreach (var part in proj.Parts)
                {
                    Row(sb, sep, proj.Name, part.Kind, part.Ttn, part.Rev.ToString(), "-", "", "", "", part.Board + " " + part.Kind, "", "", "", part.Notes);
                    foreach (var d in part.Documents)
                    {
                        var latest = d.Latest;
                        Row(sb, sep, proj.Name, FileNaming.StandardName(part, d, categories), d.Ttn, d.Rev.ToString(), d.Tag,
                            d.Versions.Count > 0 ? "x" : "", d.UploadedToWc ? "x" : "", d.WcType, d.WcName,
                            d.Versions.Count.ToString(),
                            latest == null ? "" : latest.SavedAt.ToString("yyyy-MM-dd HH:mm"),
                            latest == null ? "" : latest.Label, d.Notes);
                    }
                    Row(sb, sep);
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        private static void Row(StringBuilder sb, string sep, params string[] cells)
        {
            sb.AppendLine(string.Join(sep, cells.Select(c => Escape(c ?? "", sep)).ToArray()));
        }

        private static string Escape(string s, string sep)
        {
            if (s.Contains(sep) || s.Contains("\"") || s.Contains("\n") || s.Contains("\r"))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
