using UnityEngine;
namespace GripRehab {
public sealed class SeatPoint:MonoBehaviour {
 public Transform seatedFeet;
 public Transform standFeet;
 [Range(.8f,1.5f)] public float seatedEyeHeight=1.1f;
 public string stationName="Living room table";
}
}
