using UnityEngine;
using UnityEngine.InputSystem; // Required for the new Input System

public class BoardManager : MonoBehaviour
{
    [Header("Board Settings")]
    public int width = 9;  
    public int height = 10; 
    public float spacing = 1.5f; 

    [Header("Prefabs")]
    public GameObject nodePrefab; 
    public GameObject playerGeneralPrefab;

    public BoardNode[,] grid;
    private PlayerGeneral activePlayer; 

    // NEW: Reference to our generated Input Action Map
    private PlayerControls controls;

    private void Awake()
    {
        // Initialize the input controls
        controls = new PlayerControls();

        // Subscribe to the Click action. When performed, call OnClick()
        controls.Board.Click.performed += context => OnClick();
    }

    private void OnEnable()
    {
        controls.Enable();
    }

    private void OnDisable()
    {
        controls.Disable();
    }

    void Start()
    {
        GenerateBoard();
        SpawnPlayer(); 
    }

    void GenerateBoard()
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

    void SpawnPlayer()
    {
        BoardNode startNode = grid[4, 0];
        
        // Spawn exactly at the visual node's position to prevent offset
        GameObject playerObj = Instantiate(playerGeneralPrefab, startNode.nodeGameObject.transform.position, Quaternion.identity);
        activePlayer = playerObj.GetComponent<PlayerGeneral>();
        
        activePlayer.currentX = startNode.x;
        activePlayer.currentY = startNode.y;
        startNode.currentPiece = activePlayer;
    }

    // NEW: Handle the click using the Action Map
    private void OnClick()
    {
        // Read the mouse position from the PointerPosition action
        Vector2 screenPosition = controls.Board.PointerPosition.ReadValue<Vector2>();
        Vector2 worldPosition = Camera.main.ScreenToWorldPoint(screenPosition);

        BoardNode clickedNode = GetNodeAtPosition(worldPosition);

        if (clickedNode != null)
        {
            TryMovePlayer(clickedNode);
        }
    }

    BoardNode GetNodeAtPosition(Vector2 pos)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Use the visual node's actual transform position for accuracy
                if (Vector2.Distance(grid[x, y].nodeGameObject.transform.position, pos) < 0.5f) 
                {
                    return grid[x, y];
                }
            }
        }
        return null;
    }

    void TryMovePlayer(BoardNode targetNode)
    {
        if (activePlayer.IsValidMove(targetNode, grid))
        {
            grid[activePlayer.currentX, activePlayer.currentY].currentPiece = null;
            activePlayer.MoveTo(targetNode);
            targetNode.currentPiece = activePlayer;
            
            Debug.Log($"Player moved to {targetNode.x}, {targetNode.y}");
        }
        else
        {
            Debug.Log("Invalid Move!");
        }
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