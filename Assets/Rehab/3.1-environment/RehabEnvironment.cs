using UnityEngine;
using UnityEngine.Rendering;

namespace GripRehab
{
    public enum RoomKind { LivingRoom, Kitchen }

    // Detailed, self-contained rehabilitation spaces built from reusable URP materials.
    // Every piece is intentionally local so the Unity project remains redistributable.
    [System.Serializable]
    public sealed class RehabEnvironment
    {
        private GameObject _root;
        [SerializeField] private GameObject[] _rooms;
        public GameObject[] Rooms => _rooms;
#if UNITY_EDITOR
        public void AuthorRooms()
        {
            var rooms = new GameObject[2];
            for (int i=0; i<2; i++) { Build((RoomKind)i); rooms[i]=_root; _root=null; }
            _rooms=rooms;
            Build(RoomKind.LivingRoom);
        }
#endif

        public void Build(RoomKind room)
        {
            if (_rooms != null && _rooms.Length == 2)
            {
                for (int i=0;i<_rooms.Length;i++) _rooms[i].SetActive(i==(int)room);
                return;
            }
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject(room == RoomKind.LivingRoom ? "3.1 Living Room" : "3.1 Kitchen");

            Block("Textured floor", new Vector3(0, -.16f, 0), new Vector3(12, .3f, 10),
                room == RoomKind.LivingRoom ? RehabSurface.OakFloor : RehabSurface.Ceramic);
            Block("Back plaster wall", new Vector3(0, 2.8f, 4.8f), new Vector3(12, 6, .25f), RehabSurface.Plaster);
            Block("Left plaster wall", new Vector3(-5.9f, 2.8f, 0), new Vector3(.25f, 6, 10), RehabSurface.Plaster);
            Block("Right plaster wall", new Vector3(5.9f, 2.8f, 0), new Vector3(.25f, 6, 10), RehabSurface.Plaster);
            Block("Ceiling", new Vector3(0, 5.72f, 0), new Vector3(12, .18f, 10), RehabSurface.Plaster);
            Trim();
            TrainingStation();
            if (room == RoomKind.LivingRoom) BuildLivingRoom();
            else BuildKitchen();
            Lighting(room);
        }

        private void Trim()
        {
            var trim = new Color(.88f, .87f, .82f);
            Block("Back skirting", new Vector3(0, .12f, 4.57f), new Vector3(11.8f, .24f, .06f), trim);
            Block("Left skirting", new Vector3(-5.74f, .12f, 0), new Vector3(.06f, .24f, 9.8f), trim);
            Block("Right skirting", new Vector3(5.74f, .12f, 0), new Vector3(.06f, .24f, 9.8f), trim);
            Block("Back ceiling cornice", new Vector3(0, 5.55f, 4.56f), new Vector3(11.8f, .11f, .1f), trim);
        }

        private void TrainingStation()
        {
            Block("Grounded woven rug", new Vector3(0, .012f, .22f), new Vector3(4.3f, .025f, 2.9f), RehabSurface.Rug);
            Block("Walnut training table", new Vector3(0, .57f, .3f), new Vector3(3.6f, .14f, 1.9f), RehabSurface.Walnut);
            Block("Table edge reveal", new Vector3(0, .49f, -.63f), new Vector3(3.54f, .035f, .04f), RehabSurface.DarkMetal);
            foreach (var x in new[] { -1.57f, 1.57f })
                foreach (var z in new[] { -.38f, .96f })
                {
                    Block("Powder-coated table leg", new Vector3(x, .23f, z),
                        new Vector3(.095f, .47f, .095f), RehabSurface.DarkMetal);
                    Block("Table foot", new Vector3(x, .035f, z),
                        new Vector3(.17f, .04f, .17f), RehabSurface.Rubber);
                }
            Block("Non-slip grip mat", new Vector3(0, .65f, .2f), new Vector3(1.18f, .025f, 1.07f), RehabSurface.Rubber);
            Block("Mat accent edge", new Vector3(0, .666f, -.326f), new Vector3(1.12f, .008f, .012f),
                new Color(.32f, .71f, .62f));
            Block("Measurement module", new Vector3(1.24f, .71f, .47f), new Vector3(.6f, .15f, .49f), RehabSurface.DarkMetal);
            Block("Module display", new Vector3(1.24f, .789f, .47f), new Vector3(.45f, .008f, .22f),
                new Color(.18f, .63f, .68f));
            for (var i = 0; i < 3; i++)
                Cylinder("Control dial", new Vector3(1.04f + i * .18f, .8f, .68f),
                    new Vector3(.045f, .012f, .045f), RehabSurface.BrushedMetal);
            Block("Tool tray", new Vector3(-1.2f, .67f, .49f), new Vector3(.65f, .055f, .35f), RehabSurface.BrushedMetal);
            Cylinder("Resting pencil grip", new Vector3(-1.2f, .73f, .49f),
                new Vector3(.055f, .21f, .055f), RehabSurface.Rubber).transform.rotation = Quaternion.Euler(90, 0, 22);
        }

