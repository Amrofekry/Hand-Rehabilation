using System;
using System.IO;
using System.Text;
using GripRehab.Product;
using UnityEngine;

namespace GripRehab
{
    /// <summary>Separate encrypted catalog; legacy profiles remain intact and are linked by ID.</summary>
    public sealed class WindowsProductStore : IProductPersistence
    {
        readonly string path;
        public WindowsProductStore(string directory) { path = Path.Combine(directory, "research-accounts.dat"); }
        public LocalProductCatalog Load()
        {
            // A busy primary is retried, never rolled back to the older .bak (audit A-020).
            return ProtectedFileRecovery.Load(path, Read) ?? new LocalProductCatalog();
        }
        LocalProductCatalog Read(string file)
        {
            if (new FileInfo(file).Length > 16 * 1024 * 1024) throw new InvalidDataException("Account catalog is too large.");
            var clear = WindowsProfileProtection.Unprotect(File.ReadAllBytes(file));
            try { var result = JsonUtility.FromJson<LocalProductCatalog>(Encoding.UTF8.GetString(clear)); ProductRepository.Validate(result); return result; }
            finally { Array.Clear(clear, 0, clear.Length); }
        }
        public LocalProductCatalog Clone(LocalProductCatalog source) => JsonUtility.FromJson<LocalProductCatalog>(JsonUtility.ToJson(source));
        public void Save(LocalProductCatalog catalog)
        {
            ProductRepository.Validate(catalog); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string expected = JsonUtility.ToJson(catalog); var clear = Encoding.UTF8.GetBytes(expected);
            if (clear.Length > 15 * 1024 * 1024) throw new IOException("Account storage limit reached. Export completed sessions before continuing.");
            byte[] encrypted;
            try { encrypted = WindowsProfileProtection.Protect(clear); } finally { Array.Clear(clear, 0, clear.Length); }
            string temporary = path + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { file.Write(encrypted, 0, encrypted.Length); file.Flush(true); }
                // Validate the parsed catalog, then compare decrypted persisted bytes to
                // the exact JSON we encrypted. Re-serializing floats can change a decimal
                // representation by one ULP and falsely reject a correct atomic save.
                Read(temporary);
                var restored = WindowsProfileProtection.Unprotect(File.ReadAllBytes(temporary));
                try { if (Encoding.UTF8.GetString(restored) != expected) throw new IOException("Account save did not pass exact-content read-back."); }
                finally { Array.Clear(restored, 0, restored.Length); }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else { File.Move(temporary, path); File.Copy(path, path + ".bak", true); }
            }
            finally { Array.Clear(encrypted, 0, encrypted.Length); }
        }
    }
}
