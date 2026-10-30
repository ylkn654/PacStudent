using System.Collections.Generic;

// random map for Level 2:
// 1. make a 15x15 quarter (nodes every 3 cells, joined with a random DFS -> has dead ends)
// 2. 50/50 mirror or rotate 90 deg into 29x29; each quarter gets at least 2 roads to both neighbours
// 3. find dead ends on the full map and open them up in the quarter until there are none
// 4. number the walls 1/2/3/4 from their neighbours and hand it to LevelGenerator
public static class RandomMapGenerator
{
    public const int Q = 15;            // quarter size
    public const int Full = Q * 2 - 1;  // full size 29 (middle line shared)
    private const int Mid = Q - 1;      // last row / col of the quarter = middle line of the map
    private const int Last = Full - 1;  // last row / col of the full map
    private const int Step = 3;
    private const int MaxPasses = 100;
    static readonly int[] nIdx = { 1, 4, 7, 10, 13 };

    private enum T { Wall, Path, Tunnel, House, Gate }

    public class Result
    {
        public int[,] map;
        public bool rotated;
        public int exits;          // exits on the quarter edges (no tunnels)
        public int braidPasses;
    }

    /// <param name="rotateChance">chance to use rotation instead of mirroring</param>
    /// <param name="thirdExitChance">chance of one more exit on the right when mirrored</param>
    public static Result Generate(System.Random rng, float rotateChance = 0.5f, float thirdExitChance = 0.5f)
    {
        var res = new Result { rotated = rng.NextDouble() < rotateChance };
        T[,] q = MakeQ(rng, res, thirdExitChance);

        // remove dead ends
        T[,] f = null;
        int[,] sr = null;
        int[,] sc = null;
        int pass = 0;
        while (pass < MaxPasses)
        {
            f = Stitch(q, res.rotated, out sr, out sc);
            var dd = Dead(f);
            if (dd.Count == 0)
            {
                res.braidPasses = pass;
                break;
            }

            bool ch = false;
            var dq = new HashSet<(int, int)>();
            foreach (var d in dd) dq.Add((sr[d.r, d.c], sc[d.r, d.c]));
            var done = new HashSet<(int, int)>();
            foreach (var nd in dq)
            {
                if (done.Contains(nd)) continue;
                if (!isN(q, nd.Item1, nd.Item2)) continue;
                if (Fix(q, rng, nd, dq, done) == true) ch = true;
                done.Add(nd);
            }
            if (!ch)
            {
                res.braidPasses = pass;
                break;
            }
            pass++;
        }

        res.map = Conv(f, sr, sc);
        return res;
    }

    // ---------- quarter ----------

    static T[,] MakeQ(System.Random rng, Result res, float extra)
    {
        var q = new T[Q, Q];

        foreach (int r in nIdx)
            foreach (int c in nIdx)
                q[r, c] = T.Path;
        q[13, 13] = T.Wall;   // inside the house
        q[13, 1] = T.Wall;    // next to the tunnel
        if (res.rotated) q[1, 13] = T.Wall;

        // ghost house corner
        for (int r = 12; r <= Mid; r++)
            for (int c = 12; c <= Mid; c++)
                q[r, c] = T.House;
        q[11, Mid] = T.Gate;
        q[Mid, 11] = T.Gate;
        Dig(q, (10, 10), (10, 13));
        Dig(q, (10, 10), (13, 10));

        // tunnel
        for (int c = 0; c < 4; c++)
            q[Mid, c] = T.Tunnel;
        q[Mid, 4] = T.Path;

        // exits, at least 2 each side (not counting (Mid,4))
        q[Mid, 10] = T.Path;
        q[Mid, 7] = T.Path;
        q[10, Mid] = T.Path;
        if (!res.rotated)   // rotated copies bottom -> right later
        {
            var rows = new List<int> { 1, 4, 7 };
            int p = rows[rng.Next(rows.Count)];
            q[p, Mid] = T.Path;
            rows.Remove(p);
            if (rng.NextDouble() < extra) q[rows[rng.Next(rows.Count)], Mid] = T.Path;
        }
        res.exits = Exits(q, res.rotated);

        // random DFS from around the house
        var seen = new HashSet<(int, int)> { (10, 10), (10, 13), (13, 10) };
        var st = new List<(int, int)> { (10, 10), (10, 13), (13, 10) };
        while (st.Count > 0)
        {
            var cur = st[st.Count - 1];
            var nx = new List<(int, int)>();
            foreach (var n in Nb(q, cur))
                if (!seen.Contains(n)) nx.Add(n);
            if (nx.Count == 0)
            {
                st.RemoveAt(st.Count - 1);
                continue;
            }
            var pk = nx[rng.Next(nx.Count)];
            Dig(q, cur, pk);
            seen.Add(pk);
            st.Add(pk);
        }
        return q;
    }

