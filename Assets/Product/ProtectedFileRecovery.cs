using System;
using System.IO;
using System.Threading;

namespace GripRehab.Product
{
    /// <summary>
    /// Loads a protected primary file with its ".bak" sibling. A busy primary (sharing
    /// violation, scanner lock) is retried and never replaced by the older backup. Only a
    /// missing or unreadable primary falls back, and an unreadable primary is copied aside
    /// first so no bytes are destroyed.
    /// </summary>
    public static class ProtectedFileRecovery
    {
        const int Attempts = 4;

        /// <returns>The loaded value, or default when neither copy exists.</returns>
        public static T Load<T>(string path, Func<string, T> read, Action<int> wait = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A protected file path is required.", nameof(path));
            if (read == null) throw new ArgumentNullException(nameof(read));
            wait = wait ?? (milliseconds => Thread.Sleep(milliseconds));
            var backup = path + ".bak";
            bool hasPrimary = File.Exists(path), hasBackup = File.Exists(backup);
            if (!hasPrimary && !hasBackup) return default(T);
            if (!hasPrimary) return Restore(path, backup, read);

            Exception corrupt;
            for (int attempt = 0; ; attempt++)
            {
                try { return read(path); }
                catch (FileNotFoundException) { if (!hasBackup) throw; return Restore(path, backup, read); }
                catch (IOException busy)
                {
                    if (attempt + 1 >= Attempts)
                        throw new IOException("Protected file is busy. The newest copy was kept; retry shortly.", busy);
                    wait(25 << attempt);
                }
                catch (Exception unreadable) { corrupt = unreadable; break; }
            }
            if (!hasBackup) throw new IOException("Protected file could not be read. Original files retained.", corrupt);
            T recovered;
            try { recovered = read(backup); }
            catch (Exception secondary) { throw new IOException("Both protected copies require recovery. Original files retained.", secondary); }
            File.Copy(path, path + ".unreadable-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"), false);
            File.Copy(backup, path, true);
            return recovered;
        }

        static T Restore<T>(string path, string backup, Func<string, T> read)
        {
            T recovered;
            try { recovered = read(backup); }
            catch (Exception secondary) { throw new IOException("Protected backup could not be read. Original files retained.", secondary); }
            File.Copy(backup, path, true);
            return recovered;
        }
    }
}
