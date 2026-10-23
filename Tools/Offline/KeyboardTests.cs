using System;
using GripRehab;

// Executes the production input policy with fabricated frame/key states only.
// This is not a Unity Input/EventSystem, focus callback or OS keyboard test.
class KeyboardTests
{
    static int passed;
    static void Check(bool ok,string name)
    { if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name); }
    static RehabKeyboardGate Armed()
    { var g=new RehabKeyboardGate();g.Sample(1,true,false,false);return g; }
    static void ResumeCase(string reason)
    {
        var g=Armed();g.Sample(2,true,true,true);
        Check(g.Squeezing,reason+": active grip before interruption");
        g.Block(2);g.Sample(2,true,true,true);
        Check(!g.Allowed&&!g.Squeezing,reason+": state change blocks all same-frame reads");
        g.Sample(3,true,true,true);
        Check(g.Allowed&&!g.Squeezing,reason+": held inputs do not squeeze after resume");
        g.Sample(4,true,false,false);g.Sample(5,true,true,false);
        Check(g.Squeezing,reason+": release and new Space press restores grip");
        g.Sample(6,true,false,false);g.Sample(7,true,false,true);
        Check(g.Squeezing,reason+": release and new mouse press restores grip");
    }
    static int Main()
    {
        try
        {
            var fresh=new RehabKeyboardGate();fresh.Sample(1,true,true,true);
            Check(fresh.Allowed&&!fresh.Squeezing,"startup held inputs wait for release");
            fresh.Sample(2,true,false,false);fresh.Sample(3,true,true,false);
            Check(fresh.Squeezing,"Space squeezes after a neutral frame");
            fresh.Sample(4,true,false,false);
            Check(!fresh.Squeezing,"Space release stops squeezing");
            fresh.Sample(5,true,false,true);
            Check(fresh.Squeezing,"left mouse also squeezes");
            foreach(string reason in new[]{"Escape pause","Tab pause","Resume button","mode switch","focus loss","component disable"})ResumeCase(reason);
            foreach(string context in new[]{"Home","profile text field","session options","research activity","lost focus"})
            {
                var g=Armed();g.Sample(2,false,true,true);
                Check(!g.Allowed&&!g.Squeezing,context+": keyboard actions and grip are blocked");
                g.Sample(2,true,false,false);
                Check(!g.Allowed,context+": another consumer cannot reopen input in the same frame");
                g.Sample(3,true,true,false);
                Check(!g.Squeezing,context+": held Space does not leak across context");
            }
            var independent=Armed();independent.Block(2);independent.Sample(3,true,false,true);
            independent.Sample(4,true,true,true);
            Check(independent.Squeezing,"Space can re-arm independently of a held Resume mouse button");
            independent.Sample(5,true,false,true);
            Check(!independent.Squeezing,"unreleased mouse alone remains blocked");
            independent.Sample(6,true,false,false);independent.Sample(7,true,false,true);
            Check(independent.Squeezing,"mouse becomes usable after its own release");
            var a=Armed();var b=Armed();a.Block(2);b.Sample(2,true,true,false);
            Check(!a.Allowed&&b.Squeezing,"separate gate instances do not share state");
            Check(RehabKeyboardGate.TogglePause(true,false,false,false,true),"Escape toggles session options");
            Check(RehabKeyboardGate.TogglePause(false,true,false,false,true),"Tab toggles session options");
            Check(RehabKeyboardGate.TogglePause(true,true,false,false,true),"Escape and Tab together request one toggle");
            Check(!RehabKeyboardGate.TogglePause(false,false,false,false,true),"no pause key does nothing");
            Check(!RehabKeyboardGate.TogglePause(true,true,true,false,true),"Home/activity cannot be unpaused by apartment keys");
            Check(!RehabKeyboardGate.TogglePause(true,true,false,true,true),"typing cannot toggle gameplay");
            Check(!RehabKeyboardGate.TogglePause(true,true,false,false,false),"background keys cannot toggle gameplay");
            Check(RehabKeyboardGate.DemoForce(true,true,true,false,18)==18,"Space/mouse uses demo setpoint");
            Check(RehabKeyboardGate.DemoForce(true,true,true,true,18)==80,"either Shift uses maximum demo force");
            Check(RehabKeyboardGate.DemoForce(true,true,false,true,18)==0,"Shift alone is not a squeeze");
            Check(RehabKeyboardGate.DemoForce(true,false,true,true,18)==0,"blocked context produces no demo force");
            Check(RehabKeyboardGate.DemoForce(false,true,true,true,18)==0,"demo force cannot substitute for Camera/Phase 2");
            Check(RehabKeyboardGate.AdjustSetpoint(18,1,false,false,.1f)==21,"mouse wheel adds three newtons per notch");
            Check(RehabKeyboardGate.AdjustSetpoint(18,0,true,false,.2f)==23,"plus increases at defined rate");
            Check(RehabKeyboardGate.AdjustSetpoint(18,0,false,true,.2f)==13,"minus decreases at defined rate");
            Check(RehabKeyboardGate.AdjustSetpoint(18,0,true,true,.2f)==18,"opposing plus/minus cancel");
            Check(RehabKeyboardGate.AdjustSetpoint(79,1,true,false,.2f)==80,"setpoint cannot exceed maximum");
            Check(RehabKeyboardGate.AdjustSetpoint(2,-1,false,true,.2f)==1,"setpoint cannot drop below minimum");
            Check(RehabKeyboardGate.AdjustSetpoint(18,0,true,false,-1)==18,"negative elapsed time does not change setpoint");
            Console.WriteLine("RESULT "+passed+" passed, 0 failed; production keyboard policy only; no Unity or physical input");return 0;
        }
        catch(Exception e){Console.Error.WriteLine("FAIL "+e);return 1;}
    }
}
