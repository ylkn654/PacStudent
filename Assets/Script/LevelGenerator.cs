using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// 关卡生成器：根据 levelMap 数组生成墙、路、豆子，并提供格子坐标查询给其他脚本
public class LevelGenerator : MonoBehaviour
{

    // 一组按方向区分的预制体（直墙 / 拐角）
    [Serializable]
    public class DirectionalPrefabs
    {
        [Header("Straight")]
        [Tooltip("│ Connects up and down")] public GameObject NS;
        [Tooltip("─ Connects left and right")] public GameObject EW;

        [Header("Corners")]
        [Tooltip("└ Connects up and right")] public GameObject NE;
        [Tooltip("┘ Connects up and left")] public GameObject NW;
        [Tooltip("┌ Connects down and right")] public GameObject SE;
        [Tooltip("┐ Connects down and left")] public GameObject SW;

        [Header("T-junctions")]
        [Tooltip("├ Connects up, down and right")] public GameObject NSE;
        [Tooltip("┤ Connects up, down and left")] public GameObject NSW;
        [Tooltip("┴ Connects up, right and left")] public GameObject NEW;
        [Tooltip("┬ Connects down, right and left")] public GameObject SEW;

        [Header("Crossroads")]
        [Tooltip("┼ Connects all four sides")] public GameObject NSEW;

        public GameObject Get(string key)
        {
            switch (key)
            {
                case "NS": return NS;
                case "EW": return EW;
                case "NE": return NE;
                case "NW": return NW;
                case "SE": return SE;
                case "SW": return SW;
                case "NSE": return NSE;
                case "NSW": return NSW;
                case "NEW": return NEW;
                case "SEW": return SEW;
                case "NSEW": return NSEW;
                default: return null;
            }
        }
    }

    [Serializable]
    public class OuterWallPrefabs
    {
        [Header("Straight walls")]
        [Tooltip("─ Top-side wall (walkable area is below it)")] public GameObject WallN;
        [Tooltip("─ Bottom-side wall (walkable area is above it)")] public GameObject WallS;
        [Tooltip("│ Right-side wall (walkable area is to its left)")] public GameObject WallE;
        [Tooltip("│ Left-side wall (walkable area is to its right)")] public GameObject WallW;

        [Header("Corners")]
        [Tooltip("┌ Top-left corner, joins the top and left walls")] public GameObject WallNW;
        [Tooltip("┐ Top-right corner, joins the top and right walls")] public GameObject WallNE;
        [Tooltip("└ Bottom-left corner, joins the bottom and left walls")] public GameObject WallSW;
        [Tooltip("┘ Bottom-right corner, joins the bottom and right walls")] public GameObject WallSE;
    }

