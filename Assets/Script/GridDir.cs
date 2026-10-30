using UnityEngine;

// four directions, shared by pac and ghosts
public enum GridDir { None = -1, Up = 0, Down = 1, Left = 2, Right = 3 }

public static class GridDirExt
{
    public static readonly GridDir[] All = { GridDir.Up, GridDir.Down, GridDir.Left, GridDir.Right };

    // direction to cell offset (row goes down)
    public static Vector2Int ToCellOffset(this GridDir d)
    {
        if (d == GridDir.Up) return new Vector2Int(0, -1);
        else if (d == GridDir.Down) return new Vector2Int(0, 1);
        else if (d == GridDir.Left) return new Vector2Int(-1, 0);
        else if (d == GridDir.Right) return new Vector2Int(1, 0);
        return Vector2Int.zero;
    }

    // direction to world direction
    public static Vector3 ToWorld(this GridDir d)
    {
        var o = d.ToCellOffset();
        return new Vector3(o.x, -o.y, 0f);
    }

    // opposite
    public static GridDir Opposite(this GridDir d)
    {
        switch (d)
        {
            case GridDir.Up: return GridDir.Down;
            case GridDir.Down: return GridDir.Up;
            case GridDir.Left:
                return GridDir.Right;
            case GridDir.Right: return GridDir.Left;
        }
        return GridDir.None;
    }
}
