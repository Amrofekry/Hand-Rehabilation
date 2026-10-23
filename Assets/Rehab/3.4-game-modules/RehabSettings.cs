using UnityEngine;
namespace GripRehab
{
    [CreateAssetMenu(menuName="Grip Rehab/Demo Settings")]
    public sealed class RehabSettings : ScriptableObject
    {
        [Tooltip("Room shown when Play starts.")] public RoomKind initialRoom=RoomKind.LivingRoom;
        [Tooltip("Keyboard-demo shape; BLE shape comes from calibration metadata.")] public GripShape initialShape=GripShape.Sphere;
        [Min(.1f), Tooltip("Demonstration target in Newtons, not a patient prescription.")] public float targetNewtons=18;
        [Min(.1f), Tooltip("Allowed distance from the target in Newtons.")] public float toleranceNewtons=3;
        [Min(.1f), Tooltip("Required valid hold time in seconds.")] public float holdSeconds=3;
        [Min(.1f), Tooltip("Time to view the memory sequence.")] public float memoryRevealSeconds=4.5f;
        private void OnValidate()
        {
            targetNewtons=Positive(targetNewtons,18); toleranceNewtons=Positive(toleranceNewtons,3);
            holdSeconds=Positive(holdSeconds,3); memoryRevealSeconds=Positive(memoryRevealSeconds,4.5f);
        }
        private static float Positive(float value,float fallback) => float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Max(.1f,value);
    }
}