    // 原始地图数据（开启 mirrorMap 时会镜像成完整地图）
    private readonly int[,] levelMap =
    {
        {1,2,2,2,2,2,2,2,2,2,2,2,2,7},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,4},
        {2,6,4,0,0,4,5,4,0,0,0,4,5,4},
        {2,5,3,4,4,3,5,3,4,4,4,3,5,3},
        {2,5,5,5,5,5,5,5,5,5,5,5,5,5},
        {2,5,3,4,4,3,5,3,3,5,3,4,4,4},
        {2,5,3,4,4,3,5,4,4,5,3,4,4,3},
        {2,5,5,5,5,5,5,4,4,5,5,5,5,4},
        {1,2,2,2,2,1,5,4,3,4,4,3,0,4},
        {0,0,0,0,0,2,5,4,3,4,4,3,0,3},
        {0,0,0,0,0,2,5,4,4,0,0,0,0,0},
        {0,0,0,0,0,2,5,4,4,0,3,4,4,8},
        {2,2,2,2,2,1,5,3,3,0,4,0,0,0},
        {0,0,0,0,0,0,5,0,0,0,4,0,0,0},
    };

    // 地图里每个数字代表的含义
    private const int Empty = 0, OuterCorner = 1, OuterWall = 2, InnerCorner = 3, InnerWall = 4,
                      Pellet = 5, PowerPellet = 6, TJunction = 7, GhostGate = 8;

    [Header("Road (map values 0 / 5 / 6)")]
    public DirectionalPrefabs road = new DirectionalPrefabs();

    [Header("River - inner walls (map values 3 / 4)")]
    public DirectionalPrefabs river = new DirectionalPrefabs();

    [Header("Outer walls (map values 1 / 2 / 7)")]
    public OuterWallPrefabs outerWall = new OuterWallPrefabs();

    [Header("Others")]
    [Tooltip("Non-walkable 0 outside the outer walls")] public GameObject wall;
    [Tooltip("Stacked on the Road of a 5")] public GameObject coin;
    [Tooltip("Stacked on the Road of a 6")] public GameObject sword;
    [Tooltip("Map value 8, ghost exit gate")] public GameObject bridge;

    [Header("Layout")]
    [SerializeField] private float cellSize = 1f;
    [Tooltip("On: lay the map on the XZ plane (3D top-down). Off: XY plane (2D)")]
    [SerializeField] private bool useXZPlane = false;
    [Tooltip("Offset of Coin / Sword relative to the Road. In 2D use a negative z to draw it above the Road; in 3D use a positive y")]
    [SerializeField] private Vector3 itemOffset = new Vector3(0f, 0f, -0.1f);
    [Tooltip("On: the center of the map is placed at this GameObject's position. Off: the top-left cell is placed there")]
    [SerializeField] private bool centerMap = true;
    [Tooltip("Extra offset applied to the whole map, relative to this GameObject")]
    [SerializeField] private Vector3 mapOffset = Vector3.zero;

    [Header("Mirroring")]
    [Tooltip("Mirror the top-left quarter into the full map")]
    [SerializeField] private bool mirrorMap = true;
    [Tooltip("Duplicate the last row when mirroring vertically. Keep this OFF: the last row of levelMap is the middle row of the level")]
    [SerializeField] private bool duplicateMiddleRow = false;
    [Tooltip("Duplicate the last column when mirroring horizontally. In the original Pac-Man it is duplicated, making the middle wall and ghost gate two tiles wide")]
    [SerializeField] private bool duplicateMiddleColumn = true;

    [Header("Rules")]
    [Tooltip("Treat outside the map bounds as connected (used by the side tunnels)")]
    [SerializeField] private bool edgeIsOpen = true;
    [Tooltip("Treat the ghost gate (8) as walkable, so 0s inside the ghost house become Road")]
    [SerializeField] private bool gateIsPassable = true;
    [Tooltip("Inner wall shape fix: straight walls (4) use one axis and corners (3) use two sides, so adjacent parallel walls are not joined by mistake")]
    [SerializeField] private bool smartRiverShape = true;
    [SerializeField] private bool generateOnStart = true;

    [Header("Sorting")]
    [SerializeField] private int itemSortingOrder = 2;

    // 运行时数据
    private int[,] map;         // Full map after mirroring
    private int rows, cols;
    private bool[,] wk;   // Walkable cells inside the outer walls
    private Transform root2;
    private bool flag;     // Only check slots, do not spawn
    private readonly SortedSet<string> s1 = new SortedSet<string>();
    private readonly SortedSet<string> s2 = new SortedSet<string>();

    // Direction order is always N S E W
    // 方向：上 下 右 左
    private const int DirN = 0, DirS = 1, DirE = 2, DirW = 3;
    private static readonly int[] DRow = { -1, 1, 0, 0 };
    private static readonly int[] DCol = { 0, 0, 1, -1 };
    private static readonly char[] DirChar = { 'N', 'S', 'E', 'W' };

    public static LevelGenerator Instance { get; private set; }

    // 初始化单例，开局生成地图
    private void Awake()
    {
        Instance = this;

        // The hand-made map is replaced by the generated one
        GameObject manual = GameObject.Find("ManualMap");
        if (manual != null) Destroy(manual);

        if (generateOnStart)
        {
            // Hide the old copy right away so nothing finds its pellets before Destroy() runs
            Transform old = transform.Find("GeneratedMap");
            if (old != null) old.gameObject.SetActive(false);
            Generate();
        }
        else
        {
            EnsureInit();
        }
    }

    [ContextMenu("Generate Map")]
    // 生成整张地图
    public void Generate()
    {
        flag = false;
        s2.Clear();
        Init();
        PrepareRoot();
        BuildAll();

        if (s2.Count > 0)
            Debug.LogWarning("[LevelGenerator] These slots have no prefab assigned; their cells were skipped:\n" + string.Join("\n", s2));
    }

    [ContextMenu("Check Prefab Slots")]
    // 只检查预制体有没有拖全，不生成
    public void CheckPrefabSlots()
    {
        flag = true;
        s1.Clear();
        s2.Clear();
        Init();
        BuildAll();
        flag = false;

        Debug.Log("[LevelGenerator] Slots used by this map:\n" + string.Join("\n", s1));
        if (s2.Count > 0)
            Debug.LogWarning("[LevelGenerator] Empty slots:\n" + string.Join("\n", s2));
        else
            Debug.Log("[LevelGenerator] All used slots are assigned.");
    }

    // 准备地图数据和可走区域
    private void Init()
    {
        map = BuildFullMap();
        rows = map.GetLength(0);
        cols = map.GetLength(1);
        FloodFillWalkable();
        BuildGameplayGrid();
    }

    // 把原始地图镜像成完整地图
    private int[,] BuildFullMap()
    {
        int qRows = levelMap.GetLength(0);
        int qCols = levelMap.GetLength(1);

        if (!mirrorMap) return (int[,])levelMap.Clone();

        int fullRows = duplicateMiddleRow ? qRows * 2 : qRows * 2 - 1;
        int fullCols = duplicateMiddleColumn ? qCols * 2 : qCols * 2 - 1;
        var full = new int[fullRows, fullCols];

        for (int r = 0; r < fullRows; r++)
        {
            int srcR = r < qRows ? r : fullRows - 1 - r;       // Bottom half: flip vertically
            for (int c = 0; c < fullCols; c++)
            {
                int srcC = c < qCols ? c : fullCols - 1 - c;   // Right half: flip horizontally
                full[r, c] = levelMap[srcR, srcC];
            }
        }

        return full;
    }

    // 逐格生成
    private void BuildAll()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                BuildCell(r, c);
    }

    // 根据格子类型生成对应物体
    private void BuildCell(int r, int c)
    {
        int v = map[r, c];
        switch (v)
        {
            case Empty:
                if (wk[r, c]) SpawnRoad(r, c);
                else Spawn(wall, "Others / Wall", r, c);
                break;

            case Pellet:
                SpawnRoad(r, c);
                SetupItem(Spawn(coin, "Others / Coin", r, c, itemOffset), false);
                break;

            case PowerPellet:
                SpawnRoad(r, c);
                SetupItem(Spawn(sword, "Others / Sword", r, c, itemOffset), true);
                break;

            case OuterCorner:
            case OuterWall:
            case TJunction: // 7 is treated as an outer wall (direction judged as a straight wall)
                SpawnOuterWall(r, c, v == OuterCorner);
                break;

            case InnerCorner:
            case InnerWall:
                SpawnRiver(r, c, v == InnerCorner);
                break;

            case GhostGate:
                Spawn(bridge, "Others / Bridge", r, c);
                break;
        }
    }

    // 生成路面
    private void SpawnRoad(int r, int c)
    {
        string key = Suffix(GetLinks(r, c, IsRoad));
        Spawn(road.Get(key), "Road / " + key, r, c);
    }

    // 生成内墙（河道）
    private void SpawnRiver(int r, int c, bool isCorner)
    {
        bool[] links = GetLinks(r, c, IsRiver);
        string key = null;

        if (smartRiverShape)
        {
            if (isCorner)
            {
                if (TryPickCorner(r, c, links, IsRiver, out int v, out int h))
                    key = DirChar[v].ToString() + DirChar[h];
            }
            else
            {
                key = PickAxis(links);
            }
        }

        // Basic rule: append a letter for each side connected to another inner wall
        if (key == null) key = Suffix(links);

        Spawn(river.Get(key), "River / " + key, r, c);
    }

    // 生成外墙
    private void SpawnOuterWall(int r, int c, bool isCorner)
    {
        bool[] links = GetLinks(r, c, IsOuter);

        if (isCorner)
        {
            if (!TryPickCorner(r, c, links, IsOuter, out int v, out int h))
            {
                // If it still can't be decided, guess by quadrant: a top-left corner extends down and right
                v = r < rows / 2 ? DirS : DirN;
                h = c < cols / 2 ? DirE : DirW;
            }

            // Is the wk area inside the bend (the diagonal between the two arms) or outside it?
            bool inside = WalkableAt(r + DRow[v], c + DCol[h]);

            // WallNW / WallNE art: dirt strip down one side (walkable area inside the bend).
            // WallSW / WallSE art: dirt fill with a notch (walkable area outside the bend).
            // The art only exists for one vertical orientation, so the other one is flipped upside down.
            GameObject prefab;
            bool flipY;
            if (inside)
            {
                prefab = h == DirE ? outerWall.WallNW : outerWall.WallNE;
                flipY = v == DirN;
            }
            else
            {
                prefab = h == DirE ? outerWall.WallSW : outerWall.WallSE;
                flipY = v == DirS;
            }

            string slot = "Outer wall / corner " + DirChar[v] + DirChar[h] + (inside ? " inside" : " outside");
            GameObject corner = Spawn(prefab, slot, r, c);
            if (corner != null && flipY)
            {
                Vector3 s = corner.transform.localScale;
                corner.transform.localScale = new Vector3(s.x, -s.y, s.z);
            }
            return;
        }

        string axis = PickAxis(links);
        if (axis == null)
            axis = (WalkableAt(r - 1, c) || WalkableAt(r + 1, c)) ? "EW" : "NS";

        if (axis == "EW")
        {
            // Horizontal wall: wk area below -> top-side WallN; above -> bottom-side WallS
            bool southIn = WalkableAt(r + 1, c), northIn = WalkableAt(r - 1, c);
            bool isNorth = (southIn != northIn) ? southIn : r < rows / 2;
            if (isNorth) Spawn(outerWall.WallN, "Outer wall / WallN ─", r, c);
            else Spawn(outerWall.WallS, "Outer wall / WallS ─", r, c);
        }
        else
        {
            // Vertical wall: wk area to the right -> left-side WallW; to the left -> right-side WallE
            bool eastIn = WalkableAt(r, c + 1), westIn = WalkableAt(r, c - 1);
            bool isWest = (eastIn != westIn) ? eastIn : c < cols / 2;
            if (isWest) Spawn(outerWall.WallW, "Outer wall / WallW │", r, c);
            else Spawn(outerWall.WallE, "Outer wall / WallE │", r, c);
        }
    }

    // 这个数字能不能走
    private bool IsPassableValue(int v)
    {
        return v == Empty || v == Pellet || v == PowerPellet || (v == GhostGate && gateIsPassable);
    }

    // 从有豆子的格子开始洪水填充，算出外墙以内能走的格子
    private void FloodFillWalkable()
    {
        wk = new bool[rows, cols];
        var queue = new Queue<Vector2Int>();

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (map[r, c] == Pellet || map[r, c] == PowerPellet)
                {
                    wk[r, c] = true;
                    queue.Enqueue(new Vector2Int(c, r));
                }

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nr = p.y + DRow[d], nc = p.x + DCol[d];
                if (!InBounds(nr, nc) || wk[nr, nc]) continue;
                if (!IsPassableValue(map[nr, nc])) continue;
                wk[nr, nc] = true;
                queue.Enqueue(new Vector2Int(nc, nr));
            }
        }
    }

    // ---- 判断格子类型的小工具 ----
    private bool InBounds(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;
    private bool WalkableAt(int r, int c) => InBounds(r, c) && wk[r, c];

    private bool IsRoad(int r, int c) => wk[r, c];

    private bool IsRiver(int r, int c)
    {
        int v = map[r, c];
        return v == InnerCorner || v == InnerWall || v == TJunction || v == GhostGate;
    }

    private bool IsOuter(int r, int c)
    {
        int v = map[r, c];
        return v == OuterCorner || v == OuterWall || v == TJunction;
    }

    // 看四个方向上哪些格子是同类，用来选墙的形状
    private bool[] GetLinks(int r, int c, Func<int, int, bool> sameKind)
    {
        var links = new bool[4];
        for (int d = 0; d < 4; d++)
        {
            int nr = r + DRow[d], nc = c + DCol[d];
            links[d] = InBounds(nr, nc) ? sameKind(nr, nc) : edgeIsOpen;
        }
        return links;
    }

    // 根据连接方向得到预制体名字的后缀
    private static string Suffix(bool[] links)
    {
        var sb = new StringBuilder();
        for (int d = 0; d < 4; d++)
            if (links[d]) sb.Append(DirChar[d]);
        return sb.ToString();
    }

    private static string PickAxis(bool[] links)
    {
        int ns = (links[DirN] ? 1 : 0) + (links[DirS] ? 1 : 0);
        int ew = (links[DirE] ? 1 : 0) + (links[DirW] ? 1 : 0);
        if (ns > ew) return "NS";
        if (ew > ns) return "EW";
        return null;
    }

    private bool TryPickCorner(int r, int c, bool[] links, Func<int, int, bool> sameKind,
                               out int vertical, out int horizontal)
    {
        vertical = -1;
        horizontal = -1;
        int bestScore = -1;

        foreach (int v in new[] { DirN, DirS })
            foreach (int h in new[] { DirE, DirW })
            {
                if (!links[v] || !links[h]) continue;

                int dr = DRow[v], dc = DCol[h];
                int score = 0;
                if (IsOpenCell(r + dr, c + dc, sameKind)) score += 2;
                if (IsOpenCell(r - dr, c - dc, sameKind)) score += 1;

                if (score > bestScore)
                {
                    bestScore = score;
                    vertical = v;
                    horizontal = h;
                }
            }

        return vertical >= 0;
    }

    private bool IsOpenCell(int r, int c, Func<int, int, bool> sameKind)
    {
        return InBounds(r, c) && !sameKind(r, c);
    }

    // 创建放地图物体的父节点
    private void PrepareRoot()
    {
        Transform old = transform.Find("GeneratedMap");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        root2 = new GameObject("GeneratedMap").transform;
        root2.SetParent(transform, false);
    }

    // 格子坐标转本地坐标
    private Vector3 CellToLocal(int r, int c)
    {
        // Row 0 is at the top, column 0 is at the far left
        float x = c * cellSize;
        float down = r * cellSize;

        if (centerMap)
        {
            // Shift so the middle of the map sits at this GameObject's position
            x -= (cols - 1) * cellSize * 0.5f;
            down -= (rows - 1) * cellSize * 0.5f;
        }

        Vector3 pos = useXZPlane
            ? new Vector3(x, 0f, -down)
            : new Vector3(x, -down, 0f);

        return pos + mapOffset;
    }

    // 生成一个预制体放到格子上
    private GameObject Spawn(GameObject prefab, string slotName, int r, int c, Vector3 extraOffset = default)
    {
        if (flag) s1.Add(slotName);

        if (prefab == null)
        {
            s2.Add(slotName);
            return null;
        }

        if (flag) return null;

        GameObject go = Instantiate(prefab, root2);
        go.transform.localPosition = CellToLocal(r, c) + extraOffset;
        go.name = $"{prefab.name} ({r},{c})";
        return go;
    }

    // 设置豆子（排序、碰撞体、Pellet 组件）
    private void SetupItem(GameObject go, bool isPower)
    {
        if (go == null) return;

        foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>())
            sr.sortingOrder = itemSortingOrder;

        var pellet = go.GetComponent<Pellet>();
        if (pellet == null) pellet = go.AddComponent<Pellet>();
        pellet.isPowerPellet = isPower;

        var col = go.GetComponent<CircleCollider2D>();
        if (col == null) col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.2f;
    }

    // Cells are Vector2Int(column, row). Row 0 is the top row, column 0 the far left.

    private bool[,] pacWalkable;   // Walkable for PacStudent: corridors and tunnels, never the ghost house or its gates
    private bool[,] ghostHouse;    // Ghost spawn area: inside the house plus its gates

    // ---- 给吃豆人和鬼用的公开接口 ----
    public int Rows { get { EnsureInit(); return rows; } }
    public int Cols { get { EnsureInit(); return cols; } }

    public void EnsureInit()
    {
        if (map == null) Init();
    }

    // 生成游戏用的格子数据（哪里能走、哪里是鬼屋）
    private void BuildGameplayGrid()
    {
        // Flood fill again, this time with the ghost gate treated as a wall
        pacWalkable = new bool[rows, cols];
        var queue = new Queue<Vector2Int>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (map[r, c] == Pellet || map[r, c] == PowerPellet)
                {
                    pacWalkable[r, c] = true;
                    queue.Enqueue(new Vector2Int(c, r));
                }

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nr = p.y + DRow[d], nc = p.x + DCol[d];
                if (!InBounds(nr, nc) || pacWalkable[nr, nc]) continue;
                int v = map[nr, nc];
                if (v != Empty && v != Pellet && v != PowerPellet) continue;
                pacWalkable[nr, nc] = true;
                queue.Enqueue(new Vector2Int(nc, nr));
            }
        }

        // Ghost house = the gates + every empty cell that can only be reached through a gate
        ghostHouse = new bool[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (map[r, c] == GhostGate) FillHouse(r, c);
    }

    // 标记鬼屋格子
    private void FillHouse(int r, int c)
    {
        if (!InBounds(r, c) || ghostHouse[r, c] || pacWalkable[r, c]) return;
        int v = map[r, c];
        if (v != GhostGate && v != Empty) return;
        ghostHouse[r, c] = true;
        for (int d = 0; d < 4; d++) FillHouse(r + DRow[d], c + DCol[d]);
    }

    public bool InBounds(Vector2Int cell) { EnsureInit(); return InBounds(cell.y, cell.x); }

    public bool IsWalkable(Vector2Int cell) { EnsureInit(); return InBounds(cell.y, cell.x) && pacWalkable[cell.y, cell.x]; }

    public bool IsGhostHouse(Vector2Int cell) { EnsureInit(); return InBounds(cell.y, cell.x) && ghostHouse[cell.y, cell.x]; }

    public bool IsTunnelEnd(Vector2Int cell) => IsWalkable(cell) && (cell.x == 0 || cell.x == Cols - 1);

    // 格子坐标 <-> 世界坐标
    public Vector3 CellToWorld(Vector2Int cell)
    {
        EnsureInit();
        return transform.TransformPoint(CellToLocal(cell.y, cell.x));
    }

    public Vector2Int WorldToCell(Vector3 world)
    {
        EnsureInit();
        Vector3 local = transform.InverseTransformPoint(world) - mapOffset;
        float x = local.x, down = useXZPlane ? -local.z : -local.y;
        if (centerMap)
        {
            x += (cols - 1) * cellSize * 0.5f;
            down += (rows - 1) * cellSize * 0.5f;
        }
        return new Vector2Int(Mathf.RoundToInt(x / cellSize), Mathf.RoundToInt(down / cellSize));
    }

    public Vector3 MapCenter
    {
        get
        {
            EnsureInit();
            return (CellToWorld(new Vector2Int(0, 0)) + CellToWorld(new Vector2Int(cols - 1, rows - 1))) * 0.5f;
        }
    }

    // 地图尺寸
    public Vector2 MapSize { get { EnsureInit(); return new Vector2(cols * cellSize, rows * cellSize); } }

    // 鬼离开鬼屋的出口
    public Vector2Int GetHouseExit(bool top, Vector2Int from)
    {
        EnsureInit();
        Vector2Int best = from;
        float bestDist = float.MaxValue;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                if (map[r, c] != GhostGate) continue;
                int outR = top ? r - 1 : r + 1;
                if (!InBounds(outR, c) || !pacWalkable[outR, c]) continue;
                var exit = new Vector2Int(c, outR);
                float d = (exit - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = exit; }
            }
        return best;
    }

    // 地图四个角（4 号鬼用）
    public Vector2Int GetCornerCell(int corner)
    {
        EnsureInit();
        Vector2Int target = corner switch
        {
            0 => new Vector2Int(0, 0),
            1 => new Vector2Int(cols - 1, 0),
            2 => new Vector2Int(cols - 1, rows - 1),
            _ => new Vector2Int(0, rows - 1),
        };
        Vector2Int best = target;
        float bestDist = float.MaxValue;
        for (int r = 0; r < rows; r++)
            for (int c = 1; c < cols - 1; c++)
            {
                if (!pacWalkable[r, c]) continue;
                float d = (new Vector2Int(c, r) - target).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = new Vector2Int(c, r); }
            }
        return best;
    }
}
