using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace RevisionTracker.Core
{
    public class VersionLimitException : Exception
    {
        public VersionLimitException(string message) : base(message) { }
    }

    /// <summary>
    /// All data lives in one folder: catalog.xml (projects, documents, version history) plus objects/ (file contents).
    /// Every change is saved immediately with an atomic write, and the previous catalog is kept as catalog.bak.xml.
    /// </summary>
    public class Repository : IDisposable
    {
        public string Root { get; private set; }
        public CatalogData Data { get; private set; }
        public ObjectStore Objects { get; private set; }

        private readonly string _catalogPath;
        private readonly string _backupPath;
        private FileStream _lock;
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(CatalogData));

        public static string DefaultRoot
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Edition.ProductName);
            }
        }

        public Repository(string root)
        {
            Root = root;
            Directory.CreateDirectory(root);
            _catalogPath = Path.Combine(root, "catalog.xml");
            _backupPath = Path.Combine(root, "catalog.bak.xml");

            try
            {
                _lock = new FileStream(Path.Combine(root, ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                throw new InvalidOperationException("RevisionTracker is already running (the data folder is in use).");
            }

            Objects = new ObjectStore(root);
            Data = Load();
        }

        private CatalogData Load()
        {
            if (!File.Exists(_catalogPath))
            {
                if (File.Exists(_backupPath)) return Read(_backupPath);
                return new CatalogData();
            }
            try
            {
                return Read(_catalogPath);
            }
            catch (InvalidOperationException)
            {
                // Damaged catalog: fall back to the previous save.
                if (File.Exists(_backupPath)) return Read(_backupPath);
                throw;
            }
        }

        private static CatalogData Read(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                var data = (CatalogData)Serializer.Deserialize(fs);
                if (data.Categories == null || data.Categories.DocTypes.Count == 0) data.Categories = Categories.CreateDefault();
                return data;
            }
        }

        public void Save()
        {
            var tmp = _catalogPath + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                Serializer.Serialize(fs, Data);

            if (File.Exists(_catalogPath))
            {
                if (File.Exists(_backupPath)) File.Delete(_backupPath);
                File.Move(_catalogPath, _backupPath);
            }
            File.Move(tmp, _catalogPath);
        }

        // ---------- lookups ----------

        public Part FindPartOf(TrackedDocument doc)
        {
            return Data.Projects.SelectMany(p => p.Parts).FirstOrDefault(p => p.Documents.Contains(doc));
        }

        public Project FindProjectOf(Part part)
        {
            return Data.Projects.FirstOrDefault(p => p.Parts.Contains(part));
        }

        // ---------- versions ----------

        /// <summary>The version with identical content, if the file is unchanged since a saved version.</summary>
        public DocVersion FindIdenticalVersion(TrackedDocument doc, string filePath)
        {
            var hash = ObjectStore.HashFile(filePath);
            return doc.Versions.Where(v => v.Hash == hash).OrderByDescending(v => v.Number).FirstOrDefault();
        }

        public bool IsAtVersionLimit(TrackedDocument doc)
        {
            return Edition.MaxVersionsPerDocument > 0 && doc.Versions.Count >= Edition.MaxVersionsPerDocument;
        }

        public DocVersion AddVersion(TrackedDocument doc, string filePath, int rev, string tag, string label, string note)
        {
            if (IsAtVersionLimit(doc))
                throw new VersionLimitException(string.Format(
                    "The {0} edition keeps up to {1} versions per document. Delete an old version to save a new one.",
                    Edition.Name, Edition.MaxVersionsPerDocument));

            long size;
            var hash = Objects.Put(filePath, out size);
            var v = new DocVersion
            {
                Number = doc.NextVersionNumber(),
                SavedAt = DateTime.Now,
                Rev = rev,
                Tag = tag ?? "",
                Label = label ?? "",
                Note = note ?? "",
                OriginalFileName = Path.GetFileName(filePath),
                OriginalPath = Path.GetFullPath(filePath),
                Size = size,
                Hash = hash
            };
            doc.Versions.Add(v);
            doc.Rev = rev;
            doc.Tag = tag ?? "";
            Save();
            return v;
        }

        /// <summary>Copy a version's file to any path.</summary>
        public void ExportVersion(DocVersion version, string destPath)
        {
            Objects.Extract(version.Hash, destPath);
        }

        /// <summary>
        /// Put an old version back at its original location. If the file there has changes that were never saved,
        /// they are saved as a new version first so nothing is lost. Returns that safety version, or null.
        /// </summary>
        public DocVersion RestoreToOriginal(TrackedDocument doc, DocVersion version)
        {
            DocVersion safety = null;
            var path = version.OriginalPath;
            if (File.Exists(path))
            {
                var currentHash = ObjectStore.HashFile(path);
                if (currentHash == version.Hash) return null; // already identical
                if (!doc.Versions.Any(v => v.Hash == currentHash))
                {
                    long size;
                    var hash = Objects.Put(path, out size);
                    safety = new DocVersion
                    {
                        Number = doc.NextVersionNumber(), SavedAt = DateTime.Now, Rev = doc.Rev, Tag = doc.Tag,
                        Label = "Auto-saved before restore", OriginalFileName = Path.GetFileName(path),
                        OriginalPath = path, Size = size, Hash = hash
                    };
                    doc.Versions.Add(safety); // allowed even past the free limit: never lose user data
                    Save();
                }
            }
            Objects.Extract(version.Hash, path);
            return safety;
        }

        /// <summary>Extract to a read-only temp copy for viewing.</summary>
        public string ExtractForViewing(DocVersion version)
        {
            var dir = Path.Combine(Path.GetTempPath(), Edition.ProductName, version.Id.ToString("N"));
            var dest = Path.Combine(dir, string.IsNullOrEmpty(version.OriginalFileName) ? "file" : version.OriginalFileName);
            if (!File.Exists(dest))
            {
                Objects.Extract(version.Hash, dest);
                File.SetAttributes(dest, FileAttributes.ReadOnly);
            }
            return dest;
        }

        public void DeleteVersion(TrackedDocument doc, DocVersion version)
        {
            doc.Versions.Remove(version);
            Save();
            CollectGarbage();
        }

        // ---------- structure ----------

        public void DeleteProject(Project p) { Data.Projects.Remove(p); Save(); CollectGarbage(); }

        public void DeletePart(Part part)
        {
            var proj = FindProjectOf(part);
            if (proj != null) proj.Parts.Remove(part);
            Save(); CollectGarbage();
        }

        public void DeleteDocument(TrackedDocument doc)
        {
            var part = FindPartOf(doc);
            if (part != null) part.Documents.Remove(doc);
            Save(); CollectGarbage();
        }

        public int CollectGarbage()
        {
            var used = new HashSet<string>(Data.Projects.SelectMany(p => p.Parts).SelectMany(p => p.Documents)
                .SelectMany(d => d.Versions).Select(v => v.Hash), StringComparer.OrdinalIgnoreCase);
            return Objects.RemoveUnreferenced(used);
        }

        public void Dispose()
        {
            if (_lock != null) { _lock.Dispose(); _lock = null; }
        }
    }
}
