using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GripRehab.Product
{
    /// <summary>
    /// Stage 7.3: rolling diagnostics log. One file per run, the newest <see cref="KeepFiles"/> kept,
    /// each capped at <see cref="MaxBytes"/>. Lines are scrubbed of anything that looks like a
    /// password or a full path under the user profile; records and names are never written here.
    /// Thread-safe so Unity's threaded log callback can use it.
    /// </summary>
    public sealed class DiagnosticsLog : IDisposable
    {
        public const int KeepFiles = 5, RecentLines = 60;
        public const long MaxBytes = 1024 * 1024;
        readonly object gate = new object();
        readonly Queue<string> recent = new Queue<string>();
        StreamWriter writer;
        long written;
        bool capped;
        // A message that repeats (for example a retry every few seconds) is written once, then summarised.
        string lastBody; int repeats;
        public string Folder { get; }
        public string FilePath { get; }
        public int Errors { get; private set; }
        public int Exceptions { get; private set; }

        public DiagnosticsLog(string folder, DateTime nowUtc)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
            Directory.CreateDirectory(folder);
            FilePath = Path.Combine(folder, "griprehab-" + nowUtc.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + ".log");
            Prune(folder, KeepFiles - 1);
            writer = new StreamWriter(new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        }

        /// <summary>Deletes all but the newest <paramref name="keep"/> GripRehab log files.</summary>
        public static int Prune(string folder, int keep)
        {
            if (!Directory.Exists(folder)) return 0;
            var old = new DirectoryInfo(folder).GetFiles("griprehab-*.log").OrderByDescending(f => f.Name, StringComparer.Ordinal).Skip(Math.Max(0, keep)).ToList();
            int removed = 0;
            foreach (var f in old) { try { f.Delete(); removed++; } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            return removed;
        }

        /// <summary>Removes password-like values and user-profile paths before anything is written.</summary>
        public static string Scrub(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var result = new StringBuilder(text.Length);
            foreach (var line in text.Replace("\r", "").Split('\n'))
            {
                string l = line;
                int at = l.IndexOf("password", StringComparison.OrdinalIgnoreCase);
                if (at >= 0) l = l.Substring(0, at) + "password [removed]";
                l = ReplaceProfile(l, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                if (result.Length > 0) result.Append(" | ");
                result.Append(l);
            }
            return result.ToString();
        }

        static string ReplaceProfile(string line, string profile)
        {
            if (string.IsNullOrEmpty(profile)) return line;
            foreach (var variant in new[] { profile, profile.Replace('\\', '/') })
            {
                int i;
                while ((i = line.IndexOf(variant, StringComparison.OrdinalIgnoreCase)) >= 0) line = line.Substring(0, i) + "%USERPROFILE%" + line.Substring(i + variant.Length);
            }
            return line;
        }

        /// <param name="level">Info, Warning, Error or Exception.</param>
        public void Write(DateTime utc, string level, string message, string stack = null)
        {
            string body = (level ?? "Info").ToUpperInvariant() + " " + Scrub(message);
            if ((level == "Exception" || level == "Error") && !string.IsNullOrEmpty(stack)) body += " | " + Scrub(stack);
            if (body.Length > 4000) body = body.Substring(0, 4000) + " [truncated]";
            string stamp = utc.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " ";
            lock (gate)
            {
                if (level == "Exception") Exceptions++; else if (level == "Error") Errors++;
                if (body == lastBody) { repeats++; return; }
                if (repeats > 0) Append(stamp + "INFO previous line repeated " + repeats + " more time" + (repeats == 1 ? "" : "s"));
                lastBody = body; repeats = 0;
                Append(stamp + body);
            }
        }

        // Caller holds the lock. The text starts with its own "HH:mm:ss.fff " stamp.
        void Append(string text)
        {
            recent.Enqueue(text); while (recent.Count > RecentLines) recent.Dequeue();
            if (writer == null || capped) return;
            long size = Encoding.UTF8.GetByteCount(text) + 2;
            if (written + size > MaxBytes)
            {
                writer.WriteLine(text.Substring(0, Math.Min(13, text.Length)) + "INFO log size limit reached; later lines are kept in memory only");
                capped = true; return;
            }
            try { writer.WriteLine(text); written += size; }
            catch (IOException) { capped = true; }
        }

        public string[] Recent() { lock (gate) return recent.ToArray(); }

        public void Dispose() { lock (gate) { writer?.Dispose(); writer = null; } }
    }
}
