using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GripRehab
{
    /// <summary>
    /// Opt-in frame recorder for the demo video (Stage 19, 2026-11-01): <c>-record-frames &lt;folder&gt; [fps]</c> saves the
    /// game's own rendered frames (UI included) as numbered JPEGs, which ffmpeg turns into a video. Desktop capture cannot read
    /// the D3D11 window and would record other windows on top, so the game records itself. Never created without the flag.
    /// </summary>
    public sealed class RehabFrameRecorder : MonoBehaviour
    {
        string folder; float interval; int index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-record-frames");
            if (at < 0 || at + 1 >= args.Length) return;
            float fps = at + 2 < args.Length && float.TryParse(args[at + 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? Mathf.Clamp(f, 1, 30) : 12;
            var go = new GameObject("Frame recorder (-record-frames)"); DontDestroyOnLoad(go);
            var recorder = go.AddComponent<RehabFrameRecorder>();
            recorder.folder = Path.GetFullPath(args[at + 1]); recorder.interval = 1f / fps;
            Directory.CreateDirectory(recorder.folder);
        }

        IEnumerator Start()
        {
            var endOfFrame = new WaitForEndOfFrame(); float next = 0;
            while (true)
            {
                yield return endOfFrame;
                if (Time.realtimeSinceStartup < next) continue;
                next = Time.realtimeSinceStartup + interval;
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, (index++).ToString("D6") + ".jpg"), shot.EncodeToJPG(88));
                Destroy(shot);
            }
        }
    }
}
