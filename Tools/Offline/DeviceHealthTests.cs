using System;
using GripRehab;

// Executes the production device-status logic with injected packets, dropouts, stale gaps and reconnects (no BLE or Unity).
class DeviceHealthTests
{
    static int passed;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
    static int Main()
    {
        try
        {
            var h = new DeviceHealth(); double t = 0; int count = 0; uint seq = 0;
            h.Observe(t, 0, 0, 0, false, 1, false);
            Check(h.State == DeviceLinkState.Waiting, "no packets yet: WAITING");
            // 50 Hz for 3 s, every packet arrives.
            for (int i = 0; i < 150; i++) { t += .02; count++; seq++; h.Observe(t, count, seq, 12, true, 1, false); }
            Check(h.State == DeviceLinkState.Live && Math.Abs(h.RateHz - 50) < 2, "50 Hz stream: LIVE at " + h.RateHz.ToString("0.0") + " Hz");
            Check(h.LossPercent == 0, "no dropouts: 0 % loss");
            Check(h.AgeMs == 12, "sample age is the bridge's age");
            // Every 5th packet dropped (sequence still advances) for 5 s, so a whole 2 s window is lossy: 20 % loss.
            for (int i = 0; i < 250; i++) { t += .02; seq++; if (i % 5 == 4) continue; count++; h.Observe(t, count, seq, 12, true, 1, false); }
            Check(Math.Abs(h.LossPercent - 20) < 3, "every 5th packet lost: about 20 % loss (" + h.LossPercent.ToString("0.0") + " %)");
            // Packets stop: stale after 0.5 s, age grows.
            for (int i = 0; i < 40; i++) { t += .02; h.Observe(t, count, seq, 12, i < 10, 1, false); }
            Check(h.State == DeviceLinkState.Stale && h.AgeMs >= 700, "no packets for 0.8 s: STALE, age " + h.AgeMs + " ms");
            // Bridge reconnects (new generation): counted once, shown as RECONNECTED, then LIVE.
            for (int i = 0; i < 20; i++) { t += .02; count++; seq = (uint)(1000 + i); h.Observe(t, count, seq, 10, true, 2, false); }
            Check(h.Reconnects == 1 && h.State == DeviceLinkState.Reconnected, "new connection: RECONNECTED, counted once");
            for (int i = 0; i < 200; i++) { t += .02; count++; seq++; h.Observe(t, count, seq, 10, true, 2, false); }
            Check(h.State == DeviceLinkState.Live && h.Reconnects == 1 && h.LossPercent == 0, "after the reconnect: LIVE, loss measured on the new link only");
            // A status frame (new connection, no sample yet) resets the sequence to 0; loss must not count the jump to the new link's numbers.
            t += .02; h.Observe(t, count, 0, 10, false, 3, false);
            for (int i = 0; i < 150; i++) { t += .02; count++; seq = (uint)(5000 + i); h.Observe(t, count, seq, 10, true, 3, false); }
            Check(h.Reconnects == 2 && h.LossPercent == 0, "reconnect by status frame: no false loss (" + h.LossPercent.ToString("0.0") + " %)");
            for (int i = 0; i < 30; i++) { t += .02; h.Observe(t, count, seq, 10, false, 3, false); }
            Check(h.State == DeviceLinkState.Stale && h.RateHz == 0, "a stale link shows 0 Hz, not the last rate");
            // Invalid (uncalibrated) samples are never shown as live.
            t += .02; count++; seq++; h.Observe(t, count, seq, 10, false, 2, false);
            Check(h.State == DeviceLinkState.Stale, "an invalid sample is not LIVE");
            h.Observe(t + .02, count + 1, seq + 1, 10, true, 2, true);
            Check(h.Synthetic, "a synthetic replay is labelled");
            h.Observe(double.NaN, count, seq, 10, true, 2, false);
            Check(h.State != DeviceLinkState.Waiting, "a bad clock value is ignored");
            Check(!DeviceHealth.BatteryReported, "battery is not in the protocol, so it is not guessed");
            Console.WriteLine("DEVICE HEALTH TESTS PASSED: " + passed);
            return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error.Message); return 1; }
    }
}
