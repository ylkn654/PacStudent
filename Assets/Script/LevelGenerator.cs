using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Builds the full map by mirroring the fixed levelMap array (the top-left quarter), then spawns it.
/// Drag the prefab for each combination into its matching slot in the Inspector.
/// </summary>
public class LevelGenerator : MonoBehaviour
{
    // ===================== Slot definitions =====================

    /// <summary>Direction-combination slots shared by Road and River. Letters are the connected sides: N up, S down, E right, W left.</summary>
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

    /// <summary>Outer walls: letters show which side / corner of the map (or walkable area) the wall is on.</summary>
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

    // ===================== Fixed map array (top-left quarter) =====================
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

    private const int Empty = 0, OuterCorner = 1, OuterWall = 2, InnerCorner = 3, InnerWall = 4,
                      Pellet = 5, PowerPellet = 6, TJunction = 7, GhostGate = 8;

    // ===================== Inspector slots =====================
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
    [Tooltip("Duplicate the last row when mirroring vertically")]
    [SerializeField] private bool duplicateMiddleRow = true;
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

    // ===================== Internal data =====================
    private int[,] map;         // Full map after mirroring
    private int rows, cols;
    private bool[,] walkable;   // Walkable cells inside the outer walls
    private Transform mapRoot;
    private bool checkOnly;     // Only check slots, do not spawn
    private readonly SortedSet<string> neededSlots = new SortedSet<string>();
    private readonly SortedSet<string> emptySlots = new SortedSet<string>();

    // Direction order is always N S E W
    private const int DirN = 0, DirS = 1, DirE = 2, DirW = 3;
    private static readonly int[] DRow = { -1, 1, 0, 0 };
    private static readonly int[] DCol = { 0, 0, 1, -1 };
    private static readonly char[] DirChar = { 'N', 'S', 'E', 'W' };

    private void Start()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate Map")]
    public void Generate()
    {
        checkOnly = false;
        emptySlots.Clear();
        Init();
        PrepareRoot();
        BuildAll();

        if (emptySlots.Count > 0)
            Debug.LogWarning("[LevelGenerator] These slots have no prefab assigned; their cells were skipped:\n" + string.Join("\n", emptySlots));
    }

    /// <summary>Logs which slots this map uses and which are still empty.</summary>
    [ContextMenu("Check Prefab Slots")]
    public void CheckPrefabSlots()
    {
        checkOnly = true;
        neededSlots.Clear();
        emptySlots.Clear();
        Init();
        BuildAll();
        checkOnly = false;

        Debug.Log("[LevelGenerator] Slots used by this map:\n" + string.Join("\n", neededSlots));
        if (emptySlots.Count > 0)
            Debug.LogWarning("[LevelGenerator] Empty slots:\n" + string.Join("\n", emptySlots));
        else
            Debug.Log("[LevelGenerator] All used slots are assigned.");
    }

    private void Init()
    {
        map = BuildFullMap();
        rows = map.GetLength(0);
        cols = map.GetLength(1);
        FloodFillWalkable();
    }

    // ===================== Build the full map by mirroring =====================
    /// <summary>
    /// levelMap is the top-left quarter. It is mirrored horizontally to get the top half,
    /// then vertically to get the bottom half, so the other three quarters are flips of the first.
    /// All checks run on the full map afterwards, so wall, road and corner directions are
    /// re-evaluated automatically and prefabs never need to be flipped.
    /// </summary>
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

