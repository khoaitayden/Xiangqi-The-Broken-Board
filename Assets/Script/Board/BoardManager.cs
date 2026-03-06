using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    public enum TurnState { PlayerTurn, EnemyTurn, GameOver }
    
    [Header("Game State")]
    public TurnState currentTurn = TurnState.PlayerTurn;
    [Header("Level Data")]
    public BoardLayoutSO currentLevel;

    [Header("Board Settings")]
    public int width = 9;  
    public int height = 10; 
    public float spacing = 1.5f; 

    [Header("Prefabs")]
    public GameObject nodePrefab; 
    public GameObject playerGeneralPrefab;
    public GameObject enemyPawnPrefab; 
    public GameObject enemyHorsePrefab;

    public GameObject enemyAdvisorPrefab;
    public GameObject enemyElephantPrefab;
    public GameObject enemyGeneralPrefab;
    public GameObject enemyChariotPrefab;
    public GameObject enemyCannonPrefab;

    public BoardNode[,] grid;
    private PlayerGeneral activePlayer; 
    public List<Piece> enemyPieces = new List<Piece>();

    private PlayerControls controls;

    // A small helper class to store potential AI moves
    private class AIMove
    {
        public Piece piece; // Changed from EnemyPawn
        public BoardNode targetNode;
    }

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Board.Click.performed += context => OnClick();
    }

    private void OnEnable() { controls.Enable(); }
    private void OnDisable() { controls.Disable(); }

    void Start()
    {
        GenerateBoard();
        
        if (currentLevel != null)
        {
            LoadLevel(currentLevel);
        }
        else
        {
            Debug.LogError("No Level Data assigned to BoardManager!");
        }
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

    void LoadLevel(BoardLayoutSO levelData)
    {
        // 1. Spawn Player
        SpawnPlayer(levelData.playerSpawnPosition.x, levelData.playerSpawnPosition.y);

        // 2. Spawn Enemies
        foreach (PieceSpawnData spawnData in levelData.enemySpawns)
        {
            GameObject prefabToSpawn = GetPrefabForType(spawnData.pieceType);
            
            if (prefabToSpawn != null)
            {
                SpawnEnemy(prefabToSpawn, spawnData.position.x, spawnData.position.y, spawnData.startingCooldown);
            }
        }
    }
    void SpawnPlayer(int startX, int startY) 
    {
        BoardNode startNode = grid[startX, startY];
        GameObject playerObj = Instantiate(playerGeneralPrefab, startNode.nodeGameObject.transform.position, Quaternion.identity);
        activePlayer = playerObj.GetComponent<PlayerGeneral>();
        activePlayer.currentX = startNode.x;
        activePlayer.currentY = startNode.y;
        activePlayer.targetPosition = startNode.nodeGameObject.transform.position; 
        startNode.currentPiece = activePlayer;
    }

    void SpawnEnemy(GameObject prefab, int startX, int startY, int startingCooldown)
    {
        BoardNode startNode = grid[startX, startY];
        GameObject enemyObj = Instantiate(prefab, startNode.nodeGameObject.transform.position, Quaternion.identity);
        Piece enemyPiece = enemyObj.GetComponent<Piece>();
        
        enemyPiece.currentX = startX;
        enemyPiece.currentY = startY;
        enemyPiece.targetPosition = startNode.nodeGameObject.transform.position; 
        
        // NEW: Assign the custom starting cooldown!
        enemyPiece.currentCooldown = startingCooldown; 
        
        startNode.currentPiece = enemyPiece;
        
        enemyPieces.Add(enemyPiece);
    }

    private void OnClick()
    {
        if (currentTurn != TurnState.PlayerTurn) return;

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
            
            currentTurn = TurnState.EnemyTurn;
            StartCoroutine(EnemyPhaseCoroutine());
        }
    }

    IEnumerator EnemyPhaseCoroutine()
    {
        yield return new WaitForSeconds(0.2f); // Tiny delay

        // Make a copy in case pieces capture/destroy each other
        List<Piece> enemiesToMove = new List<Piece>(enemyPieces);

        foreach (Piece enemy in enemiesToMove)
        {
            if (enemy == null) continue; // Skip dead pieces

            // 1. Decrease Cooldown
            if (enemy.currentCooldown > 0)
            {
                enemy.currentCooldown--;
            }

            // 2. If Cooldown is 0, it strikes!
            if (enemy.currentCooldown == 0)
            {
                BoardNode targetNode = enemy.GetAIMove(grid);

                if (targetNode != null)
                {
                    // Check for Player Kill
                    if (targetNode.currentPiece == activePlayer)
                    {
                        Debug.Log("GAME OVER! Player was captured!");
                        currentTurn = TurnState.GameOver;
                        grid[enemy.currentX, enemy.currentY].currentPiece = null;
                        enemy.MoveTo(targetNode);
                        targetNode.currentPiece = enemy;
                        yield break; // End game immediately
                    }

                    // Normal Move
                    grid[enemy.currentX, enemy.currentY].currentPiece = null;
                    enemy.MoveTo(targetNode);
                    targetNode.currentPiece = enemy;

                    // 3. Reset Cooldown because it successfully moved!
                    enemy.currentCooldown = enemy.maxCooldown;
                }
                else 
                {
                    // If it wants to move but is completely blocked by its own team,
                    // its cooldown stays at 0. It will continue jiggling and try again next turn!
                    enemy.currentCooldown = 0;
                }
            }
        }

        // Pass the turn back to the player
        if (currentTurn != TurnState.GameOver)
        {
            currentTurn = TurnState.PlayerTurn;
        }
    }

    GameObject GetPrefabForType(PieceType type)
    {
        switch (type)
        {
            case PieceType.Pawn: return enemyPawnPrefab;
            case PieceType.Horse: return enemyHorsePrefab;
            case PieceType.Chariot: return enemyChariotPrefab;
            case PieceType.Elephant: return enemyElephantPrefab;
            case PieceType.Advisor: return enemyAdvisorPrefab;
            case PieceType.Cannon: return enemyCannonPrefab;
            case PieceType.EnemyGeneral: return enemyGeneralPrefab;
            default: return null;
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