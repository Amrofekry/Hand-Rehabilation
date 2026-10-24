using System;
using System.Collections.Generic;

namespace GripRehab
{
    /// <summary>
    /// Stage 3.2 Spatial Assembly: drag each piece from the tray (left) to its matching outline
    /// (right). Coordinates are normalized board units. Dropping on another piece's outline is an
    /// error and the piece returns to the tray; dropping in empty space just returns it. Metrics
    /// are game measures (placement error, path efficiency), not clinical assessments.
    /// </summary>
    public sealed class AssemblyBoard
    {
        public sealed class Piece
        {
            public int Id, Shape;
            public float StartX, StartY, X, Y, TargetX, TargetY;
            public bool Placed;
            internal float path, straight;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly int[] slotOfPiece;
        readonly float tolerance;
        float placementErrorSum;
        public IReadOnlyList<Piece> Pieces => pieces;
        public int Errors { get; private set; }
        public int PlacedCount { get; private set; }
        public bool Complete => PlacedCount == pieces.Count;
        public float MeanPlacementError => PlacedCount == 0 ? 0 : placementErrorSum / PlacedCount;
        /// <summary>Straight-line distance over the distance actually dragged, for placed pieces (1 = direct).</summary>
        public float PathEfficiency
        {
            get
            {
                float straight = 0, path = 0;
                foreach (var p in pieces) if (p.Placed) { straight += p.straight; path += Math.Max(p.path, p.straight); }
                return path <= 0 ? 0 : Math.Min(1, straight / path);
            }
        }
        public float TotalPath { get { float sum = 0; foreach (var p in pieces) sum += p.path; return sum; } }

        public AssemblyBoard(int count, int seed, float tolerance = .08f)
        {
            if (count < 2 || count > 8) throw new ArgumentOutOfRangeException(nameof(count));
            this.tolerance = tolerance;
            var random = new Random(seed);
            var slots = new List<int>(); for (int i = 0; i < count; i++) slots.Add(i);
            for (int i = slots.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = slots[i]; slots[i] = slots[j]; slots[j] = t; }
            slotOfPiece = new int[count];
            int columns = count <= 4 ? 2 : 3, rows = (count + columns - 1) / columns;
            for (int i = 0; i < count; i++)
            {
                int slot = slots[i]; slotOfPiece[i] = slot;
                float tx = .62f + .3f * (columns == 1 ? .5f : (slot % columns) / (float)(columns - 1));
                float ty = .2f + .6f * (rows == 1 ? .5f : (slot / columns) / (float)(rows - 1));
                float sy = .12f + .76f * (count == 1 ? .5f : i / (float)(count - 1));
                float sx = .1f + .2f * (float)random.NextDouble();
                pieces.Add(new Piece { Id = i, Shape = i % 4, StartX = sx, StartY = sy, X = sx, Y = sy, TargetX = tx, TargetY = ty });
            }
        }

        public int SlotOf(int pieceId) => slotOfPiece[pieceId];
        Piece Find(int id) => id >= 0 && id < pieces.Count ? pieces[id] : null;

        public void Move(int id, float x, float y)
        {
            var p = Find(id); if (p == null || p.Placed || !Finite(x) || !Finite(y)) return;
            p.path += Distance(p.X, p.Y, x, y); p.X = x; p.Y = y;
        }

        /// <returns>True when the piece snapped into its own outline.</returns>
        public bool Drop(int id, float x, float y)
        {
            var p = Find(id); if (p == null || p.Placed || !Finite(x) || !Finite(y)) return false;
            if (Distance(x, y, p.TargetX, p.TargetY) <= tolerance)
            {
                placementErrorSum += Distance(x, y, p.TargetX, p.TargetY);
                p.straight = Distance(p.StartX, p.StartY, p.TargetX, p.TargetY);
                p.X = p.TargetX; p.Y = p.TargetY; p.Placed = true; PlacedCount++;
                return true;
            }
            foreach (var other in pieces)
                if (other != p && Distance(x, y, other.TargetX, other.TargetY) <= tolerance) { Errors++; break; }
            p.X = p.StartX; p.Y = p.StartY;
            return false;
        }

        /// <summary>Accessible alternative: choose a piece, then choose an outline.</summary>
        public bool PlaceByTap(int pieceId, int slot)
        {
            var p = Find(pieceId); if (p == null || p.Placed) return false;
            for (int i = 0; i < pieces.Count; i++)
                if (slotOfPiece[i] == slot)
                {
                    var target = pieces[i];
                    p.path += Distance(p.X, p.Y, target.TargetX, target.TargetY);
                    return Drop(pieceId, target.TargetX, target.TargetY);
                }
            return false;
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static float Distance(float ax, float ay, float bx, float by) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));
    }
}
