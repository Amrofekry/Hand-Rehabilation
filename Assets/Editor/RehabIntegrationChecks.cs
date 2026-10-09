using System;
using UnityEditor;
using UnityEngine;
namespace GripRehab.Editor
{
    public static class RehabIntegrationChecks
    {
        private static ForceBridgeFrame Packet(long sequence=1) => new ForceBridgeFrame {
            schemaVersion=1,kind="sample",source="hardware",sessionId="s",connectionId="c",
            calibrationRevision=1,calibrationCrc=1234,shapeId=1,sequence=sequence,sampleMs=sequence*100,
            rawCounts=-12345,forceMilliNewtons=18000,flags=3,ageMs=20 };
        private static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
        [MenuItem("Grip Rehab/Verify Phase 2 Input")]
        public static void Run()
        {
            var state=new LiveForceState(); var packet=Packet();
            Check(state.Accept(packet,1),"Valid sample rejected");
            Check(Math.Abs(state.Latest.Newtons-18)<.001,"Wrong force units");
            Check(!state.Accept(packet,1.01),"Duplicate accepted");
            Check(!state.Accept(Packet(0),1.02),"Backward sample accepted");
            packet=Packet(2); packet.flags=7; Check(!state.Accept(packet,1.1),"Fault accepted");
            packet=Packet(2); packet.ageMs=500; Check(!state.Accept(packet,1.1),"Stale sample accepted");
            packet=Packet(2); Check(!state.Accept(packet,1.1,false,.480),"Queue delay must consume freshness budget");
            Check(state.Accept(packet,1.1,false,.3),"Fresh queued sample rejected");
            Check(Math.Abs(state.Latest.Timestamp-.78)<.0001,"Queue age was lost");
            state.Tick(1.281); Check(!state.Latest.Valid,"Delayed sample did not expire");
            packet=Packet(3); packet.calibrationRevision=2; int generation=state.Generation;
            Check(state.Accept(packet,1.3)&&state.Generation>generation,"Calibration did not reset generation");
            packet=Packet(4); packet.shapeId=2; generation=state.Generation;
            Check(state.Accept(packet,1.4)&&state.Generation>generation&&state.Shape==GripShape.Cylinder,"Shape change failed");
            packet=Packet(5); packet.connectionId="c2";
            Check(state.Accept(packet,1.5),"Reconnect failed");
            Check(!state.Accept(Packet(6),1.6),"Retired connection accepted");
            state=new LiveForceState(); packet=Packet(); packet.source="synthetic";
            Check(!state.Accept(packet,1),"Synthetic accepted by default");
            Check(state.Accept(packet,1,true)&&state.IsSynthetic,"Explicit replay failed");
            Check(!new LiveForceState().Accept(Packet(),double.NaN),"NaN clock accepted");
            state=new LiveForceState(); packet=Packet(); packet.sequence=uint.MaxValue; packet.sampleMs=uint.MaxValue;
            Check(state.Accept(packet,1),"Pre-wrap packet failed");
            packet=Packet(0); Check(state.Accept(packet,1.1),"Counter rollover rejected");
            state=new LiveForceState(); state.Accept(Packet(),1); generation=state.Generation;
            Check(state.Accept(Packet(2),1.7)&&state.Generation>generation,"Stale resume did not reset generation");
            var continuous=new LiveForceState();continuous.Accept(Packet(),1,false,.04);int unchanged=continuous.Generation;
            for(int seq=2;seq<=11;seq++)Check(continuous.Accept(Packet(seq),1.65,false,.45-(seq-2)*.04),"Valid continuous queued input rejected");
            Check(continuous.Generation==unchanged,"A main-thread hitch was misclassified as a BLE reconnect");
            Check(continuous.TryGetSample(1.65,out var buffered)&&Math.Abs(buffered.ReceivedAt-1.56)<.0001,"Sample receive time must include queue delay");
            state.Accept(new ForceBridgeFrame{schemaVersion=1,kind="status",source="hardware",sessionId="s",connectionId="c",reason="Disconnected"},1.8);
            Check(!state.Latest.Valid,"Disconnect status retained valid force");
            Debug.Log("[REHAB_INTEGRATION] PASS: units, flags, duplicates, ordering, queued age, stale timeout, calibration, shape, reconnect, synthetic opt-in, rollover");
        }
    }
}
