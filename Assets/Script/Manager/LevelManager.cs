using UnityEngine;

public class LevelManager : MonoBehaviour
{
    [Header("Level Data")]
    public BoardLayoutSO currentLevel;

    [Header("Prefabs")]
    public GameObject playerGeneralPrefab;
    public GameObject enemyPawnPrefab; 
    public GameObject enemyHorsePrefab;
    public GameObject enemyAdvisorPrefab;
    public GameObject enemyElephantPrefab;
    public GameObject enemyGeneralPrefab;
    public GameObject enemyChariotPrefab;
    public GameObject enemyCannonPrefab;

    void Start()
    {
        GridManager.Instance.GenerateBoard();
        
        if (currentLevel != null) LoadLevel(currentLevel);
        else Debug.LogError("No Level Data assigned to LevelManager!");
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