    private void BuildAll()
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                BuildCell(r, c);
    }

    // ===================== Per-cell handling =====================
    private void BuildCell(int r, int c)
    {
        int v = map[r, c];
        switch (v)
        {
            case Empty:
                if (walkable[r, c]) SpawnRoad(r, c);
                else Spawn(wall, "Others / Wall", r, c);
                break;

            case Pellet:
                SpawnRoad(r, c);
                Spawn(coin, "Others / Coin", r, c, itemOffset);
                break;

            case PowerPellet:
                SpawnRoad(r, c);
                Spawn(sword, "Others / Sword", r, c, itemOffset);
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

    private void SpawnRoad(int r, int c)
    {
        string key = Suffix(GetLinks(r, c, IsRoad));
        Spawn(road.Get(key), "Road / " + key, r, c);
    }

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

            // Extends down and right = top-left ┌ = WallNW; the others follow the same idea
            if (v == DirS && h == DirE) Spawn(outerWall.WallNW, "Outer wall / WallNW ┌", r, c);
            else if (v == DirS && h == DirW) Spawn(outerWall.WallNE, "Outer wall / WallNE ┐", r, c);
            else if (v == DirN && h == DirE) Spawn(outerWall.WallSW, "Outer wall / WallSW └", r, c);
            else Spawn(outerWall.WallSE, "Outer wall / WallSE ┘", r, c);
            return;
        }

        string axis = PickAxis(links);
        if (axis == null)
            axis = (WalkableAt(r - 1, c) || WalkableAt(r + 1, c)) ? "EW" : "NS";

        if (axis == "EW")
        {
            // Horizontal wall: walkable area below -> top-side WallN; above -> bottom-side WallS
            bool southIn = WalkableAt(r + 1, c), northIn = WalkableAt(r - 1, c);
            bool isNorth = (southIn != northIn) ? southIn : r < rows / 2;
            if (isNorth) Spawn(outerWall.WallN, "Outer wall / WallN ─", r, c);
            else Spawn(outerWall.WallS, "Outer wall / WallS ─", r, c);
        }
        else
        {
            // Vertical wall: walkable area to the right -> left-side WallW; to the left -> right-side WallE
            bool eastIn = WalkableAt(r, c + 1), westIn = WalkableAt(r, c - 1);
            bool isWest = (eastIn != westIn) ? eastIn : c < cols / 2;
            if (isWest) Spawn(outerWall.WallW, "Outer wall / WallW │", r, c);
            else Spawn(outerWall.WallE, "Outer wall / WallE │", r, c);
        }
    }

    // ===================== Tell the two kinds of 0 apart (flood fill) =====================
    private bool IsPassableValue(int v)
    {
        return v == Empty || v == Pellet || v == PowerPellet || (v == GhostGate && gateIsPassable);
    }

    /// <summary>
    /// Spreads from every pellet (5/6) through 0/5/6(/8). Any 0 that is reached is a walkable 0
    /// inside the outer walls; any 0 that is not reached is outside the walls (or fully enclosed by walls).
    /// </summary>
    private void FloodFillWalkable()
    {
        walkable = new bool[rows, cols];
        var queue = new Queue<Vector2Int>();

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                if (map[r, c] == Pellet || map[r, c] == PowerPellet)
                {
                    walkable[r, c] = true;
                    queue.Enqueue(new Vector2Int(c, r));
                }

        while (queue.Count > 0)
        {
            Vector2Int p = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nr = p.y + DRow[d], nc = p.x + DCol[d];
                if (!InBounds(nr, nc) || walkable[nr, nc]) continue;
                if (!IsPassableValue(map[nr, nc])) continue;
                walkable[nr, nc] = true;
                queue.Enqueue(new Vector2Int(nc, nr));
            }
        }
    }

    // ===================== Connection helpers =====================
    private bool InBounds(int r, int c) => r >= 0 && r < rows && c >= 0 && c < cols;
    private bool WalkableAt(int r, int c) => InBounds(r, c) && walkable[r, c];

    private bool IsRoad(int r, int c) => walkable[r, c];

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

    /// <summary>Returns whether each of N S E W connects to a cell of the same kind.</summary>
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

    private static string Suffix(bool[] links)
    {
        var sb = new StringBuilder();
        for (int d = 0; d < 4; d++)
            if (links[d]) sb.Append(DirChar[d]);
        return sb.ToString();
    }

    /// <summary>Straight walls use one axis: more vertical links -> NS, more horizontal links -> EW, tie -> null.</summary>
    private static string PickAxis(bool[] links)
    {
        int ns = (links[DirN] ? 1 : 0) + (links[DirS] ? 1 : 0);
        int ew = (links[DirE] ? 1 : 0) + (links[DirW] ? 1 : 0);
        if (ns > ew) return "NS";
        if (ew > ns) return "EW";
        return null;
    }

    /// <summary>
    /// A corner uses exactly one vertical + horizontal pair. When several pairs fit, prefer the pair whose
    /// diagonal cell between them is not a wall (the side the corner actually bends toward),
    /// then the pair whose opposite diagonal is not a wall.
    /// </summary>
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

    // ===================== Spawning =====================
    private void PrepareRoot()
    {
        Transform old = transform.Find("GeneratedMap");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        mapRoot = new GameObject("GeneratedMap").transform;
        mapRoot.SetParent(transform, false);
    }

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

    private void Spawn(GameObject prefab, string slotName, int r, int c, Vector3 extraOffset = default)
    {
        if (checkOnly) neededSlots.Add(slotName);

        if (prefab == null)
        {
            emptySlots.Add(slotName);
            return;
        }

        if (checkOnly) return;

        GameObject go = Instantiate(prefab, mapRoot);
        go.transform.localPosition = CellToLocal(r, c) + extraOffset;
        go.name = $"{prefab.name} ({r},{c})";
    }
}