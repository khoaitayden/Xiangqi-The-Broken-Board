using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    public enum TurnState { PlayerTurn, EnemyTurn, GameOver }
    
    [Header("Game State")]
    public TurnState currentTurn = TurnState.PlayerTurn;

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
        SpawnPlayer(); 
        
        // Spawn a full traditional Enemy backline!
        SpawnEnemy(enemyChariotPrefab, 0, 9);
        SpawnEnemy(enemyHorsePrefab, 1, 9);
        SpawnEnemy(enemyElephantPrefab, 2, 9);
        SpawnEnemy(enemyAdvisorPrefab, 3, 9);
        SpawnEnemy(enemyGeneralPrefab, 4, 9);
        SpawnEnemy(enemyAdvisorPrefab, 5, 9);
        SpawnEnemy(enemyElephantPrefab, 6, 9);
        SpawnEnemy(enemyHorsePrefab, 7, 9);
        SpawnEnemy(enemyChariotPrefab, 8, 9);

        // Spawn Cannons
        SpawnEnemy(enemyCannonPrefab, 1, 7);
        SpawnEnemy(enemyCannonPrefab, 7, 7);

        // Spawn Pawns
        SpawnEnemy(enemyPawnPrefab, 0, 6);
        SpawnEnemy(enemyPawnPrefab, 2, 6);
        SpawnEnemy(enemyPawnPrefab, 4, 6);
        SpawnEnemy(enemyPawnPrefab, 6, 6);
        SpawnEnemy(enemyPawnPrefab, 8, 6);
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
        GameObject playerObj = Instantiate(playerGeneralPrefab, startNode.nodeGameObject.transform.position, Quaternion.identity);
        activePlayer = playerObj.GetComponent<PlayerGeneral>();
        activePlayer.currentX = startNode.x;
        activePlayer.currentY = startNode.y;
        startNode.currentPiece = activePlayer;
    }

    void SpawnEnemy(GameObject prefab, int startX, int startY)
    {
        BoardNode startNode = grid[startX, startY];
        GameObject enemyObj = Instantiate(prefab, startNode.nodeGameObject.transform.position, Quaternion.identity);
        Piece enemyPiece = enemyObj.GetComponent<Piece>();
        
        enemyPiece.currentX = startX;
        enemyPiece.currentY = startY;
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

    // UPDATED: Only ONE enemy moves per turn
    IEnumerator EnemyPhaseCoroutine()
    {
        yield return new WaitForSeconds(0.2f); // Tiny delay for board game feel

        List<AIMove> possibleMoves = new List<AIMove>();
        AIMove winningMove = null;

        // 1. Gather all possible moves for all surviving enemies
        foreach (Piece enemy in enemyPieces)
        {
            if (enemy == null) continue; // Skip dead pieces

            BoardNode targetNode = enemy.GetAIMove(grid);

            if (targetNode != null)
            {
                AIMove move = new AIMove { piece = enemy, targetNode = targetNode };
                possibleMoves.Add(move);

                // If this move captures the player, save it as the winning move!
                if (targetNode.currentPiece == activePlayer)
                {
                    winningMove = move;
                }
            }
        }

        // 2. Decide which move to take
        AIMove chosenMove = null;

        if (winningMove != null)
        {
            // Always take the kill if it's available
            chosenMove = winningMove;
        }
        else if (possibleMoves.Count > 0)
        {
            // Otherwise, pick a random valid move from the list
            int randomIndex = Random.Range(0, possibleMoves.Count);
            chosenMove = possibleMoves[randomIndex];
        }

        // 3. Execute the chosen move
        if (chosenMove != null)
        {
            if (chosenMove.targetNode.currentPiece == activePlayer)
            {
                Debug.Log("GAME OVER! Player was captured!");
                currentTurn = TurnState.GameOver;
            }

            // Move the chosen piece physically and in the data grid
            grid[chosenMove.piece.currentX, chosenMove.piece.currentY].currentPiece = null;
            chosenMove.piece.MoveTo(chosenMove.targetNode);
            chosenMove.targetNode.currentPiece = chosenMove.piece;
        }

        // 4. Pass turn back to player
        if (currentTurn != TurnState.GameOver)
        {
            currentTurn = TurnState.PlayerTurn;
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