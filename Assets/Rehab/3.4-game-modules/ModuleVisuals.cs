using UnityEngine;

namespace GripRehab
{
    // Small self-contained 3D set pieces so every training mode has a spatial cue in the room.
    [System.Serializable]
    public sealed class ModuleVisuals
    {
        private GameObject _root;
        [SerializeField] private GameObject[] _modules;
        [SerializeField] private GameObject _authoredMazeAvatar;
        [SerializeField] private GameObject[] _authoredPieces;
        private Vector3[] _initialPiecePositions;
        public GameObject[] Modules => _modules;
#if UNITY_EDITOR
        public void AuthorModules()
        {
            var modules=new GameObject[5];
            for(int i=0;i<5;i++)
            {
                Show((TrainingMode)i,new MazeTask()); modules[i]=_root; _root=null;
                if(i==2) _authoredPieces=(GameObject[])_pieces.Clone();
                if(i==4) _authoredMazeAvatar=_mazeAvatar;
            }
            _modules=modules; Show(TrainingMode.Force,new MazeTask());
        }
#endif
        private GameObject _mazeAvatar;
        private readonly GameObject[] _pieces = new GameObject[4];

        public void Show(TrainingMode mode, MazeTask maze)
        {
            if (_modules != null && _modules.Length==5)
            {
                for(int i=0;i<5;i++) _modules[i].SetActive(i==(int)mode);
                _mazeAvatar=_authoredMazeAvatar;
                if(_initialPiecePositions==null)
                {
                    _initialPiecePositions=new Vector3[4];
                    for(int i=0;i<4;i++) _initialPiecePositions[i]=_authoredPieces[i].transform.position;
                }
                for(int i=0;i<4;i++) { _pieces[i]=_authoredPieces[i]; _pieces[i].transform.position=_initialPiecePositions[i]; }
                UpdateMaze(0,0);
                return;
            }
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject($"3.4 {mode} scene objects");
            _mazeAvatar = null;
            for (var i = 0; i < _pieces.Length; i++) _pieces[i] = null;
            switch (mode)
            {
                case TrainingMode.Memory:
                    SpawnSurface(PrimitiveType.Cube, "Sequence pedestal", new Vector3(0, .69f, .28f),
                        new Vector3(.85f, .09f, .72f), RehabSurface.BrushedMetal);
                    Spawn(PrimitiveType.Capsule, "Sequence avatar", new Vector3(0, .92f, .28f),
                        new Vector3(.36f, .26f, .36f), new Color(.93f, .62f, .25f));
                    Spawn(PrimitiveType.Sphere, "Avatar head", new Vector3(0, 1.35f, .28f),
                        new Vector3(.4f, .4f, .4f), new Color(.98f, .79f, .61f));
                    SpawnSurface(PrimitiveType.Cube, "Avatar torso", new Vector3(0, 1.02f, .28f),
                        new Vector3(.34f, .35f, .22f), RehabSurface.PaintedWood);
                    break;
                case TrainingMode.Assembly:
                    SpawnSurface(PrimitiveType.Cube, "Assembly board", new Vector3(0, .68f, .35f),
                        new Vector3(2.86f, .07f, 1.22f), RehabSurface.Walnut);
                    for (var slot = 0; slot < 4; slot++)
                        SpawnSurface(PrimitiveType.Cylinder, $"Assembly socket {slot + 1}",
                            new Vector3(-1.1f + slot * .73f, .74f, .65f),
                            new Vector3(.23f, .012f, .23f), RehabSurface.DarkMetal);
                    var colors = new[] { new Color(.97f, .65f, .3f), new Color(.34f, .73f, .82f),
                        new Color(.6f, .82f, .47f), new Color(.72f, .55f, .88f) };
                    for (var i = 0; i < 4; i++)
                        _pieces[i] = Spawn(PrimitiveType.Cube, $"Assembly piece {i + 1}",
                            new Vector3(-1.1f + i * .73f, .81f, -.3f), new Vector3(.43f, .3f, .43f), colors[i]);
                    break;
                case TrainingMode.Tracing:
                    SpawnSurface(PrimitiveType.Cube, "Tracing board", new Vector3(0, .68f, .18f),
                        new Vector3(2.9f, .07f, 1.18f), RehabSurface.Walnut);
                    for (var point = 0; point < 5; point++)
                        SpawnSurface(PrimitiveType.Cylinder, $"Tracing marker {point + 1}",
                            new Vector3(-1.14f + point * .57f, .75f,
                                point % 2 == 0 ? -.05f : .37f),
                            new Vector3(.11f, .015f, .11f), RehabSurface.BrushedMetal);
                    break;
                case TrainingMode.Maze:
                    SpawnSurface(PrimitiveType.Cube, "Maze tray", new Vector3(0, .68f, .23f),
                        new Vector3(2.9f, .07f, 1.83f), RehabSurface.Walnut);
                    for (var y = 0; y < maze.Rows; y++)
                        for (var x = 0; x < maze.Columns; x++)
                            if (maze.IsWall(x, y))
                                Spawn(PrimitiveType.Cube, $"Maze wall {x},{y}", TilePosition(x, y),
                                    new Vector3(.32f, .25f, .22f), new Color(.24f, .33f, .42f));
                    Spawn(PrimitiveType.Cylinder, "Maze goal", TilePosition(maze.Columns - 1, maze.Rows - 1),
                        new Vector3(.15f, .07f, .15f), new Color(.34f, .88f, .6f));
                    _mazeAvatar = Spawn(PrimitiveType.Sphere, "Maze avatar", TilePosition(0, 0) + Vector3.up * .18f,
                        new Vector3(.18f, .18f, .18f), new Color(.96f, .64f, .28f));
                    break;
            }
        }

        public void UpdateMaze(int x, int y)
        {
            if (_mazeAvatar != null) _mazeAvatar.transform.position = TilePosition(x, y) + Vector3.up * .18f;
        }

        public void UpdateAssembly(int placed, int piece)
        {
            if (piece < 0 || piece >= _pieces.Length || _pieces[piece] == null) return;
            _pieces[piece].transform.position = new Vector3(-1.1f + (placed - 1) * .73f, .81f, .65f);
        }

        private static Vector3 TilePosition(int x, int y) =>
            new Vector3(-1.12f + x * .37f, .81f, -.38f + y * .29f);

        private GameObject SpawnSurface(PrimitiveType type, string name, Vector3 position,
            Vector3 scale, RehabSurface surface)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(_root.transform);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            RehabMaterials.Apply(obj.GetComponent<Renderer>(), surface);
            return obj;
        }

        private GameObject Spawn(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(_root.transform);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            RehabMaterials.Apply(obj.GetComponent<Renderer>(), color);
            return obj;
        }
    }
}
