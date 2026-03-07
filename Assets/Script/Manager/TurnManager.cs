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

            if (enemy.CurrentCooldown > 0) enemy.CurrentCooldown--;

            if (enemy.CurrentCooldown == 0)
            {
                BoardNode targetNode = enemy.GetAIMove(GridManager.Instance.grid);

                if (targetNode != null)
                {
                    if (targetNode.currentPiece == activePlayer)
                    {
                        TriggerArmorRewind();   
                        yield break; 
                    }

                    GridManager.Instance.grid[enemy.X, enemy.Y].currentPiece = null;
                    enemy.MoveTo(targetNode);
                    targetNode.currentPiece = enemy;
                    enemy.CurrentCooldown = enemy.MaxCooldown;
                }
                else 
                {
                    enemy.CurrentCooldown = 0;
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
        if (activePlayer.CurrentArmor > 0)
        {
            activePlayer.CurrentArmor--;
            Debug.Log($"ARMOR BROKEN! Rewinding... ({activePlayer.CurrentArmor} Left)");

            // 1. Restore Player
            BoardNode oldPlayerNode = GridManager.Instance.grid[previousTurnState.playerX, previousTurnState.playerY];
            GridManager.Instance.grid[activePlayer.X, activePlayer.Y].currentPiece = null;
            
            // MoveTo handles X/Y assignment internally, so this is fine!
            activePlayer.MoveTo(oldPlayerNode); 
            activePlayer.LoadedAmmo = previousTurnState.playerAmmo;

            // 2. Restore Enemies
            foreach (var data in previousTurnState.savedPieces)
            {
                Piece p = data.pieceReference;
                
                if (p != null)
                {
                    // Ensure the piece is active (in case it died this turn)
                    p.gameObject.SetActive(true);

                    // Clear current pos on grid
                    GridManager.Instance.grid[p.X, p.Y].currentPiece = null;
                    
                    // FIX: Use ForceSetStats instead of p.CurrentHp = ...
                    p.ForceSetStats(data.hp, data.cooldown);
                    
                    // Move Back
                    BoardNode oldNode = GridManager.Instance.grid[data.x, data.y];
                    p.MoveTo(oldNode);
                }
            }

            currentTurn = TurnState.PlayerTurn;
        }
        else
        {
            Debug.Log("GAME OVER! No Armor Left!");
            currentTurn = TurnState.GameOver;
        }
    }
}