        private void BuildLivingRoom()
        {
            // Sofa upholstery has separate seat/back cushions, seam reveals and feet.
            Block("Sofa plinth", new Vector3(-3.25f, .38f, 2.74f), new Vector3(2.95f, .42f, 1.22f), RehabSurface.Walnut);
            Block("Sofa seat", new Vector3(-3.25f, .66f, 2.73f), new Vector3(2.87f, .28f, 1.22f), RehabSurface.LinenFabric);
            Block("Sofa back", new Vector3(-3.25f, 1.14f, 3.29f), new Vector3(2.95f, .89f, .2f), RehabSurface.LinenFabric);
            foreach (var x in new[] { -4.59f, -1.91f })
            {
                Block("Upholstered arm", new Vector3(x, .85f, 2.74f),
                    new Vector3(.28f, .68f, 1.29f), RehabSurface.LinenFabric);
                Block("Sofa foot", new Vector3(x, .12f, 2.25f),
                    new Vector3(.11f, .24f, .11f), RehabSurface.DarkMetal);
            }
            for (var i = 0; i < 3; i++)
            {
                var x = -4.12f + i * .88f;
                Block("Individual seat cushion", new Vector3(x, .84f, 2.67f),
                    new Vector3(.82f, .13f, 1.06f), RehabSurface.LinenFabric);
                Block("Back cushion", new Vector3(x, 1.19f, 3.11f),
                    new Vector3(.77f, .6f, .15f), RehabSurface.LinenFabric);
            }
            Block("Accent pillow", new Vector3(-3.9f, 1.1f, 2.83f),
                new Vector3(.48f, .49f, .16f), new Color(.73f, .58f, .39f));
            Window(2.65f, 2.9f, 2.85f, 2.35f);
            Block("Window sill", new Vector3(2.65f, 1.7f, 4.38f),
                new Vector3(3.14f, .1f, .33f), RehabSurface.Quartz);
            foreach (var x in new[] { 1.02f, 4.27f })
                Block("Soft curtain", new Vector3(x, 2.85f, 4.32f),
                    new Vector3(.23f, 2.55f, .18f), RehabSurface.LinenFabric);
            Artwork(-.2f, 3.42f);
            Plant(new Vector3(4.75f, 0, 2.65f));
            FloorLamp(new Vector3(-5.1f, 0, 2.6f));
        }

