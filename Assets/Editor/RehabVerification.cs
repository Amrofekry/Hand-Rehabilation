using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GripRehab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using VirtualGrasp;

namespace GripRehab.Editor
{
    public static class RehabVerification
    {
        private const string ScenePath = "Assets/Scenes/3.1-Bootstrap.unity";
        private const string MaterialPath = "Assets/Resources/RehabBase.mat";

        [MenuItem("Grip Rehab/Verify Scene and Logic")]
        public static void PrepareAndVerify()
        {
            if (UnityEngine.Object.FindAnyObjectByType<ApartmentAcceptance>() != null)
                throw new Exception("Test-only Editor acceptance object was saved into the scene");
            VerifyLogic();
            ApartmentAuthoring.VerifyScene();
            VerifyVirtualGraspProfile();
            RehabPremiumSkin.VerifyLegibility();
            RehabIntegrationChecks.Run();
            VerifySpaceIsNotUiSubmit();
            Debug.Log("[REHAB_VERIFY] PASS: logic and serialized scene validation; no regeneration");
        }

        private static void VerifySpaceIsNotUiSubmit()
        {
            var input = File.ReadAllText("ProjectSettings/InputManager.asset");
            var submitAxes = Regex.Matches(input,
                @"(?ms)^\s*- serializedVersion: 3\s*\r?\n\s*m_Name: Submit\s*\r?\n.*?(?=^\s*- serializedVersion: 3|\z)");
            if (submitAxes.Count == 0) throw new Exception("No keyboard Submit axis remains for menus");
            foreach (Match axis in submitAxes)
                if (Regex.IsMatch(axis.Value,
                    @"(?m)^\s*(?:altPositiveButton|positiveButton):\s*space\s*$"))
                    throw new Exception("Space is mapped to UI Submit and can activate Quit during grip");
        }

