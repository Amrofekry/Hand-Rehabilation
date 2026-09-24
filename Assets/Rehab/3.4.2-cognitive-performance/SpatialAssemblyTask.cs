namespace GripRehab
{
    // A small fixed assembly puzzle for the desktop demo. The piece/slot contract supports
    // replacing numeric buttons with recognized pick-and-place gestures later.
    public sealed class SpatialAssemblyTask
    {
        private readonly int[] _target = { 2, 0, 3, 1 };
        private int _next;
        public int Placed => _next;
        public int Errors { get; private set; }
        public bool Complete => _next == _target.Length;
        public string TargetDisplay => "3  ·  1  ·  4  ·  2";
        public string ProgressDisplay =>
            (_next > 0 ? "3" : "_") + "  ·  " + (_next > 1 ? "1" : "_") +
            "  ·  " + (_next > 2 ? "4" : "_") + "  ·  " + (_next > 3 ? "2" : "_");

        public bool Place(int piece)
        {
            if (Complete || piece < 0 || piece > 3) return false;
            if (piece != _target[_next]) { Errors++; return false; }
            _next++;
            return true;
        }
    }
}
