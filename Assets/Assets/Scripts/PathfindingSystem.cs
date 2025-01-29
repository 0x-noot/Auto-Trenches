using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PathfindingSystem
{
    private Grid grid;
    private LayerMask obstacleLayer;
    private LayerMask unitLayer;

    // Diagonal movement cost (roughly sqrt(2))
    private const float DIAGONAL_COST = 1.4f;
    
    public PathfindingSystem(Grid grid, LayerMask obstacleLayer, LayerMask unitLayer)
    {
        this.grid = grid;
        this.obstacleLayer = obstacleLayer;
        this.unitLayer = unitLayer;
    }

    public List<Vector3Int> FindPath(Vector3Int start, Vector3Int target)
    {
        var openSet = new List<PathNode>();
        var closedSet = new HashSet<Vector3Int>();
        var startNode = new PathNode(start);
        
        openSet.Add(startNode);
        
        while (openSet.Count > 0)
        {
            var current = openSet.OrderBy(n => n.F).First();
            
            if (current.Position == target)
            {
                var path = ReconstructPath(current);
                return SmoothPath(path);
            }
                
            openSet.Remove(current);
            closedSet.Add(current.Position);
            
            foreach (var neighbor in GetNeighbors(current.Position))
            {
                if (closedSet.Contains(neighbor.Position))
                    continue;

                float moveCost = Vector3Int.Distance(current.Position, neighbor.Position) < 1.1f ? 1f : DIAGONAL_COST;
                float newG = current.G + moveCost;
                
                var existingNode = openSet.FirstOrDefault(n => n.Position == neighbor.Position);
                if (existingNode == null)
                {
                    neighbor.G = newG;
                    neighbor.H = CalculateHeuristic(neighbor.Position, target);
                    neighbor.Parent = current;
                    openSet.Add(neighbor);
                }
                else if (newG < existingNode.G)
                {
                    existingNode.G = newG;
                    existingNode.Parent = current;
                }
            }
        }
        
        // If no path found, return null
        return null;
    }

    private IEnumerable<PathNode> GetNeighbors(Vector3Int position)
    {
        // Include diagonal directions
        Vector3Int[] directions = new[]
        {
            new Vector3Int(1, 0, 0),   // Right
            new Vector3Int(-1, 0, 0),  // Left
            new Vector3Int(0, 1, 0),   // Up
            new Vector3Int(0, -1, 0),  // Down
            new Vector3Int(1, 1, 0),   // Up-Right
            new Vector3Int(-1, 1, 0),  // Up-Left
            new Vector3Int(1, -1, 0),  // Down-Right
            new Vector3Int(-1, -1, 0)  // Down-Left
        };
        
        foreach (var dir in directions)
        {
            var neighborPos = position + dir;
            if (IsValidPosition(neighborPos))
            {
                // For diagonal movement, check if both adjacent cells are walkable
                if (Mathf.Abs(dir.x) == 1 && Mathf.Abs(dir.y) == 1)
                {
                    var horizontalNeighbor = position + new Vector3Int(dir.x, 0, 0);
                    var verticalNeighbor = position + new Vector3Int(0, dir.y, 0);
                    
                    if (IsValidPosition(horizontalNeighbor) && IsValidPosition(verticalNeighbor))
                    {
                        yield return new PathNode(neighborPos);
                    }
                }
                else
                {
                    yield return new PathNode(neighborPos);
                }
            }
        }
    }

    private bool IsValidPosition(Vector3Int position)
    {
        Vector3 worldPos = grid.GetCellCenterWorld(position);
        
        // Check for obstacles with a slightly smaller radius to allow for better pathing
        if (Physics2D.OverlapCircle(worldPos, 0.3f, obstacleLayer))
            return false;
            
        // Check for other units with a smaller radius
        if (Physics2D.OverlapCircle(worldPos, 0.3f, unitLayer))
            return false;
        
        return true;
    }

    private float CalculateHeuristic(Vector3Int a, Vector3Int b)
    {
        // Using octile distance for better diagonal movement estimation
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) + (DIAGONAL_COST - 2) * Mathf.Min(dx, dy);
    }

    private List<Vector3Int> ReconstructPath(PathNode endNode)
    {
        var path = new List<Vector3Int>();
        var current = endNode;
        
        while (current != null)
        {
            path.Add(current.Position);
            current = current.Parent;
        }
        
        path.Reverse();
        return path;
    }

    private List<Vector3Int> SmoothPath(List<Vector3Int> path)
    {
        if (path == null || path.Count <= 2)
            return path;

        var smoothedPath = new List<Vector3Int> { path[0] };
        int current = 0;

        while (current < path.Count - 1)
        {
            int furthest = current + 1;
            
            // Look ahead in the path for the furthest point we can directly reach
            for (int i = current + 2; i < path.Count; i++)
            {
                if (CanMoveDirect(path[current], path[i]))
                {
                    furthest = i;
                }
            }

            smoothedPath.Add(path[furthest]);
            current = furthest;
        }

        return smoothedPath;
    }

    private bool CanMoveDirect(Vector3Int start, Vector3Int end)
    {
        Vector3 startWorld = grid.GetCellCenterWorld(start);
        Vector3 endWorld = grid.GetCellCenterWorld(end);
        Vector2 direction = (endWorld - startWorld).normalized;
        float distance = Vector2.Distance(startWorld, endWorld);

        // Check if there are any obstacles in the direct path
        RaycastHit2D hit = Physics2D.Raycast(startWorld, direction, distance, obstacleLayer | unitLayer);
        return hit.collider == null;
    }
}