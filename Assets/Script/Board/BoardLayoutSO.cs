using UnityEngine;
using System.Collections.Generic;

// 1. Define all the piece types we have
public enum PieceType 
{ 
    Pawn, 
    Horse, 
    Chariot, 
    Elephant, 
    Advisor, 
    Cannon, 
    EnemyGeneral 
}

// 2. A tiny struct to hold the data for a single enemy spawn
[System.Serializable]
public class PieceSpawnData
{
    public PieceType pieceType;
    public Vector2Int position; // X and Y coordinates
    public int startingCooldown;
}

// 3. The actual ScriptableObject that holds the whole level
[CreateAssetMenu(fileName = "NewBoardLayout", menuName = "Xiangqi/Board Layout")]
public class BoardLayoutSO : ScriptableObject
{
    [Header("Level Info")]
    public string levelName = "Floor 1";

    [Header("Player Settings")]
    public Vector2Int playerSpawnPosition = new Vector2Int(4, 0); // Default bottom center

    [Header("Enemy Layout")]
    public List<PieceSpawnData> enemySpawns = new List<PieceSpawnData>();
}