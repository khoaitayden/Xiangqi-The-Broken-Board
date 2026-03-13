using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }
    
    [Header("Campaign Data")]
    [SerializeField] private List<BoardLayoutSO> _campaignLevels;
    [SerializeField] private int _currentLevelIndex = 0;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerGeneralPrefab;
    [SerializeField] private GameObject enemyPawnPrefab; 
    [SerializeField] private GameObject enemyHorsePrefab;
    [SerializeField] private GameObject enemyAdvisorPrefab;
    [SerializeField] private GameObject enemyElephantPrefab;
    [SerializeField] private GameObject enemyGeneralPrefab;
    [SerializeField] private GameObject enemyChariotPrefab;
    [SerializeField] private GameObject enemyCannonPrefab;

    public int CurrentLevelIndex { get { return _currentLevelIndex; } protected set { _currentLevelIndex = value; } }

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    public void StartGame()
    {
        if (GridManager.Instance.grid == null) 
        {
            GridManager.Instance.GenerateBoard();
        }

        _currentLevelIndex = 0;
        
        ClearBoard(); 
        
        TurnManager.Instance.ResetTurnCounter();
        
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
            // --- VICTORY STATE ---
            Debug.Log("YOU BEAT THE ENTIRE CAMPAIGN!");
            TurnManager.Instance.CurrentTurn = TurnManager.TurnState.GameOver;
            UIManager.Instance.ShowWinScreen(); // NEW: Call the Win Screen
        }
    }

    private void ClearBoard()
    {
        TurnManager turnMan = TurnManager.Instance;

        // 1. CLEAR LISTS
        turnMan.enemyPieces.Clear();
        turnMan.activeCorpses.Clear();
        turnMan.activePlayer = null;

        // 2. WIPE GRID DATA
        foreach (BoardNode node in GridManager.Instance.grid)
        {
            node.currentPiece = null;
            node.currentCorpse = null;
        }

        // 3. NUKE ALL PIECES IN THE SCENE
        // FindObjectsByType finds EVERYTHING in the scene, even if it is disabled (Inactive)!
        Piece[] allPieces = FindObjectsByType<Piece>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Piece p in allPieces)
        {
            if (p != null) Destroy(p.gameObject);
        }

        // 4. NUKE ALL CORPSES IN THE SCENE
        Corpse[] allCorpses = FindObjectsByType<Corpse>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Corpse c in allCorpses)
        {
            if (c != null) Destroy(c.gameObject);
        }
    }

    private void LoadCurrentLevel()
    {
        if (_campaignLevels.Count == 0 || _campaignLevels[_currentLevelIndex] == null) return;

        BoardLayoutSO levelData = _campaignLevels[_currentLevelIndex];

        // 1. SPAWN PLAYER
        SpawnPlayer(levelData.playerSpawnPosition.x, levelData.playerSpawnPosition.y);

        // 2. SPAWN ENEMY BOSS (Using the new explicit data)
        SpawnEnemy(enemyGeneralPrefab, levelData.enemyGeneralSpawnPosition.x, levelData.enemyGeneralSpawnPosition.y, levelData.enemyGeneralStartingCooldown);

        // 3. SPAWN ENEMY MINIONS
        foreach (PieceSpawnData spawnData in levelData.enemySpawns)
        {
            GameObject prefabToSpawn = GetPrefabForType(spawnData.pieceType);
            if (prefabToSpawn != null)
            {
                SpawnEnemy(prefabToSpawn, spawnData.position.x, spawnData.position.y, spawnData.startingCooldown);
            }
        }

        // 4. APPLY RUN MANAGER BUFFS (Conscription - Extra Pawns)
        if (RunManager.Instance != null && RunManager.Instance.BonusStartingPawns > 0)
        {
            // Build the list of safe empty spots ONCE
            List<BoardNode> emptyTopNodes = new List<BoardNode>();
            for (int x = 0; x < GridManager.Instance.width; x++)
            {
                for (int y = 5; y < GridManager.Instance.height; y++) 
                {
                    if (GridManager.Instance.grid[x, y].IsEmpty())
                        emptyTopNodes.Add(GridManager.Instance.grid[x, y]);
                }
            }

            // Spawn the pawns and remove the used spots
            for (int i = 0; i < RunManager.Instance.BonusStartingPawns; i++)
            {
                if (emptyTopNodes.Count > 0)
                {
                    int randomIndex = Random.Range(0, emptyTopNodes.Count);
                    BoardNode chosenNode = emptyTopNodes[randomIndex];
                    SpawnEnemy(enemyPawnPrefab, chosenNode.x, chosenNode.y, 3);
                    emptyTopNodes.RemoveAt(randomIndex); // FIX: Remove from list!
                }
            }
        }

        // 5. APPLY RUN MANAGER BUFFS (The Vanguard - Extra Chariots)
        if (RunManager.Instance != null && RunManager.Instance.BonusStartingChariots > 0)
        {
            List<BoardNode> emptyBackNodes = new List<BoardNode>();
            for (int x = 0; x < GridManager.Instance.width; x++)
            {
                for (int y = 8; y <= 9; y++) 
                {
                    if (GridManager.Instance.grid[x, y].IsEmpty())
                        emptyBackNodes.Add(GridManager.Instance.grid[x, y]);
                }
            }

            for (int i = 0; i < RunManager.Instance.BonusStartingChariots; i++)
            {
                if (emptyBackNodes.Count > 0)
                {
                    int randomIndex = Random.Range(0, emptyBackNodes.Count);
                    BoardNode chosenNode = emptyBackNodes[randomIndex];
                    SpawnEnemy(enemyChariotPrefab, chosenNode.x, chosenNode.y, 3);
                    emptyBackNodes.RemoveAt(randomIndex); // FIX: Remove from list!
                }
            }
        }

        // APPLY RUN MANAGER BUFFS (Artillery Backup)
        if (RunManager.Instance != null && RunManager.Instance.BonusStartingCannons > 0)
        {
            List<BoardNode> emptyMidNodes = new List<BoardNode>();
            for (int x = 0; x < GridManager.Instance.width; x++)
            {
                for (int y = 6; y <= 7; y++) 
                {
                    if (GridManager.Instance.grid[x, y].IsEmpty()) emptyMidNodes.Add(GridManager.Instance.grid[x, y]);
                }
            }
            for (int i = 0; i < RunManager.Instance.BonusStartingCannons; i++)
            {
                if (emptyMidNodes.Count > 0)
                {
                    int randomIndex = Random.Range(0, emptyMidNodes.Count);
                    BoardNode chosenNode = emptyMidNodes[randomIndex];
                    SpawnEnemy(enemyCannonPrefab, chosenNode.x, chosenNode.y, 2);
                    emptyMidNodes.RemoveAt(randomIndex); 
                }
            }
        }
        // Reset Turn to Player
        TurnManager.Instance.CurrentTurn = TurnManager.TurnState.PlayerTurn;
        Debug.Log($"Loaded Level {_currentLevelIndex + 1}: {levelData.levelName}");
    }

    private void SpawnRandomExtraChariot()
    {
        List<BoardNode> emptyBackNodes = new List<BoardNode>();
        for (int x = 0; x < GridManager.Instance.width; x++)
        {
            for (int y = 8; y <= 9; y++) 
            {
                if (GridManager.Instance.grid[x, y].IsEmpty())
                {
                    emptyBackNodes.Add(GridManager.Instance.grid[x, y]);
                }
            }
        }

        if (emptyBackNodes.Count > 0)
        {
            BoardNode randomNode = emptyBackNodes[Random.Range(0, emptyBackNodes.Count)];
            SpawnEnemy(enemyChariotPrefab, randomNode.x, randomNode.y, 3);
        }
    }

    private void SpawnRandomExtraPawn()
    {
        List<BoardNode> emptyTopNodes = new List<BoardNode>();
        for (int x = 0; x < GridManager.Instance.width; x++)
        {
            for (int y = 5; y < GridManager.Instance.height; y++) 
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
            // Removed EnemyGeneral from here!
            default: return null;
        }
    }
}