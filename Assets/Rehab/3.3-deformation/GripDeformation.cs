using UnityEngine;

namespace GripRehab
{
    // Deterministic bounded fallback, used until licensed Obi Softbody/Fluid are supplied.
    [System.Serializable]
    public sealed class GripDeformation
    {
        private GameObject _object;
        [SerializeField] private GameObject[] _grips;
        private Vector3[] _scales;
        public GameObject[] Grips => _grips;
#if UNITY_EDITOR
        public void AuthorGrips()
        {
            var grips=new GameObject[3];
            for(int i=0;i<3;i++) { SetShape((GripShape)(i+1)); grips[i]=_object; _object=null; }
            _grips=grips; SetShape(GripShape.Sphere);
        }
#endif
        private Vector3 _restScale;
        private GripShape _shape;

        public void SetShape(GripShape shape)
        {
            if (_grips != null && _grips.Length==3)
            {
                if (_scales==null) { _scales=new Vector3[3]; for(int i=0;i<3;i++) _scales[i]=_grips[i].transform.localScale; }
                _shape=shape;
                for(int i=0;i<3;i++) _grips[i].SetActive(i==(int)shape-1);
                _object=_grips[(int)shape-1]; _restScale=_scales[(int)shape-1];
                _object.transform.localScale=_restScale;
                return;
            }
            if (_object != null) Object.Destroy(_object);
            _shape = shape;
            var primitive = shape == GripShape.Sphere ? PrimitiveType.Sphere : PrimitiveType.Cylinder;
            _object = GameObject.CreatePrimitive(primitive);
            _object.name = $"3.3 Grip {shape} - bounded visual deformation";
            _restScale = shape == GripShape.Sphere ? new Vector3(0.85f, 0.85f, 0.85f) :
                shape == GripShape.Cylinder ? new Vector3(0.54f, 0.62f, 0.54f) : new Vector3(0.16f, 0.7f, 0.16f);
            _object.transform.position = new Vector3(0,
                shape == GripShape.Sphere ? 1.075f : shape == GripShape.Cylinder ? 1.26f : 1.36f, .2f);
            _object.transform.localScale = _restScale;
            var color = shape == GripShape.Sphere ?
                new Color(0.27f, 0.76f, 0.67f) : shape == GripShape.Cylinder ?
                new Color(0.93f, 0.59f, 0.34f) : new Color(0.65f, 0.5f, 0.82f);
            RehabMaterials.Apply(_object.GetComponent<Renderer>(), color);
            if (shape != GripShape.Sphere)
            {
                foreach (var y in new[] { -.75f, .75f })
                {
                    var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    band.name = "Soft grip end band";
                    band.transform.SetParent(_object.transform, false);
                    band.transform.localPosition = new Vector3(0, y, 0);
                    band.transform.localScale = new Vector3(1.03f, .04f, 1.03f);
                    RehabMaterials.Apply(band.GetComponent<Renderer>(), RehabSurface.DarkMetal);
                }
            }
        }

        public void SetVisible(bool visible)
        {
            if (_object != null) _object.SetActive(visible);
        }

        public void Step(float measuredNewtons, float deltaSeconds)
        {
            if (_object == null) return;
            var compression = Mathf.Clamp01(measuredNewtons / 50f) * 0.22f;
            var target = new Vector3(_restScale.x * (1 + compression * 0.45f),
                _restScale.y * (1 - compression), _restScale.z * (1 + compression * 0.45f));
            _object.transform.localScale = Vector3.Lerp(_object.transform.localScale, target,
                1 - Mathf.Exp(-9f * Mathf.Max(0, deltaSeconds)));
            _object.transform.Rotate(0, deltaSeconds * (_shape == GripShape.Pencil ? 9f : 18f), 0);
        }
    }
}
