using System;
using System.Collections.Generic;

namespace GripRehab
{
    // Standalone deterministic memory exercise. Avatar/hand gesture adapters can submit the
    // same tokens later; the demo uses accessible on-screen buttons and number keys.
    public sealed class SequenceMemoryTask
    {
        private readonly int[] _sequence;
        private int _index;
        public int Correct => _index;
        public int Length => _sequence.Length;
        public int Mistakes { get; private set; }
        public bool Complete => _index == _sequence.Length;
        public IReadOnlyList<int> Sequence => _sequence;
        public string SequenceDisplay { get { var values = new string[_sequence.Length]; for (var i = 0; i < values.Length; i++) values[i] = (_sequence[i] + 1).ToString(); return string.Join("  ", values); } }

        public SequenceMemoryTask(int seed, int length = 4)
        {
            if (length < 1 || length > 12) throw new ArgumentOutOfRangeException(nameof(length));
            var random = new Random(seed);
            _sequence = new int[length];
            for (var i = 0; i < length; i++) _sequence[i] = random.Next(0, 4);
        }

        public bool Submit(int token)
        {
            if (Complete || token < 0 || token > 3) return false;
            if (_sequence[_index] != token) { Mistakes++; _index = 0; return false; }
            _index++;
            return true;
        }
    }
}

