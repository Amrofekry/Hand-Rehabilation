using System;
using System.Collections.Generic;
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
        public Vector2[] Route { get; }
        public float Tolerance { get; }
        public int NextWaypoint { get; private set; }
        public int OffPathSamples { get; private set; }
        public int TotalSamples { get; private set; }
        public bool Complete => NextWaypoint >= Route.Length;
        public float Accuracy => TotalSamples == 0 ? 0 : 1f - (float)OffPathSamples / TotalSamples;
        /// <summary>Mean distance of samples from the route segment being traced.</summary>
        public float MeanDeviation => TotalSamples == 0 ? 0 : deviationSum / TotalSamples;
        /// <summary>Whether the latest sample was within tolerance (green) or not (red).</summary>
        public bool LastOnPath { get; private set; } = true;
        bool hasLast;
        Vector2 last;
        float deviationSum;

        public TraceTask() : this(Path, OffPathDistance) { }
        public TraceTask(Vector2[] route, float tolerance)
        {
            if (route == null || route.Length < 2) throw new ArgumentException("A trace needs at least two waypoints.");
            Route = route; Tolerance = tolerance > 0 ? tolerance : OffPathDistance;
        }

        // Each move is the segment from the previous sample, so a fast drag that skips over
        // a waypoint between samples still reaches it. Accuracy is the distance to the
        // route segment being traced, not to the next waypoint (audit A-018/P-015).
        public void Submit(Vector2 normalizedPosition)
        {
            if (Complete) return;
            TotalSamples++;
            var from = hasLast ? last : normalizedPosition;
            last = normalizedPosition; hasLast = true;
            if (SegmentDistance(Route[NextWaypoint], from, normalizedPosition) < WaypointRadius) NextWaypoint++;
            // Near a corner the waypoint is reached early (radius), so also allow the segment just finished.
            int next = Math.Min(Route.Length - 1, NextWaypoint);
            float deviation = SegmentDistance(normalizedPosition, Route[Math.Max(0, next - 1)], Route[next]);
            if (next >= 2) deviation = Math.Min(deviation, SegmentDistance(normalizedPosition, Route[next - 2], Route[next - 1]));
            deviationSum += deviation;
            LastOnPath = deviation <= Tolerance;
            if (!LastOnPath) OffPathSamples++;
        }

        public static float SegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            float dx = b.x - a.x, dy = b.y - a.y, lengthSquared = dx * dx + dy * dy;
            float t = lengthSquared < 1e-12f ? 0 : Mathf.Clamp01(((point.x - a.x) * dx + (point.y - a.y) * dy) / lengthSquared);
            return Vector2.Distance(point, new Vector2(a.x + dx * t, a.y + dy * t));
        }

        /// <summary>Stage 3.3 routes: level 1 is the legacy trail; later levels add turns and amplitude.</summary>
        public static Vector2[] PathFor(int level)
        {
            level = Math.Max(1, Math.Min(10, level));
            if (level == 1) return Path;
            int points = 5 + (level - 1) / 2;          // 5..9 waypoints
            float amplitude = .16f + .03f * (level - 2); // taller zigzags at higher levels
            var route = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                float x = .1f + .8f * i / (points - 1);
                float wave = (float)Math.Sin(i * Math.PI * (level % 2 == 0 ? .5 : .75));
                route[i] = new Vector2(x, Mathf.Clamp(.5f + amplitude * wave, .12f, .88f));
            }
            return route;
        }
    }

    public struct MazeCell { public int x, y; public MazeCell(int x, int y) { this.x = x; this.y = y; } }

    public sealed class MazeTask
    {
        private static readonly string[] LegacyMap = { ".......", ".###.#.", "...#.#.", ".#...#.", ".#....." };
        private readonly bool[,] _walls;
        private readonly bool _generated;
        public int Columns { get; }
        public int Rows { get; }
        public bool IsWall(int x, int y) => x < 0 || x >= Columns || y < 0 || y >= Rows || _walls[x, y];
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Collisions { get; private set; }
        public int Moves { get; private set; }
        public int ShortestPath { get; }
        public bool Complete => X == Columns - 1 && Y == Rows - 1;
        /// <summary>Legacy map keeps its original formula; generated mazes score path efficiency.</summary>
        public float Score => _generated
            ? Mathf.Max(0, Mathf.RoundToInt(100f * ShortestPath / Mathf.Max(Moves, ShortestPath)) - Collisions * 8)
            : Mathf.Max(0, 100 - Collisions * 8 - Mathf.Max(0, Moves - 10) * 2);
        public float Efficiency => Moves == 0 ? 0 : Mathf.Min(1, ShortestPath / (float)Moves);

        public MazeTask()
        {
            Columns = 7; Rows = 5; _walls = new bool[Columns, Rows];
            for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++) _walls[x, y] = LegacyMap[y][x] == '#';
            ShortestPath = Solve().Count - 1;
        }

        /// <summary>Stage 3.3: seeded perfect maze (recursive backtracker) on an odd grid.</summary>
        public MazeTask(int cells, int seed)
        {
            if (cells < 5 || cells % 2 == 0) throw new ArgumentOutOfRangeException(nameof(cells), "Use an odd size of at least 5.");
            Columns = Rows = cells; _generated = true; _walls = new bool[cells, cells];
            for (int y = 0; y < cells; y++) for (int x = 0; x < cells; x++) _walls[x, y] = true;
            var random = new System.Random(seed);
            var stack = new Stack<MazeCell>(); stack.Push(new MazeCell(0, 0)); _walls[0, 0] = false;
            var options = new List<MazeCell>(4);
            while (stack.Count > 0)
            {
                var cell = stack.Peek(); options.Clear();
                foreach (var d in new[] { new MazeCell(2, 0), new MazeCell(-2, 0), new MazeCell(0, 2), new MazeCell(0, -2) })
                {
                    int nx = cell.x + d.x, ny = cell.y + d.y;
                    if (nx >= 0 && ny >= 0 && nx < cells && ny < cells && _walls[nx, ny]) options.Add(new MazeCell(nx, ny));
                }
                if (options.Count == 0) { stack.Pop(); continue; }
                var next = options[random.Next(options.Count)];
                _walls[(cell.x + next.x) / 2, (cell.y + next.y) / 2] = false; _walls[next.x, next.y] = false;
                stack.Push(next);
            }
            ShortestPath = Solve().Count - 1;
        }

        /// <summary>Breadth-first shortest route from the current cell to the goal.</summary>
        public List<MazeCell> Solve()
        {
            var previous = new Dictionary<int, int>(); var queue = new Queue<MazeCell>();
            var start = new MazeCell(X, Y); queue.Enqueue(start); previous[Key(start)] = -1;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell.x == Columns - 1 && cell.y == Rows - 1)
                {
                    var path = new List<MazeCell>();
                    for (int key = Key(cell); key != -1; key = previous[key]) path.Add(new MazeCell(key % Columns, key / Columns));
                    path.Reverse(); return path;
                }
                foreach (var d in new[] { new MazeCell(1, 0), new MazeCell(-1, 0), new MazeCell(0, 1), new MazeCell(0, -1) })
                {
                    var next = new MazeCell(cell.x + d.x, cell.y + d.y);
                    if (IsWall(next.x, next.y) || previous.ContainsKey(Key(next))) continue;
                    previous[Key(next)] = Key(cell); queue.Enqueue(next);
                }
            }
            return new List<MazeCell>();
        }
        int Key(MazeCell cell) => cell.y * Columns + cell.x;

        public bool Move(int dx, int dy)
        {
            if (Complete || Math.Abs(dx) + Math.Abs(dy) != 1) return false;
            var x = X + dx;
            var y = Y + dy;
            if (IsWall(x, y))
            {
                Collisions++;
                return false;
            }
            X = x; Y = y; Moves++;
            return true;
        }

        public string Draw()
        {
            var lines = new string[Rows];
            for (var y = 0; y < Rows; y++)
            {
                var chars = new char[Columns];
                for (var x = 0; x < Columns; x++)
                    chars[x] = x == X && y == Y ? 'P' : x == Columns - 1 && y == Rows - 1 ? 'G' : _walls[x, y] ? '■' : '·';
                lines[y] = new string(chars);
            }
            return string.Join("\n", lines);
        }
    }
}
