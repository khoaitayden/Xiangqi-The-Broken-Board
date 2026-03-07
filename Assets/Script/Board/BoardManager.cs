using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(InputHandler))] // Automatically adds InputHandler to this GameObject
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

    [Header("Aiming & Shooting")]
    public GameObject projectilePrefab; 
    public Transform playerVisualGhost; 
    
    // State Tracking
    private bool isAimingMode = false;
    private Vector2 currentAimDirection;
    private bool isExecutingAction = false; 
    
    private PlayerGeneral activePlayer; 
    public List<Piece> enemyPieces = new List<Piece>();

    // Input
    private InputHandler input;

    private void Awake()
    {
        input = GetComponent<InputHandler>();
    }

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

    void Update()
    {
        if (currentTurn != TurnState.PlayerTurn || isExecutingAction || activePlayer == null) return;

        // 1. Read Mouse Position directly from our new InputHandler
        Vector2 worldPosition = input.MouseWorldPosition;
        BoardNode hoveredNode = GetNodeAtPosition(worldPosition);

        // 2. Determine Context (Move vs Aim)
        DetermineInputContext(worldPosition, hoveredNode);

        // 3. Render Aiming Visuals
        if (isAimingMode)
        {
            DrawAimConeAndHighlightEnemies();
        }

        // 4. Listen for Click Execution from the InputHandler
        if (input.IsClickTriggered) 
        {
            if (!isAimingMode && hoveredNode != null)
            {
                ExecuteMove(hoveredNode);
            }
            else if (isAimingMode && activePlayer.loadedAmmo > 0)
            {
                StartCoroutine(ExecuteShootCoroutine());
            }
            else if (isAimingMode && activePlayer.loadedAmmo <= 0)
            {
                Debug.Log("Out of Ammo! Move to Reload!");
            }
        }
    }

    void DetermineInputContext(Vector2 mouseWorldPos, BoardNode hoveredNode)
    {
        if (hoveredNode != null && activePlayer.IsValidMove(hoveredNode, grid))
        {
            isAimingMode = false;
            
            foreach (Piece enemy in enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }
        }
        else
        {
            isAimingMode = true;
            
            Vector2 playerPos = activePlayer.transform.position;
            currentAimDirection = (mouseWorldPos - playerPos).normalized;
            if (currentAimDirection == Vector2.zero) currentAimDirection = Vector2.up; 
        }
    }

    void DrawAimConeAndHighlightEnemies()
    {
        // FIX: Explicitly cast to Vector3 to resolve ambiguity errors (CS9342)
        Vector3 playerPos = activePlayer.transform.position;
        float aimAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;

        float halfArc = activePlayer.fireArc / 2f;
        Vector3 edge1 = Quaternion.Euler(0, 0, aimAngle - halfArc) * Vector3.right;
        Vector3 edge2 = Quaternion.Euler(0, 0, aimAngle + halfArc) * Vector3.right;

        Debug.DrawRay(playerPos, edge1 * activePlayer.rangeX, Color.red);
        Debug.DrawRay(playerPos, edge2 * activePlayer.rangeX, Color.red);
        
        Debug.DrawRay(playerPos + edge1 * activePlayer.rangeX, edge1 * (activePlayer.rangeY - activePlayer.rangeX), Color.yellow);
        Debug.DrawRay(playerPos + edge2 * activePlayer.rangeX, edge2 * (activePlayer.rangeY - activePlayer.rangeX), Color.yellow);

        foreach (Piece enemy in enemyPieces)
        {
            if (enemy == null) continue;
            
            // FIX: Using Vector3 for consistency
            Vector3 toEnemy = enemy.transform.position - playerPos;
            float distance = toEnemy.magnitude;
            float angleToEnemy = Vector2.Angle(currentAimDirection, toEnemy);

            if (angleToEnemy <= halfArc && distance <= activePlayer.rangeY)
            {
                enemy.SetTargeted(true);
            }
            else
            {
                enemy.SetTargeted(false);
            }
        }
    }

    void ExecuteMove(BoardNode targetNode)
    {
        activePlayer.MoveTo(targetNode);
        
        if (activePlayer.loadedAmmo < activePlayer.maxAmmo) activePlayer.loadedAmmo++;
        
        StartCoroutine(PassTurnToEnemies());
    }

    IEnumerator ExecuteShootCoroutine()
    {
        isExecutingAction = true;
        activePlayer.loadedAmmo--; 
        
        foreach (Piece enemy in enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }

        float aimAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
        float halfArc = activePlayer.fireArc / 2f;

        for (int i = 0; i < activePlayer.firepower; i++)
        {
            float randomAngle = Random.Range(aimAngle - halfArc, aimAngle + halfArc);
            Quaternion bulletRotation = Quaternion.Euler(0, 0, randomAngle);

            GameObject bulletObj = Instantiate(projectilePrefab, activePlayer.transform.position, bulletRotation);
            Projectile p = bulletObj.GetComponent<Projectile>();
            p.rangeX = activePlayer.rangeX;
            p.rangeY = activePlayer.rangeY;
        }

        // FIX: Upgraded to FindObjectsByType to resolve obsolete warning (CS0618)
        yield return new WaitUntil(() => FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0);

        StartCoroutine(PassTurnToEnemies());
    }

    IEnumerator PassTurnToEnemies()
    {
        enemyPieces.RemoveAll(e => e == null);
        currentTurn = TurnState.EnemyTurn;
        yield return StartCoroutine(EnemyPhaseCoroutine()); 
        isExecutingAction = false;
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
        SpawnPlayer(levelData.playerSpawnPosition.x, levelData.playerSpawnPosition.y);

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
        
        enemyPiece.currentCooldown = startingCooldown; 
        startNode.currentPiece = enemyPiece;
        enemyPieces.Add(enemyPiece);
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
        yield return new WaitForSeconds(0.2f); 

        List<Piece> enemiesToMove = new List<Piece>(enemyPieces);

        foreach (Piece enemy in enemiesToMove)
        {
            if (enemy == null) continue; 

            if (enemy.currentCooldown > 0)
            {
                enemy.currentCooldown--;
            }

            if (enemy.currentCooldown == 0)
            {
                BoardNode targetNode = enemy.GetAIMove(grid);

                if (targetNode != null)
                {
                    if (targetNode.currentPiece == activePlayer)
                    {
                        Debug.Log("GAME OVER! Player was captured!");
                        currentTurn = TurnState.GameOver;
                        grid[enemy.currentX, enemy.currentY].currentPiece = null;
                        enemy.MoveTo(targetNode);
                        targetNode.currentPiece = enemy;
                        yield break; 
                    }

                    grid[enemy.currentX, enemy.currentY].currentPiece = null;
                    enemy.MoveTo(targetNode);
                    targetNode.currentPiece = enemy;

                    enemy.currentCooldown = enemy.maxCooldown;
                }
                else 
                {
                    enemy.currentCooldown = 0;
                }
            }
        }

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