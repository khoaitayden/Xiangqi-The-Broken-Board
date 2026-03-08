using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    [Header("Campaign Data")]
    [SerializeField] private List<BoardLayoutSO> _campaignLevels;
    [SerializeField] private int _currentLevelIndex = 0;

    [Header("Prefabs")]
    public GameObject playerGeneralPrefab;
    public GameObject enemyPawnPrefab; 
    public GameObject enemyHorsePrefab;
    public GameObject enemyAdvisorPrefab;
    public GameObject enemyElephantPrefab;
    public GameObject enemyGeneralPrefab;
    public GameObject enemyChariotPrefab;
    public GameObject enemyCannonPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    void Start()
    {
        GridManager.Instance.GenerateBoard();
        LoadCurrentLevel();
    }

    public void LoadNextLevel()
    {
        _currentLevelIndex++;
        
        if (_currentLevelIndex < _campaignLevels.Count)
        {
            ClearBoard();
            LoadCurrentLevel();
        }
        else
        {
            Debug.Log("YOU BEAT THE ENTIRE CAMPAIGN!");
            TurnManager.Instance.CurrentTurn = TurnManager.TurnState.GameOver;
        }
    }

    private void ClearBoard()
    {
        TurnManager turnMan = TurnManager.Instance;

        // 1. Destroy all enemies
        foreach (Piece enemy in turnMan.enemyPieces)
        {
            if (enemy != null) Destroy(enemy.gameObject);
        }
        turnMan.enemyPieces.Clear();

        // 2. Destroy all corpses
        foreach (Corpse corpse in turnMan.activeCorpses)
        {
            if (corpse != null) Destroy(corpse.gameObject);
        }
        turnMan.activeCorpses.Clear();

        // 3. Destroy Player
        if (turnMan.activePlayer != null)
        {
            Destroy(turnMan.activePlayer.gameObject);
            turnMan.activePlayer = null;
        }

        // 4. Wipe Grid Data
        foreach (BoardNode node in GridManager.Instance.grid)
        {
            node.currentPiece = null;
            node.currentCorpse = null;
        }
    }

    private void LoadCurrentLevel()
    {
        if (_campaignLevels.Count == 0 || _campaignLevels[_currentLevelIndex] == null) return;

        BoardLayoutSO levelData = _campaignLevels[_currentLevelIndex];

        SpawnPlayer(levelData.playerSpawnPosition.x, levelData.playerSpawnPosition.y);

        foreach (PieceSpawnData spawnData in levelData.enemySpawns)
        {
            GameObject prefabToSpawn = GetPrefabForType(spawnData.pieceType);
            if (prefabToSpawn != null)
            {
                SpawnEnemy(prefabToSpawn, spawnData.position.x, spawnData.position.y, spawnData.startingCooldown);
            }
        }

        // APPLY RUN MANAGER BUFFS (Conscription)
        if (RunManager.Instance != null && RunManager.Instance.BonusStartingPawns > 0)
        {
            for (int i = 0; i < RunManager.Instance.BonusStartingPawns; i++)
            {
                SpawnRandomExtraPawn();
            }
        }

        // Reset Turn to Player
        TurnManager.Instance.CurrentTurn = TurnManager.TurnState.PlayerTurn;
        Debug.Log($"Loaded Level {_currentLevelIndex + 1}: {levelData.levelName}");
    }

    private void SpawnRandomExtraPawn()
    {
        List<BoardNode> emptyTopNodes = new List<BoardNode>();
        for (int x = 0; x < GridManager.Instance.width; x++)
        {
            for (int y = 5; y < GridManager.Instance.height; y++) // Top half
            {
                if (GridManager.Instance.grid[x, y].IsEmpty())
                {
                    emptyTopNodes.Add(GridManager.Instance.grid[x, y]);
                }
            }
        }

        if (emptyTopNodes.Count > 0)
        {
            BoardNode randomNode = emptyTopNodes[Random.Range(0, emptyTopNodes.Count)];
            SpawnEnemy(enemyPawnPrefab, randomNode.x, randomNode.y, 3);
        }
    }
    void SpawnPlayer(int startX, int startY) 
    {
        BoardNode startNode = GridManager.Instance.grid[startX, startY];
        GameObject playerObj = Instantiate(playerGeneralPrefab, startNode.nodeGameObject.transform.position, Quaternion.identity);
        PlayerGeneral player = playerObj.GetComponent<PlayerGeneral>();
        
        player.InitPosition(startX, startY, startNode);
        
        TurnManager.Instance.activePlayer = player; 
    }

    void SpawnEnemy(GameObject prefab, int startX, int startY, int startingCooldown)
    {
        BoardNode startNode = GridManager.Instance.grid[startX, startY];
        GameObject enemyObj = Instantiate(prefab, startNode.nodeGameObject.transform.position, Quaternion.identity);
        Piece enemyPiece = enemyObj.GetComponent<Piece>();
        
        enemyPiece.InitPosition(startX, startY, startNode);
        
        enemyPiece.CurrentCooldown = startingCooldown; 
        
        TurnManager.Instance.enemyPieces.Add(enemyPiece); 
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
}