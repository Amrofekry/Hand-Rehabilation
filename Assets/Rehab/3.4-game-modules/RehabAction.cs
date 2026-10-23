using UnityEngine;
namespace GripRehab
{
    // A persistent button action. No runtime listener accumulation.
    public sealed class RehabAction : MonoBehaviour
    {
        public string command;
        public void Invoke()
        {
            var app=GetComponentInParent<RehabApp>();
            if(app!=null) app.InvokeCommand(command);
        }
    }
}