        private void BuildKitchen()
        {
            Block("Quartz worktop", new Vector3(-.5f, 1.13f, 3.87f),
                new Vector3(10.5f, .13f, 1.35f), RehabSurface.Quartz);
            Block("Tiled backsplash", new Vector3(-.5f, 2.06f, 4.55f),
                new Vector3(10.5f, 1.37f, .07f), RehabSurface.Ceramic);
            for (var i = 0; i < 5; i++)
            {
                var x = -4.48f + i * 2f;
                Block("Lower cabinet carcass", new Vector3(x, .55f, 3.94f),
                    new Vector3(1.9f, 1.03f, 1.14f), RehabSurface.PaintedWood);
                Block("Lower cabinet inset door", new Vector3(x, .55f, 3.34f),
                    new Vector3(1.72f, .88f, .035f), RehabSurface.PaintedWood);
                Block("Lower door reveal", new Vector3(x, .55f, 3.313f),
                    new Vector3(1.52f, .69f, .012f), new Color(.5f, .61f, .58f));
                Block("Lower door pull", new Vector3(x + .55f, .58f, 3.28f),
                    new Vector3(.28f, .035f, .05f), RehabSurface.BrushedMetal);
                Block("Upper cabinet carcass", new Vector3(x, 3.32f, 4.1f),
                    new Vector3(1.9f, 1.22f, .92f), RehabSurface.PaintedWood);
                Block("Upper cabinet door", new Vector3(x, 3.32f, 3.61f),
                    new Vector3(1.74f, 1.06f, .035f), RehabSurface.PaintedWood);
                Block("Upper pull", new Vector3(x + .55f, 3.0f, 3.57f),
                    new Vector3(.27f, .035f, .05f), RehabSurface.BrushedMetal);
            }
            Block("Sink basin", new Vector3(.42f, 1.204f, 3.9f),
                new Vector3(1.05f, .018f, .72f), RehabSurface.DarkMetal);
            Block("Sink rim", new Vector3(.42f, 1.218f, 3.9f),
                new Vector3(1.17f, .016f, .8f), RehabSurface.BrushedMetal);
            Block("Sink opening", new Vector3(.42f, 1.23f, 3.9f),
                new Vector3(.98f, .008f, .63f), RehabSurface.DarkMetal);
            Cylinder("Faucet stem", new Vector3(.45f, 1.42f, 4.29f),
                new Vector3(.04f, .22f, .04f), RehabSurface.BrushedMetal);
            Cylinder("Faucet spout", new Vector3(.45f, 1.65f, 4.1f),
                new Vector3(.035f, .24f, .035f), RehabSurface.BrushedMetal)
                .transform.rotation = Quaternion.Euler(90, 0, 0);
            Block("Hob", new Vector3(-2.7f, 1.205f, 3.82f),
                new Vector3(1.22f, .016f, .81f), RehabSurface.DarkMetal);
            foreach (var x in new[] { -3.05f, -2.38f })
                foreach (var z in new[] { 3.59f, 4.05f })
                    Cylinder("Burner", new Vector3(x, 1.228f, z),
                        new Vector3(.13f, .013f, .13f), RehabSurface.BrushedMetal);
            Block("Open walnut shelf", new Vector3(3.55f, 2.27f, 3.96f),
                new Vector3(1.4f, .075f, .48f), RehabSurface.Walnut);
            for (var i = 0; i < 3; i++)
            {
                Cylinder("Pantry jar", new Vector3(3.05f + i * .46f, 2.46f, 3.95f),
                    new Vector3(.12f, .18f, .12f), RehabSurface.Glass);
                Cylinder("Jar lid", new Vector3(3.05f + i * .46f, 2.66f, 3.95f),
                    new Vector3(.13f, .025f, .13f), RehabSurface.DarkMetal);
            }
            Plant(new Vector3(4.6f, 1.2f, 3.82f));
            Pendant(new Vector3(-1.75f, 3.7f, 1.5f));
            Pendant(new Vector3(1.75f, 3.7f, 1.5f));
        }

        private void Window(float x, float y, float width, float height)
        {
            Block("Daylight view", new Vector3(x, y, 4.48f),
                new Vector3(width, height, .035f), new Color(.52f, .72f, .82f));
            Block("Distant landscape", new Vector3(x, y - height * .31f, 4.45f),
                new Vector3(width, height * .26f, .025f), new Color(.4f, .57f, .48f));
            foreach (var side in new[] { -1f, 1f })
            {
                Block("Window frame vertical", new Vector3(x + side * width * .5f, y, 4.36f),
                    new Vector3(.09f, height + .1f, .13f), RehabSurface.PaintedWood);
                Block("Window frame horizontal", new Vector3(x, y + side * height * .5f, 4.36f),
                    new Vector3(width + .09f, .09f, .13f), RehabSurface.PaintedWood);
            }
            Block("Window mullion", new Vector3(x, y, 4.34f),
                new Vector3(.07f, height, .1f), RehabSurface.PaintedWood);
            Block("Window transom", new Vector3(x, y + .35f, 4.34f),
                new Vector3(width, .065f, .1f), RehabSurface.PaintedWood);
        }

        private void Artwork(float x, float y)
        {
            Block("Framed art walnut", new Vector3(x, y, 4.46f),
                new Vector3(1.25f, 1.04f, .085f), RehabSurface.Walnut);
            Block("Art mount", new Vector3(x, y, 4.405f),
                new Vector3(1.08f, .87f, .01f), new Color(.9f, .86f, .77f));
            Block("Abstract landscape", new Vector3(x, y, 4.394f),
                new Vector3(.92f, .71f, .012f), new Color(.56f, .68f, .64f));
            Block("Art horizon", new Vector3(x, y - .15f, 4.382f),
                new Vector3(.92f, .13f, .006f), new Color(.72f, .57f, .43f));
        }

        private void Plant(Vector3 origin)
        {
            Cylinder("Ceramic planter", origin + new Vector3(0, .34f, 0),
                new Vector3(.34f, .34f, .34f), RehabSurface.Ceramic);
            Cylinder("Planter rim", origin + new Vector3(0, .69f, 0),
                new Vector3(.37f, .045f, .37f), RehabSurface.Quartz);
            Cylinder("Plant stem", origin + new Vector3(0, 1.12f, 0),
                new Vector3(.035f, .49f, .035f), RehabSurface.Walnut);
            for (var i = 0; i < 9; i++)
            {
                var angle = i * 2.39996f;
                var radius = .2f + .12f * (i % 3);
                var leaf = Sphere("Variegated leaf", origin + new Vector3(Mathf.Cos(angle) * radius,
                    1.08f + .13f * (i % 4), Mathf.Sin(angle) * radius),
                    new Vector3(.22f, .34f, .13f),
                    i % 3 == 0 ? new Color(.27f, .46f, .29f) : new Color(.19f, .38f, .26f));
                leaf.transform.rotation = Quaternion.Euler(20, i * 137f, 20);
            }
        }

