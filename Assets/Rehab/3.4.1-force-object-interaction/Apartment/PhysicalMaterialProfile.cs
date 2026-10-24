using UnityEngine;
namespace GripRehab {
public enum MaterialKind { Rubber, Foam, Food, Ceramic, Paper }
public enum DamageState { Intact, SlightlyDamaged, Damaged, CriticallyDamaged, Broken }
[CreateAssetMenu(menuName="Grip Rehab/Physical Material Profile")]
public sealed class PhysicalMaterialProfile : ScriptableObject {
 [Header("Material")]
 public MaterialKind kind;
 [Min(.01f)] public float massKg=.2f;
 [Range(0,1)] public float friction=.55f;
 [Range(0,1)] public float restitution=.15f;
 [Header("Deformation")]
 public bool deformable=true;
 [Min(1)] public float fullCompressionNewtons=35;
 [Range(0,.5f)] public float maxDepthMeters=.018f;
 [Min(.005f)] public float deformationRadiusMeters=.06f;
 [Range(0,1)] public float plasticity=0;
 [Min(.01f)] public float recoveryPerSecond=8;
 [Header("Damage")]
 public bool breakable;
 [Min(.01f)] public float impactEnergyJoules=1;
 [Min(1)] public float sustainedForceNewtons=55;
 [Min(.05f)] public float forceDwellSeconds=.4f;
 [Min(.01f)] public float damageCapacity=1;
 [Header("Liquid (Stage 3.4: in-house stand-in for the Obi Fluid role)")]
 public bool releasesLiquid;
 public Color liquidColor=new Color(.75f,.88f,1f,.85f);
 public Material liquidMaterial;
 [Range(0,1)] public float liquidStartCompression=.25f;
 [Min(0)] public float liquidRate=70;
 [Min(0)] public int liquidBurst=0;
 [Header("Performance")][Min(128)] public int maxVertices=4096;
 [Header("Debug")]
 public bool showContactGizmos;
}
}
