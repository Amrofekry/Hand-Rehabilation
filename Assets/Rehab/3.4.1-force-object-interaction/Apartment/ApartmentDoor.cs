using UnityEngine;
namespace GripRehab {
public sealed class ApartmentDoor:MonoBehaviour {
 public Transform hinge;public float openAngle=85;private Quaternion _closed;private bool _open;
 private void Awake(){if(hinge==null)hinge=transform;_closed=hinge.localRotation;}
 public void Toggle(){_open=!_open;}
 private void Update(){hinge.localRotation=Quaternion.Slerp(hinge.localRotation,_closed*Quaternion.Euler(0,_open?openAngle:0,0),1-Mathf.Exp(-5*Time.deltaTime));}
}
}
