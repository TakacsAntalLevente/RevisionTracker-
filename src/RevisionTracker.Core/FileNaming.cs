using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RevisionTracker.Core
{
    /// <summary>
    /// Builds the standard file and Windchill names the old Excel generated with formulas, e.g.
    /// Schematic_LogicPCBA_C2_VAR04_0127Y05196-02_e16.07
    /// </summary>
    public static class FileNaming
    {
        /// <summary>"FabDraw (PCBA)" -> "FabDraw"</summary>
        public static string BaseTypeName(string docType)
        {
            if (string.IsNullOrEmpty(docType)) return "";
            return Regex.Replace(docType, @"\s*\(.*\)\s*$", "").Trim();
        }

        public static string StandardName(Part part, TrackedDocument doc, Categories categories)
        {
            var def = categories.Find(doc.DocType);
            var prefix = def != null && !string.IsNullOrWhiteSpace(def.FilePrefix)
                ? def.FilePrefix
                : BaseTypeName(doc.DocType).Replace(' ', '_').Replace("&", "_");

            var parts = new System.Collections.Generic.List<string> { prefix, (part.Board ?? "") + (part.Kind ?? "") };
            if (!string.IsNullOrWhiteSpace(part.Variant)) parts.Add(part.Variant.Trim());
            var ttn = string.IsNullOrWhiteSpace(doc.Ttn) ? "xxxxxxxxxx" : doc.Ttn.Trim();
            parts.Add(ttn + "-" + doc.Rev.ToString("00"));
            if (!string.IsNullOrWhiteSpace(doc.Tag) && doc.Tag.Trim() != "-") parts.Add(doc.Tag.Trim());

            var name = string.Join("_", parts.Where(p => !string.IsNullOrEmpty(p)).ToArray()) + (doc.Suffix ?? "").Trim();
            return Sanitize(name);
        }

        public static string WcName(Part part, string docType)
        {
            return (BaseTypeName(docType) + " " + part.Board + " " + part.Kind).Trim();
        }

        public static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
