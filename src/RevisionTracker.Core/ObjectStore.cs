using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace RevisionTracker.Core
{
    /// <summary>
    /// Content-addressed file store. Each unique file content is stored once, gzip-compressed,
    /// at objects/ab/abcdef....gz where the name is the SHA-256 of the uncompressed content.
    /// Saving the same content twice costs nothing extra.
    /// </summary>
    public class ObjectStore
    {
        private readonly string _root;

        public ObjectStore(string root)
        {
            _root = Path.Combine(root, "objects");
            Directory.CreateDirectory(_root);
        }

        public string PathFor(string hash)
        {
            return Path.Combine(_root, hash.Substring(0, 2), hash + ".gz");
        }

        public bool Contains(string hash)
        {
            return File.Exists(PathFor(hash));
        }

        /// <summary>Hash a file without storing it.</summary>
        public static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = OpenShared(path))
                return ToHex(sha.ComputeHash(fs));
        }

        /// <summary>Store a file. Returns its hash and sets size. Works even if the file is open in Excel/Word.</summary>
        public string Put(string sourcePath, out long size)
        {
            var tmp = Path.Combine(_root, "tmp-" + Guid.NewGuid().ToString("N"));
            string hash;
            try
            {
                using (var sha = SHA256.Create())
                {
                    using (var input = OpenShared(sourcePath))
                    using (var output = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write))
                    using (var gz = new GZipStream(output, CompressionLevel.Optimal))
                    {
                        var buffer = new byte[81920];
                        int read;
                        size = 0;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sha.TransformBlock(buffer, 0, read, null, 0);
                            gz.Write(buffer, 0, read);
                            size += read;
                        }
                        sha.TransformFinalBlock(new byte[0], 0, 0);
                    }
                    hash = ToHex(sha.Hash);
                }

                var dest = PathFor(hash);
                if (File.Exists(dest))
                {
                    File.Delete(tmp); // already stored: deduplicated
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Move(tmp, dest);
                }
                return hash;
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }

        /// <summary>Write stored content to destPath, verifying integrity.</summary>
        public void Extract(string hash, string destPath)
        {
            var src = PathFor(hash);
            if (!File.Exists(src))
                throw new FileNotFoundException("The stored content for this version is missing from the data folder.", src);

            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = destPath + ".rtmp";
            using (var sha = SHA256.Create())
            {
                using (var input = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var gz = new GZipStream(input, CompressionMode.Decompress))
                using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = gz.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        sha.TransformBlock(buffer, 0, read, null, 0);
                        output.Write(buffer, 0, read);
                    }
                    sha.TransformFinalBlock(new byte[0], 0, 0);
                }
                if (!string.Equals(ToHex(sha.Hash), hash, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(tmp);
                    throw new InvalidDataException("Stored content is damaged (checksum mismatch).");
                }
            }
            if (File.Exists(destPath))
            {
                File.SetAttributes(destPath, FileAttributes.Normal);
                File.Delete(destPath);
            }
            File.Move(tmp, destPath);
        }

        /// <summary>Delete objects that no version references any more.</summary>
        public int RemoveUnreferenced(ISet<string> referenced)
        {
            int removed = 0;
            foreach (var file in Directory.GetFiles(_root, "*.gz", SearchOption.AllDirectories))
            {
                var hash = Path.GetFileNameWithoutExtension(file);
                if (!referenced.Contains(hash))
                {
                    try { File.Delete(file); removed++; } catch (IOException) { }
                }
            }
            return removed;
        }

        public long TotalStoredBytes()
        {
            long total = 0;
            foreach (var file in Directory.GetFiles(_root, "*.gz", SearchOption.AllDirectories))
                total += new FileInfo(file).Length;
            return total;
        }

        private static FileStream OpenShared(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
