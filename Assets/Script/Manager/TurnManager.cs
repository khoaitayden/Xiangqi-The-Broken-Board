using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState { PlayerTurn, EnemyTurn, GameOver }
    
    [Header("Game State")]
    public TurnState currentTurn = TurnState.PlayerTurn;
    
    public PlayerGeneral activePlayer; 
    public List<Piece> enemyPieces = new List<Piece>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    public void StartEnemyPhase()
    {
        enemyPieces.RemoveAll(e => e == null); // Cleanup dead enemies
        currentTurn = TurnState.EnemyTurn;
        StartCoroutine(EnemyPhaseCoroutine());
    }

    private IEnumerator EnemyPhaseCoroutine()
    {
        yield return new WaitForSeconds(0.2f); 

        List<Piece> enemiesToMove = new List<Piece>(enemyPieces);

        foreach (Piece enemy in enemiesToMove)
        {
            if (enemy == null) continue; 

            if (enemy.currentCooldown > 0) enemy.currentCooldown--;

            if (enemy.currentCooldown == 0)
            {
                BoardNode targetNode = enemy.GetAIMove(GridManager.Instance.grid);

                if (targetNode != null)
                {
                    if (targetNode.currentPiece == activePlayer)
                    {
                        Debug.Log("GAME OVER! Player was captured!");
                        currentTurn = TurnState.GameOver;
                        GridManager.Instance.grid[enemy.currentX, enemy.currentY].currentPiece = null;
                        enemy.MoveTo(targetNode);
                        targetNode.currentPiece = enemy;
                        yield break; 
                    }

                    GridManager.Instance.grid[enemy.currentX, enemy.currentY].currentPiece = null;
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

        if (currentTurn != TurnState.GameOver) currentTurn = TurnState.PlayerTurn;
    }
}