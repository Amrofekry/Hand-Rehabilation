using System;
using System.IO;
using GripRehab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GripRehab.Editor
{
    public static class RehabVerification
    {
        private const string ScenePath = "Assets/Scenes/GripRehab.unity";
        private const string MaterialPath = "Assets/Resources/RehabBase.mat";

        [MenuItem("Grip Rehab/Verify Scene and Logic")]
        public static void PrepareAndVerify()
        {
            VerifyLogic();
            ApartmentAuthoring.VerifyScene(); RehabIntegrationChecks.Run();
            Debug.Log("[REHAB_VERIFY] PASS: logic and serialized scene validation; no regeneration");
        }

        private static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new Exception("URP Lit shader missing from the Unity Editor.");
            AssetDatabase.CreateAsset(new Material(shader) { name = "RehabBase" }, MaterialPath);
        }

        private static void EnsureSurfaceImports()
        {
            foreach (var name in new[] { "OakFloor", "Plaster", "LinenFabric", "Quartz", "Ceramic", "Rug" })
            {
                foreach (var suffix in new[] { "", "_Normal" })
                {
                    var path = $"Assets/Resources/Surfaces/{name}{suffix}.png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new Exception("Missing surface texture: " + path);
                    var normal = suffix.Length > 0;
                    var changed = importer.textureType != (normal ? TextureImporterType.NormalMap : TextureImporterType.Default)
                                  || importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled
                                  || importer.anisoLevel != 4;
                    if (changed)
                    {
                        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                        importer.wrapMode = TextureWrapMode.Repeat;
                        importer.mipmapEnabled = true;
                        importer.anisoLevel = 4;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static void ConfigureRenderQuality()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                "Assets/Settings/PC_RPAsset.asset");
            if (asset == null) throw new Exception("Missing Windows URP pipeline asset.");
            asset.supportsHDR = true;
            asset.msaaSampleCount = 4;
            asset.shadowDistance = 25f;
            EditorUtility.SetDirty(asset);
        }

        [MenuItem("Grip Rehab/Build Windows Demo")]
        public static void BuildWindows()
        {
            PrepareAndVerify();
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var destination = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows", "GripRehab.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"[REHAB_BUILD] {report.summary.result}: {report.summary.totalErrors} errors");
            Debug.Log($"[REHAB_BUILD] PASS: {destination}");
        }

        [MenuItem("Grip Rehab/Verify Logic Only")]
        public static void VerifyLogic()
        {
            var force = new ForceHoldTask(18, 3, 3);
            force.Step(new ForceReading(18, 0, false, true), 1);
            Require(force.ProgressSeconds == 0, "invalid force advanced a hold");
            for (var i = 0; i < 31; i++) force.Step(new ForceReading(18, i * .1, true, true), .1f);
            Require(force.Complete && force.PeakNewtons == 18, "valid force did not complete hold");
            force.Reset();
            Require(!force.Complete && force.PeakNewtons == 0, "force reset failed");

            var live = new LiveForceState();
            var packet = new ForceBridgeFrame
            {
                schemaVersion = 1, kind = "sample", source = "hardware",
                sessionId = "session", connectionId = "connection",
                calibrationRevision = 1, calibrationCrc = 1234, shapeId = 1,
                sequence = 42, sampleMs = 1000, rawCounts = -12345,
                forceMilliNewtons = 12345, flags = 3, ageMs = 20
            };
            Require(live.Accept(packet, 1) && live.Latest.Valid &&
                    Mathf.Abs(live.Latest.Newtons - 12.345f) < .001f, "Phase 2 force was not decoded");
            Require(!live.Accept(packet, 1.01), "duplicate force advanced");
            packet.sequence = 43;
            packet.sampleMs = 1100;
            packet.flags = 7;
            Require(!live.Accept(packet, 1.1), "fault flags accepted as force");
            packet.flags = 3;
            Require(live.Accept(packet, 1.1), "new force sample rejected");
            live.Tick(1.59);
            Require(!live.Latest.Valid, "stale BLE sample remained valid");
            packet.source = "synthetic";
            packet.sequence = 44;
            packet.sampleMs = 1200;
            Require(!live.Accept(packet, 1.6), "synthetic sample accepted in hardware mode");
            packet.source = "hardware";
            packet.connectionId = "reconnected";
            Require(live.Accept(packet, 1.6) && live.Generation == 2,
                    "reconnect did not create a new session generation");
            packet.sequence = 45;
            packet.sampleMs = 1300;
            Require(live.Accept(packet, 2.2) && live.Generation == 3,
                    "stale transport gap did not end the previous session");
            packet.connectionId = "connection";
            packet.sequence = 46;
            packet.sampleMs = 1400;
            Require(!live.Accept(packet, 2.3), "retired connection packet displaced current session");

            var memory = new SequenceMemoryTask(42);
            Require(memory.SequenceDisplay.Split(new[] { "  " }, StringSplitOptions.None).Length == 4, "sequence display missing");
            for (var i = 0; i < memory.Length; i++)
                Require(memory.Submit(memory.Sequence[i]), "correct memory token was rejected");
            Require(memory.Complete, "memory sequence did not complete");

            var assembly = new SpatialAssemblyTask();
            Require(!assembly.Place(0) && assembly.Errors == 1, "assembly error not scored");
            foreach (var piece in new[] { 2, 0, 3, 1 }) Require(assembly.Place(piece), "assembly correct placement rejected");
            Require(assembly.Complete, "assembly did not complete");

            var maze = new MazeTask();
            Require(!maze.Move(-1, 0) && maze.Collisions == 1, "maze wall was not penalized");
            for (var i = 0; i < 6; i++) Require(maze.Move(1, 0), "maze horizontal path failed");
            for (var i = 0; i < 4; i++) Require(maze.Move(0, 1), "maze vertical path failed");
            Require(maze.Complete && maze.Score < 100, "maze scoring failed");

            var trace = new TraceTask();
            foreach (var point in TraceTask.Path) trace.Submit(point);
            Require(trace.Complete && trace.Accuracy == 1, "trace path failed");

            var frame = new LandmarkFrame { schemaVersion = 1, confidence = .9f, landmarks = new Vector3[21] };
            Require(HandKinematics.IsUsable(frame), "valid hand landmarks rejected");
            frame.confidence = .2f;
            Require(!HandKinematics.IsUsable(frame), "low-confidence landmarks accepted");
            Debug.Log("[REHAB_VERIFY] PASS: force, memory, assembly, maze, trace, and tracking contracts");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("[REHAB_VERIFY] FAIL: " + message);
        }
    }
}

