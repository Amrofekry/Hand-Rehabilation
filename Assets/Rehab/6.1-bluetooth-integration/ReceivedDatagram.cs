using System.Diagnostics;
namespace GripRehab
{
    internal readonly struct ReceivedDatagram
    {
        public readonly string Json;
        private readonly long _receivedTicks;
        public ReceivedDatagram(string json) { Json=json; _receivedTicks=Stopwatch.GetTimestamp(); }
        public double ElapsedSeconds => (Stopwatch.GetTimestamp()-_receivedTicks)/(double)Stopwatch.Frequency;
    }
}
