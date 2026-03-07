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
        previousTurnState = new BoardState(activePlayer, enemyPieces, activeCorpses);
    }
    public void TriggerArmorRewind()
    {
        if (activePlayer.CurrentArmor > 0)
        {
            activePlayer.CurrentArmor--;
            Debug.Log($"ARMOR BROKEN! Rewinding... ({activePlayer.CurrentArmor} Left)");

            // --- 1. CLEANUP "FUTURE" CORPSES ---
            for (int i = activeCorpses.Count - 1; i >= 0; i--)
            {
                Corpse c = activeCorpses[i];
                // If this corpse wasn't in our save file, it was created THIS turn. Erase it!
                if (c != null && !previousTurnState.savedCorpses.Contains(c))
                {
                    BoardNode node = GridManager.Instance.GetNodeAtPosition(c.transform.position);
                    if (node != null && node.currentCorpse == c) node.currentCorpse = null; // Free the grid space
                    
                    Destroy(c.gameObject); // Destroy the clone
                }
            }
            // Reset the active corpse list to exactly what it was
            activeCorpses = new List<Corpse>(previousTurnState.savedCorpses);

            // --- 2. CLEAR ENTIRE BOARD OF ALIVE PIECES ---
            // This prevents duplicate pieces from getting stuck on nodes
            foreach (var node in GridManager.Instance.grid)
            {
                node.currentPiece = null;
            }

            // --- 3. RESTORE PLAYER ---
            BoardNode oldPlayerNode = GridManager.Instance.grid[previousTurnState.playerX, previousTurnState.playerY];
            activePlayer.MoveTo(oldPlayerNode); 
            activePlayer.LoadedAmmo = previousTurnState.playerAmmo;

            // --- 4. RESTORE ENEMIES ---
            enemyPieces.Clear(); // Empty the current list
            foreach (var data in previousTurnState.savedPieces)
            {
                Piece p = data.pieceReference;
                
                if (p != null)
                {
                    enemyPieces.Add(p); // Put the enemy BACK into the turn order list

                    p.gameObject.SetActive(true); // Unhide if it died
                    p.ForceSetStats(data.hp, data.cooldown); // Revive stats
                    
                    BoardNode oldNode = GridManager.Instance.grid[data.x, data.y];
                    p.MoveTo(oldNode); // Move back to original spot
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