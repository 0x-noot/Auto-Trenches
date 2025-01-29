using UnityEngine;

public class PathNode
{
    public Vector3Int Position { get; private set; }
    public PathNode Parent { get; set; }
    public float G { get; set; } // Cost from start to this node
    public float H { get; set; } // Estimated cost from this node to end
    public float F => G + H;     // Total cost

    public PathNode(Vector3Int position)
    {
        Position = position;
    }
}