        private void FloorLamp(Vector3 origin)
        {
            Cylinder("Lamp foot", origin + new Vector3(0, .045f, 0),
                new Vector3(.27f, .045f, .27f), RehabSurface.DarkMetal);
            Cylinder("Lamp stem", origin + new Vector3(0, 1.08f, 0),
                new Vector3(.025f, 1.05f, .025f), RehabSurface.BrushedMetal);
            Cylinder("Warm fabric shade", origin + new Vector3(0, 2.1f, 0),
                new Vector3(.38f, .3f, .38f), new Color(.92f, .84f, .69f));
            PointLight("Lamp glow", origin + new Vector3(0, 2.08f, 0), 2.4f, 7.5f,
                new Color(1f, .79f, .58f));
        }

        private void Pendant(Vector3 position)
        {
            Cylinder("Pendant cord", position + new Vector3(0, 1.05f, 0),
                new Vector3(.012f, .78f, .012f), RehabSurface.DarkMetal);
            Cylinder("Pendant shade", position, new Vector3(.38f, .14f, .38f), RehabSurface.DarkMetal);
            Sphere("Frosted bulb", position + new Vector3(0, -.18f, 0),
                new Vector3(.18f, .18f, .18f), new Color(1f, .92f, .73f));
            PointLight("Pendant warm pool", position + new Vector3(0, -.2f, 0),
                2.4f, 7f, new Color(1f, .88f, .7f));
        }

        private void Lighting(RoomKind room)
        {
            var sun = new GameObject("Soft afternoon daylight").AddComponent<Light>();
            sun.transform.SetParent(_root.transform);
            sun.type = LightType.Directional;
            sun.intensity = room == RoomKind.LivingRoom ? 1.45f : 1.25f;
            sun.color = new Color(1f, .94f, .85f);
            sun.transform.rotation = Quaternion.Euler(51f, -36f, 0);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = .62f;
            sun.shadowBias = .04f;
            sun.shadowNormalBias = .35f;
            PointLight("Large window bounce", new Vector3(2.6f, 3.2f, 2.7f), 3.2f, 9f,
                new Color(.76f, .86f, 1f));
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.68f, .73f, .78f);
            RenderSettings.ambientEquatorColor = new Color(.55f, .58f, .58f);
            RenderSettings.ambientGroundColor = new Color(.34f, .32f, .29f);
            RenderSettings.ambientIntensity = 1f;
        }

        private void PointLight(string name, Vector3 position, float intensity, float range, Color color)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(_root.transform);
            light.transform.position = position;
            light.type = LightType.Point;
            light.intensity = intensity;
            light.range = range;
            light.color = color;
        }

        private GameObject Block(string name, Vector3 position, Vector3 scale, RehabSurface surface)
        {
            var obj = Primitive(PrimitiveType.Cube, name, position, scale);
            RehabMaterials.Apply(obj.GetComponent<Renderer>(), surface);
            return obj;
        }

        private GameObject Block(string name, Vector3 position, Vector3 scale, Color color)
        {
            var obj = Primitive(PrimitiveType.Cube, name, position, scale);
            RehabMaterials.Apply(obj.GetComponent<Renderer>(), color);
            return obj;
        }

        private GameObject Cylinder(string name, Vector3 position, Vector3 scale, RehabSurface surface)
        {
            var obj = Primitive(PrimitiveType.Cylinder, name, position, scale);
            RehabMaterials.Apply(obj.GetComponent<Renderer>(), surface);
            return obj;
        }

        private GameObject Cylinder(string name, Vector3 position, Vector3 scale, Color color)
        {
            var obj = Primitive(PrimitiveType.Cylinder, name, position, scale);
            RehabMaterials.Apply(obj.GetComponent<Renderer>(), color);
            return obj;
        }

        private GameObject Sphere(string name, Vector3 position, Vector3 scale, Color color)
        {
            var obj = Primitive(PrimitiveType.Sphere, name, position, scale);
            RehabMaterials.Apply(obj.GetComponent<Renderer>(), color);
            return obj;
        }

        private GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(_root.transform);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            return obj;
        }
    }
}
