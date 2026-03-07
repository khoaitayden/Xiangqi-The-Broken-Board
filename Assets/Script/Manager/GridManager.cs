using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    [Header("Board Settings")]
    public int width = 9;  
    public int height = 10; 
    public float spacing = 1.5f; 
    public GameObject nodePrefab; 

    public BoardNode[,] grid;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    public void GenerateBoard()
    {
        grid = new BoardNode[width, height];
        float offsetX = (width - 1) * spacing / 2f;
        float offsetY = (height - 1) * spacing / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 pos = new Vector2(x * spacing - offsetX, y * spacing - offsetY);
                BoardNode newNode = new BoardNode(x, y, pos);
                grid[x, y] = newNode;

                GameObject spawnedNode = Instantiate(nodePrefab, pos, Quaternion.identity);
                spawnedNode.name = $"Node ({x}, {y})";
                spawnedNode.transform.parent = this.transform;
                
                if (newNode.isPlayerPalace) spawnedNode.GetComponent<SpriteRenderer>().color = Color.blue;
                if (newNode.isEnemyPalace) spawnedNode.GetComponent<SpriteRenderer>().color = Color.red;

                newNode.nodeGameObject = spawnedNode;
            }
        }
    }

    public BoardNode GetNodeAtPosition(Vector2 pos)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (Vector2.Distance(grid[x, y].nodeGameObject.transform.position, pos) < 0.5f) 
                {
                    return grid[x, y];
                }
            }
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying && grid == null) return;
        if (grid == null) return;
        Gizmos.color = Color.black;
        for (int y = 0; y < height; y++) Gizmos.DrawLine(grid[0, y].worldPosition, grid[width - 1, y].worldPosition);
        for (int x = 0; x < width; x++)
        {
            if (x == 0 || x == width - 1) Gizmos.DrawLine(grid[x, 0].worldPosition, grid[x, height - 1].worldPosition);
            else { Gizmos.DrawLine(grid[x, 0].worldPosition, grid[x, 4].worldPosition); Gizmos.DrawLine(grid[x, 5].worldPosition, grid[x, height - 1].worldPosition); }
        }
        Gizmos.DrawLine(grid[3, 0].worldPosition, grid[5, 2].worldPosition);
        Gizmos.DrawLine(grid[3, 2].worldPosition, grid[5, 0].worldPosition);
        Gizmos.DrawLine(grid[3, 7].worldPosition, grid[5, 9].worldPosition);
        Gizmos.DrawLine(grid[3, 9].worldPosition, grid[5, 7].worldPosition);
    }
}