    static int Exits(T[,] q, bool rot)
    {
        int b = 0;
        int rr = 0;
        for (int i = 5; i < Mid; i++)
        {
            if (q[Mid, i] == T.Path) b++;
            if (q[i, Mid] == T.Path) rr++;
        }
        if (!rot)
            for (int i = 0; i < 5; i++)
                if (q[i, Mid] == T.Path) rr++;
        return rot ? b * 2 : b + rr;
    }

    static bool isN(T[,] q, int r, int c) => System.Array.IndexOf(nIdx, r) >= 0 && System.Array.IndexOf(nIdx, c) >= 0 && q[r, c] == T.Path;

    static IEnumerable<(int, int)> Nb(T[,] q, (int r, int c) n)
    {
        int[] dr = { -Step, Step, 0, 0 };
        int[] dc = { 0, 0, -Step, Step };
        for (int i = 0; i < 4; i++)
        {
            int r = n.r + dr[i];
            int c = n.c + dc[i];
            if (r < 0 || r >= Q || c < 0 || c >= Q) continue;
            if (isN(q, r, c)) yield return (r, c);
        }
    }

    static bool Dug(T[,] q, (int r, int c) a, (int r, int c) b)
    {
        return q[a.r + System.Math.Sign(b.r - a.r), a.c + System.Math.Sign(b.c - a.c)] == T.Path;
    }

    static void Dig(T[,] q, (int r, int c) a, (int r, int c) b)
    {
        int sr = System.Math.Sign(b.r - a.r);
        int sc = System.Math.Sign(b.c - a.c);
        int r = a.r;
        int c = a.c;
        while (r != b.r || c != b.c)
        {
            q[r, c] = T.Path;
            r += sr;
            c += sc;
        }
        q[b.r, b.c] = T.Path;
    }

