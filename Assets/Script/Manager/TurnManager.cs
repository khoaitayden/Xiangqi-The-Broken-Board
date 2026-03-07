using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState { PlayerTurn, EnemyTurn, GameOver }
    
    [Header("Game State")]
    public TurnState currentTurn = TurnState.PlayerTurn;
    private BoardState previousTurnState;
    
    public PlayerGeneral activePlayer; 
    public List<Piece> enemyPieces = new List<Piece>();
    public List<Corpse> activeCorpses = new List<Corpse>();
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
                        TriggerArmorRewind();   
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

        for (int i = activeCorpses.Count - 1; i >= 0; i--)
        {
            if (activeCorpses[i] != null)
            {
                activeCorpses[i].Decay();
                if (activeCorpses[i] == null) // It was destroyed by Decay()
                {
                    activeCorpses.RemoveAt(i);
                }
            }
        }

        if (currentTurn != TurnState.GameOver) currentTurn = TurnState.PlayerTurn;
    }
    public void SaveState()
    {
        previousTurnState = new BoardState(activePlayer, enemyPieces);
    }
    public void TriggerArmorRewind()
    {
        if (activePlayer.currentArmor > 0)
        {
            activePlayer.currentArmor--;
            Debug.Log($"ARMOR BROKEN! Rewinding Turn... ({activePlayer.currentArmor} Left)");

            // 1. Restore Player
            BoardNode oldPlayerNode = GridManager.Instance.grid[previousTurnState.playerX, previousTurnState.playerY];
            // Clear current pos
            GridManager.Instance.grid[activePlayer.currentX, activePlayer.currentY].currentPiece = null;
            // Move back
            activePlayer.MoveTo(oldPlayerNode); 
            activePlayer.loadedAmmo = previousTurnState.playerAmmo;

            // 2. Restore Enemies
            foreach (var data in previousTurnState.savedPieces)
            {
                Piece p = data.pieceReference;
                
                // If the piece is somehow null (destroyed completely), we can't restore it easily without respawning.
                // ideally, we disable pieces instead of destroying them until the turn is fully over.
                if (p != null)
                {
                    // Clear current pos
                    GridManager.Instance.grid[p.currentX, p.currentY].currentPiece = null;
                    
                    // Restore Stats
                    p.currentHp = data.hp;
                    p.currentCooldown = data.cooldown;
                    
                    // Move Back
                    BoardNode oldNode = GridManager.Instance.grid[data.x, data.y];
                    p.MoveTo(oldNode);
                }
            }

            // 3. Reset Turn to Player
            currentTurn = TurnState.PlayerTurn;
            
            // TODO: Play Screen Shake / Shatter Sound Effect here!
        }
        else
        {
            Debug.Log("GAME OVER! No Armor Left!");
            currentTurn = TurnState.GameOver;
        }
    }
}