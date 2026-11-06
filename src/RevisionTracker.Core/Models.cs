using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace RevisionTracker.Core
{
    /// <summary>Root of everything the app stores (except file contents, which live in the object store).</summary>
    [XmlRoot("RevisionTracker")]
    public class CatalogData
    {
        public int SchemaVersion = 1;
        public List<Project> Projects = new List<Project>();
        public Categories Categories = Categories.CreateDefault();
    }

    /// <summary>A project / release, e.g. "VW_Series-04_VS". One sheet in the old Excel.</summary>
    public class Project
    {
        public Guid Id = Guid.NewGuid();
        public string Name = "";
        public string Notes = "";
        public DateTime CreatedAt = DateTime.Now;
        public List<Part> Parts = new List<Part>();
    }

    /// <summary>A board assembly inside a project, e.g. "Logic PCBA F00M713026 rev 3".</summary>
    public class Part
    {
        public Guid Id = Guid.NewGuid();
        public string Board = "Logic";      // Logic / Power / Panel
        public string Kind = "PCBA";        // PCBA / PCB
        public string Ttn = "";
        public int Rev = 1;
        public string Variant = "";         // e.g. C2_VAR04
        public string Notes = "";
        public List<TrackedDocument> Documents = new List<TrackedDocument>();

        [XmlIgnore] public string DisplayName
        {
            get
            {
                var s = Board + " " + Kind;
                if (!string.IsNullOrWhiteSpace(Ttn)) s += "  " + Ttn + "-" + Rev.ToString("00");
                return s;
            }
        }
    }

    /// <summary>A document (schematic, eBOM, gerber...) whose versions we keep.</summary>
    public class TrackedDocument
    {
        public Guid Id = Guid.NewGuid();
        public string DocType = "";
        public string Ttn = "";
        public int Rev = 1;
        public string Tag = "";
        public string WcType = "";
        public string WcName = "";
        public string Suffix = "";
        public bool UploadedToWc;
        public string Notes = "";
        public List<DocVersion> Versions = new List<DocVersion>();

        [XmlIgnore] public DocVersion Latest
        {
            get { return Versions.Count == 0 ? null : Versions.OrderByDescending(v => v.Number).First(); }
        }

        /// <summary>Highest version number ever issued, so numbers are never reused after a delete.</summary>
        public int LastVersionNumber;

        public int NextVersionNumber()
        {
            var max = Versions.Count == 0 ? 0 : Versions.Max(v => v.Number);
            LastVersionNumber = Math.Max(LastVersionNumber, max) + 1;
            return LastVersionNumber;
        }
    }

    /// <summary>One saved snapshot of a document's file.</summary>
    public class DocVersion
    {
        public Guid Id = Guid.NewGuid();
        public int Number;
        public DateTime SavedAt = DateTime.Now;
        public int Rev;
        public string Tag = "";
        public string Label = "";            // "Sent to client", "Approved"...
        public string Note = "";
        public string OriginalFileName = "";
        public string OriginalPath = "";
        public long Size;
        public string Hash = "";             // SHA-256 of the file contents (object store key)
    }

    public class DocTypeDef
    {
        public string Name = "";
        public string FilePrefix = "";
        public string DefaultWcType = "";
        public string DefaultSuffix = "";
        public string AppliesTo = "";        // "PCBA", "PCB" or "" for any
        public bool UsesPartTtn;             // FabDraw uses the board's own TTN
        public bool AddByDefault = true;     // created automatically for every new board of this type

        public override string ToString() { return Name; }
    }

    /// <summary>User-editable lists that drive the drop-downs. Defaults come from the Windchill tracking Excel.</summary>
    public class Categories
    {
        public List<DocTypeDef> DocTypes = new List<DocTypeDef>();
        public List<string> WcTypes = new List<string>();
        public List<string> Boards = new List<string>();
        public List<string> PartKinds = new List<string>();
        public List<string> Labels = new List<string>();

        public DocTypeDef Find(string name)
        {
            return DocTypes.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public static Categories CreateDefault()
        {
            var c = new Categories();
            c.WcTypes.AddRange(new[] { "Gen.D.Spec.", "Man.Inf.", "SW. Doc." });
            c.Boards.AddRange(new[] { "Logic", "Power", "Panel" });
            c.PartKinds.AddRange(new[] { "PCBA", "PCB" });
            c.Labels.AddRange(new[] { "Sent to client", "Approved", "Uploaded to Windchill", "Released", "Work in progress" });

            Action<string, string, string, string, string, bool> add = (name, prefix, wc, suffix, applies, partTtn) =>
                c.DocTypes.Add(new DocTypeDef { Name = name, FilePrefix = prefix, DefaultWcType = wc, DefaultSuffix = suffix, AppliesTo = applies, UsesPartTtn = partTtn });
            Action<string> optional = name => c.DocTypes.Last().AddByDefault = false;

            add("3D Model", "3D", "", "_no_copper", "PCBA", false);
            add("FabDraw (PCBA)", "Fab_Draw", "Gen.D.Spec.", "", "PCBA", true);
            add("Schematic", "Schematic", "Gen.D.Spec.", "", "PCBA", false);
            add("eBOM", "eBOM", "Man.Inf.", "_combined", "PCBA", false);
            add("Pick&Place", "Pick_Place", "Gen.D.Spec.", "", "PCBA", false);
            add("ASYview", "Assy_View", "Gen.D.Spec.", "", "PCBA", false);
            optional("ASYview");
            add("Layoutview", "Layout_View", "Gen.D.Spec.", "", "PCBA", false);
            optional("Layoutview");
            add("Test Points", "Test_Points", "", "", "PCBA", false);
            optional("Test Points");
            add("FabDraw (PCB)", "Fab_Draw", "Man.Inf.", "", "PCB", true);
            add("Gerber Output", "Gerber_Output", "SW. Doc.", "", "PCB", false);
            add("ODB Output", "ODB_Output", "SW. Doc.", "", "PCB", false);
            add("Netlist", "Netlist", "SW. Doc.", "", "PCB", false);
            return c;
        }
    }
}
