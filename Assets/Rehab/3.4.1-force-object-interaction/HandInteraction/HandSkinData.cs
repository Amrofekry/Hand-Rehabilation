using UnityEngine;
namespace GripRehab {
// Project-owned CPU contact data. The imported SDK mesh stays untouched and
// need not be Read/Write enabled in a Windows player.
public sealed class HandSkinData:ScriptableObject {
 public Vector3[] vertices;
 public BoneWeight[] weights;
 public Matrix4x4[] bindPoses;
 public int[] rightVertices,leftVertices;
}
}
