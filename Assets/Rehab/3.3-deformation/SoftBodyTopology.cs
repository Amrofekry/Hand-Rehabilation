using UnityEngine;
namespace GripRehab {
// Baked in Editor; coordinates remain in the original visual mesh local space.
[CreateAssetMenu(menuName="Grip Rehab/Soft Body Topology")]
public sealed class SoftBodyTopology : ScriptableObject {
 public Mesh sourceMesh;
 public Vector3[] nodes, normals;
 public Vector2Int[] edges;
 public Vector2Int[] wallPairs;
 public int[] vertexNodes;
 public float[] vertexWeights;
 public bool Matches(Mesh mesh)=>sourceMesh==mesh&&nodes!=null&&nodes.Length>0&&nodes.Length<=96&&
  normals!=null&&normals.Length==nodes.Length&&edges!=null&&vertexNodes!=null&&vertexWeights!=null&&
  vertexNodes.Length==mesh.vertexCount*4&&vertexWeights.Length==vertexNodes.Length;
}
}
