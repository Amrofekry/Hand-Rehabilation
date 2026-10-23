using System;
using UnityEngine;

namespace GripRehab
{
    public sealed class TraceTask
    {
        // Normalized screen-space trail, deliberately sparse so it can be followed without
        // fine motor precision. This is a game metric, not a clinical ROM measure.
        public static readonly Vector2[] Path =
        {
            new Vector2(.23f, .34f), new Vector2(.36f, .55f), new Vector2(.49f, .39f),
            new Vector2(.62f, .58f), new Vector2(.82f, .43f)
        };
        public const float WaypointRadius = .095f, OffPathDistance = .12f;
        public int NextWaypoint { get; private set; }
        public int OffPathSamples { get; private set; }
        public int TotalSamples { get; private set; }
        public bool Complete => NextWaypoint >= Path.Length;
        public float Accuracy => TotalSamples == 0 ? 0 : 1f - (float)OffPathSamples / TotalSamples;
        bool hasLast;
        Vector2 last;

        // Each move is the segment from the previous sample, so a fast drag that skips over
        // a waypoint between samples still reaches it. Accuracy is the distance to the
        // route segment being traced, not to the next waypoint (audit A-018/P-015).
        public void Submit(Vector2 normalizedPosition)
        {
            if (Complete) return;
            TotalSamples++;
            var from = hasLast ? last : normalizedPosition;
            last = normalizedPosition; hasLast = true;
            if (SegmentDistance(Path[NextWaypoint], from, normalizedPosition) < WaypointRadius) NextWaypoint++;
            var routeStart = Path[Math.Max(0, NextWaypoint - 1)];
            var routeEnd = Path[Math.Min(Path.Length - 1, NextWaypoint)];
            if (SegmentDistance(normalizedPosition, routeStart, routeEnd) > OffPathDistance) OffPathSamples++;
        }

        static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            float dx = b.x - a.x, dy = b.y - a.y, lengthSquared = dx * dx + dy * dy;
            float t = lengthSquared < 1e-12f ? 0 : Mathf.Clamp01(((point.x - a.x) * dx + (point.y - a.y) * dy) / lengthSquared);
            return Vector2.Distance(point, new Vector2(a.x + dx * t, a.y + dy * t));
        }
    }

    public sealed class MazeTask
    {
        private const int Width = 7;
        private const int Height = 5;
        private readonly string[] _map = { ".......", ".###.#.", "...#.#.", ".#...#.", ".#....." };
        public int Columns => Width;
        public int Rows => Height;
        public bool IsWall(int x, int y) => x < 0 || x >= Width || y < 0 || y >= Height || _map[y][x] == '#';
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Collisions { get; private set; }
        public int Moves { get; private set; }
        public bool Complete => X == Width - 1 && Y == Height - 1;
        public float Score => Mathf.Max(0, 100 - Collisions * 8 - Mathf.Max(0, Moves - 10) * 2);

        public bool Move(int dx, int dy)
        {
            if (Complete || Math.Abs(dx) + Math.Abs(dy) != 1) return false;
            var x = X + dx;
            var y = Y + dy;
            if (x < 0 || x >= Width || y < 0 || y >= Height || _map[y][x] == '#')
            {
                Collisions++;
                return false;
            }
            X = x; Y = y; Moves++;
            return true;
        }

        public string Draw()
        {
            var lines = new string[Height];
            for (var y = 0; y < Height; y++)
            {
                var chars = _map[y].ToCharArray();
                chars[Width - 1] = y == Height - 1 ? 'G' : chars[Width - 1];
                if (y == Y) chars[X] = 'P';
                lines[y] = new string(chars).Replace('.', '·').Replace('#', '■');
            }
            return string.Join("\n", lines);
        }
    }
}
