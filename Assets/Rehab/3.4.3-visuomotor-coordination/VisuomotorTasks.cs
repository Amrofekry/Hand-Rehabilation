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
        public int NextWaypoint { get; private set; }
        public int OffPathSamples { get; private set; }
        public int TotalSamples { get; private set; }
        public bool Complete => NextWaypoint >= Path.Length;
        public float Accuracy => TotalSamples == 0 ? 0 : 1f - (float)OffPathSamples / TotalSamples;

        public void Submit(Vector2 normalizedPosition)
        {
            if (Complete) return;
            TotalSamples++;
            var distance = Vector2.Distance(normalizedPosition, Path[NextWaypoint]);
            if (distance < .095f) NextWaypoint++;
            else if (distance > .2f) OffPathSamples++;
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