        private static void VerifyVirtualGraspProfile()
        {
            const string path = "Assets/Resources/VG_ControllerProfiles/VG_CP_Rehab.Input.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VG_ControllerProfile>(path);
            if (profile == null || !profile.m_controllerClasses.StartsWith("RehabVGController", StringComparison.Ordinal))
                throw new Exception("Project VirtualGrasp controller profile is missing or misnamed");
            if (!Resources.LoadAll<VG_ControllerProfile>("VG_ControllerProfiles").Contains(profile))
                throw new Exception("VirtualGrasp cannot discover the project controller profile through Resources");
            var sdk = UnityEngine.Object.FindAnyObjectByType<RehabVirtualGrasp>();
            if (sdk == null) throw new Exception("Scene VirtualGrasp component is missing");
            var serialized = new SerializedObject(sdk);
            var binding = serialized.FindProperty("m_avatars.Array.data[0].m_primarySensorSetup.m_profile");
            if (binding == null || binding.objectReferenceValue != profile)
                throw new Exception("The scene is not bound to the project VirtualGrasp controller profile");
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
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("Windows build canceled before saving the scene");
            var destination = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Windows", "GripRehab.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = GripRehab.Composition.SceneComposition.Current != null ? SceneOrganization.BuildScenePaths() : AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/UI/GripUiGallery.unity") != null ?
                    new[] { ScenePath, "Assets/UI/GripUiGallery.unity" } : new[] { ScenePath },
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
            Require(live.TryGetSample(1.01, out var contracted) &&
                    contracted.Source == ForceSourceKind.BleBridge &&
                    contracted.Shape == GripShape.Sphere &&
                    contracted.CalibrationRevision == 1 &&
                    Mathf.Abs(contracted.Newtons - 12.345f) < .001f,
                    "Phase 2 source contract lost sample provenance");
            Require(!live.TryGetSample(1.5, out _),
                    "Phase 2 source contract accepted stale force");
            var handContract = new HandSample(HandSide.Left, new Vector3[21],
                1, 1.1, 7, .9f, true);
            Require(handContract.IsFresh(1.2) && !handContract.IsFresh(1.5),
                    "Hand source contract freshness is incorrect");
            var policySettings = ScriptableObject.CreateInstance<ResearchPolicySettings>();
            try
            {
                policySettings.signPersistenceSeconds = 1;
                policySettings.stableWindowSeconds = 1;
                policySettings.cooldownSeconds = 2;
                var policy = new ResearchAdaptivePolicy(policySettings);
                policy.Step(new ResearchObservation(1, .95, FatigueSign.None, true, true, true, true));
                Require(policy.Step(new ResearchObservation(2, 1.95, FatigueSign.None,
                    true, true, true, true)).Level == 2,
                    "Research difficulty did not wait for valid stable input");
                policy.Step(new ResearchObservation(3, 2.95, FatigueSign.Force,
                    true, true, true, false));
                Require(policy.Step(new ResearchObservation(4, 3.95, FatigueSign.Force,
                    true, true, true, false)).Status == ResearchSessionStatus.WarningResumeOrQuit,
                    "One independent sign must show Resume/Quit warning");
                policy.Step(new ResearchObservation(5, 4.95,
                    FatigueSign.Force | FatigueSign.Kinematic, true, true, true, false));
                Require(policy.Step(new ResearchObservation(6, 5.95,
                    FatigueSign.Force | FatigueSign.Kinematic, true, true, true, false))
                    .Status == ResearchSessionStatus.SafetyStopped,
                    "Two independent signs must latch safety stop");
            }
            finally { UnityEngine.Object.DestroyImmediate(policySettings); }
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
            frame.imageWidth = 640; frame.imageHeight = 480;
            frame.landmarks[1] = new Vector3(.1f, .1f, .1f);
            Require(Vector3.Distance(HandKinematics.PosePoint(frame, 1), new Vector3(.1f, -.075f, -.1f)) < .00001f,
                "normalized image aspect ratio distorted hand geometry");
            frame.worldLandmarks = new Vector3[21];
            frame.worldLandmarks[1] = new Vector3(.01f, .02f, .03f);
            Require(Vector3.Distance(HandKinematics.PosePoint(frame, 1, true), new Vector3(-.01f, -.02f, -.03f)) < .00001f,
                "world hand points or horizontal reflection were mapped incorrectly");
            Require(!HandKinematics.HasPoseGeometry(frame), "collapsed world palm was accepted as a pose");
            frame.worldLandmarks[1].x = float.NaN;
            Require(!HandKinematics.IsUsable(frame), "non-finite world landmarks accepted");
            frame.worldLandmarks = new Vector3[20];
            Require(!HandKinematics.IsUsable(frame), "incomplete world hand accepted");
            frame.worldLandmarks = null; frame.imageWidth = 640; frame.imageHeight = 0;
            Require(!HandKinematics.IsUsable(frame), "incomplete image dimensions accepted");
            frame.imageWidth = frame.imageHeight = 0;
            frame.confidence = .54f; frame.landmarks[0].x = 1.05f;
            Require(HandKinematics.IsUsable(frame), "real camera edge landmark or handedness confidence rejected");
            frame.landmarks[0].x = 1.5f;
            Require(!HandKinematics.IsUsable(frame), "far outside image landmark accepted");
            frame.landmarks[0].x = 0;

            frame.confidence = .2f;
            Require(!HandKinematics.IsUsable(frame), "low-confidence landmarks accepted");
            frame.confidence = .9f; frame.handedness = "Left";
            Require(HandKinematics.IsUsable(frame) && HandKinematics.IsLeft(frame), "left hand label rejected");
            frame.handedness = "unknown";
            Require(!HandKinematics.IsUsable(frame), "unknown handedness accepted");
            frame.handedness = "Right"; frame.landmarks[0].z = float.NaN;
            Require(!HandKinematics.IsUsable(frame), "non-finite hand depth accepted");
            frame.landmarks[0].z = 0; frame.ageMs = 500;
            Require(!HandKinematics.IsUsable(frame), "already stale camera frame accepted");
            var open = new LandmarkFrame { schemaVersion = 1, source = "mediapipe-test", handedness = "Right", confidence = .95f, landmarks = new Vector3[21] };
            var closed = new LandmarkFrame { schemaVersion = 1, source = "mediapipe-test", handedness = "Right", confidence = .95f, landmarks = new Vector3[21] };
            open.landmarks[0] = closed.landmarks[0] = new Vector3(.5f, .8f, 0);
            for (int finger = 0; finger < 4; finger++)
            {
                int n = 5 + finger * 4; float x = .38f + finger * .08f;
                open.landmarks[n] = closed.landmarks[n] = new Vector3(x, .54f, 0);
                open.landmarks[n + 1] = closed.landmarks[n + 1] = new Vector3(x, .46f, 0);
                open.landmarks[n + 2] = new Vector3(x, .38f, 0); open.landmarks[n + 3] = new Vector3(x, .30f, 0);
                closed.landmarks[n + 2] = new Vector3(x, .52f, 0); closed.landmarks[n + 3] = new Vector3(x, .60f, 0);
            }
            Require(HandKinematics.GripScore(open) < .2f && HandKinematics.GripScore(closed) > .7f,
                    "camera open/fist landmarks did not separate");
            var gate = new CameraGripGate();
            gate.Step(.8f, true, .06f, .55f, .28f, .12f);
            Require(!gate.Closed, "one short camera frame closed the grasp");
            gate.Step(.8f, true, .06f, .55f, .28f, .12f);
            Require(gate.Closed, "sustained fist did not close the grasp");
            gate.Step(.35f, true, .2f, .55f, .28f, .12f);
            Require(gate.Closed, "hysteresis released an intermediate grip");
            gate.Step(.1f, true, .12f, .55f, .28f, .12f);
            Require(!gate.Closed, "open hand did not release");
            gate.Step(.8f, true, .12f, .55f, .28f, .12f); gate.Step(.8f, false, .01f, .55f, .28f, .12f);
            Require(!gate.Closed, "lost camera tracking kept a grip");
            Debug.Log("[REHAB_VERIFY] PASS: force, memory, assembly, maze, trace, and tracking contracts");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception("[REHAB_VERIFY] FAIL: " + message);
        }
    }
}

