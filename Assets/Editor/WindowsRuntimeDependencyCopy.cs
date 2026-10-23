using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GripRehab.Editor
{
    // Unity2022's Mono player requests this native engine dependency at startup.
    // Copy the matching installed Editor runtime, never a downloaded substitute.
    public sealed class WindowsRuntimeDependencyCopy : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;
        [Serializable] sealed class Evidence
        { public string unity, file, sha256, utc; public bool x64; }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!Application.unityVersion.StartsWith("2022.3.") || report.summary.platform != BuildTarget.StandaloneWindows64 ||
                PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) != ScriptingImplementation.Mono2x) return;
            bool development = (report.summary.options & BuildOptions.Development) != 0;
            string variation = development ? "win64_player_development_mono" : "win64_player_nondevelopment_mono";
            string source = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "windowsstandalonesupport", "Variations", variation, "NVUnityPlugin.dll");
            if (!File.Exists(source)) throw new FileNotFoundException("The installed Unity2022 Windows runtime dependency is missing", source);
            using (var file = new BinaryReader(File.OpenRead(source)))
            {
                if (file.ReadUInt16() != 0x5a4d) throw new InvalidDataException("Unity native runtime is not a PE image");
                file.BaseStream.Position = 60;
                int offset = file.ReadInt32(); file.BaseStream.Position = offset;
                if (file.ReadUInt32() != 0x00004550 || file.ReadUInt16() != 0x8664)
                    throw new InvalidDataException("Unity native runtime must match the Windows x64 player");
            }
            string destination = Path.Combine(Path.GetDirectoryName(report.summary.outputPath), "NVUnityPlugin.dll");
            File.Copy(source, destination, true);
            string hash;
            using (var sha = SHA256.Create()) using (var file = File.OpenRead(destination))
                hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/windows-runtime-dependency.json", JsonUtility.ToJson(new Evidence {
                unity = Application.unityVersion, file = "NVUnityPlugin.dll", sha256 = hash, x64 = true, utc = DateTime.UtcNow.ToString("O") }, true));
            Debug.Log("[REHAB_BUILD] Included matching original Unity2022 x64 runtime dependency");
        }
    }
}