    static int Deg(T[,] q, (int r, int c) n)
    {
        int d = 0;
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };
        for (int i = 0; i < 4; i++)
        {
            int r = n.r + dr[i];
            int c = n.c + dc[i];
            if (r >= 0 && r < Q && c >= 0 && c < Q)
                if (q[r, c] == T.Path || q[r, c] == T.Tunnel) d++;
        }
        return d;
    }

    // open a dead end node to a neighbour node; prefer another dead end, else the one with fewest roads
    static bool Fix(T[,] q, System.Random rng, (int r, int c) node, HashSet<(int, int)> dead, HashSet<(int, int)> done)
    {
        var opt = new List<(int, int)>();
        foreach (var n in Nb(q, node))
            if (!Dug(q, node, n)) opt.Add(n);
        if (opt.Count == 0) return false;

        var pool = opt.FindAll(o => dead.Contains(o) && !done.Contains(o));
        if (pool.Count == 0)
        {
            int mn = int.MaxValue;
            foreach (var o in opt) mn = System.Math.Min(mn, Deg(q, o));
            pool = opt.FindAll(o => Deg(q, o) == mn);
        }

        var pk = pool[rng.Next(pool.Count)];
        Dig(q, node, pk);
        done.Add(pk);
        return true;
    }

    // ---------- stitch ----------

    // quarter -> 29x29, sr/sc = which quarter cell each full cell came from
    static T[,] Stitch(T[,] q, bool rot, out int[,] sr, out int[,] sc)
    {
        if (rot)
            for (int i = 0; i < Mid; i++)
                q[i, Mid] = q[Mid, i];   // middle lines have to match

        var f = new T[Full, Full];
        sr = new int[Full, Full];
        sc = new int[Full, Full];
        for (int r = 0; r < Full; r++)
            for (int c = 0; c < Full; c++)
            {
                int a = r;
                int b = c;
                if (rot)
                {
                    // turn back to top left: (r, c) -> (Last - c, r)
                    while (a > Mid || b > Mid)
                    {
                        int t = a;
                        a = Last - b;
                        b = t;
                    }
                }
                else
                {
                    if (a > Mid) a = Last - a;
                    if (b > Mid) b = Last - b;
                }
                f[r, c] = q[a, b];
                sr[r, c] = a;
                sc[r, c] = b;
            }
        return f;
    }

    // ---------- checks ----------

    static bool Open(T t) => t == T.Path || t == T.Tunnel;

    // path cells with only one open neighbour (tunnels dont count)
    static List<(int r, int c)> Dead(T[,] f)
    {
        var l = new List<(int, int)>();
        int[] dr = { -1, 1, 0, 0 };
        int[] dc = { 0, 0, -1, 1 };
        for (int r = 0; r < Full; r++)
            for (int c = 0; c < Full; c++)
            {
                if (f[r, c] != T.Path) continue;
                int n = 0;
                for (int i = 0; i < 4; i++)
                {
                    int rr = r + dr[i];
                    int cc = c + dc[i];
                    if (rr >= 0 && rr < Full && cc >= 0 && cc < Full && Open(f[rr, cc])) n++;
                }
                if (n <= 1) l.Add((r, c));
            }
        return l;
    }

    // ---------- to levelMap numbers ----------

    private const int Empty = 0, OuterCorner = 1, OuterWall = 2, InnerCorner = 3, InnerWall = 4,
                      Pellet = 5, PowerPellet = 6, GhostGate = 8;

    static int[,] Conv(T[,] f, int[,] sr, int[,] sc)
    {
        var m = new int[Full, Full];
        var ol = new bool[Full, Full];

        // walls touching a road / the house are the river outline, the rest becomes grass
        for (int r = 0; r < Full; r++)
            for (int c = 0; c < Full; c++)
            {
                if (f[r, c] != T.Wall || Edge(r, c)) continue;
                for (int dr = -1; dr <= 1 && !ol[r, c]; dr++)
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        int rr = r + dr;
                        int cc = c + dc;
                        if (rr < 0 || rr >= Full || cc < 0 || cc >= Full) continue;
                        if (f[rr, cc] != T.Wall)
                        {
                            ol[r, c] = true;
                            break;
                        }
                    }
            }

        for (int r = 0; r < Full; r++)
            for (int c = 0; c < Full; c++)
            {
                T t = f[r, c];
                if (t == T.Tunnel || t == T.House)
                {
                    m[r, c] = Empty;
                    continue;
                }
                if (t == T.Gate)
                {
                    m[r, c] = GhostGate;
                    continue;
                }
                if (t == T.Path)
                {
                    // power pellet on node (4,1) of each quarter
                    m[r, c] = (sr[r, c] == 4 && sc[r, c] == 1) ? PowerPellet : Pellet;
                    continue;
                }
                if (Edge(r, c))
                    m[r, c] = ((r == 0 || r == Last) && (c == 0 || c == Last)) ? OuterCorner : OuterWall;
                else if (ol[r, c])
                {
                    int h = Rv(f, ol, r, c - 1) + Rv(f, ol, r, c + 1);
                    int v = Rv(f, ol, r - 1, c) + Rv(f, ol, r + 1, c);
                    m[r, c] = (h == 1 && v == 1) ? InnerCorner : InnerWall;
                }
                else m[r, c] = Empty;   // grass inside walls
            }
        return m;
    }

    static bool Edge(int r, int c) => r == 0 || c == 0 || r == Last || c == Last;

    static int Rv(T[,] f, bool[,] ol, int r, int c)
    {
        if (r < 0 || r >= Full || c < 0 || c >= Full) return 0;
        return (ol[r, c] || f[r, c] == T.Gate) ? 1 : 0;
    }
}
