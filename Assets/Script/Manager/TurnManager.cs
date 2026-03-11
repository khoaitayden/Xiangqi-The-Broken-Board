using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState { PlayerTurn, EnemyTurn, GameOver, Drafting }
    
    [Header("Game State")]
    [SerializeField] private TurnState _currentTurn = TurnState.PlayerTurn;
    public TurnState CurrentTurn 
    { 
        get { return _currentTurn; } 
        set { _currentTurn = value; } 
    }
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
        // 1. NEW SAFETY CHECK: If we are Drafting (Boss died) or GameOver, do not start the enemy phase!
        if (CurrentTurn == TurnState.Drafting || CurrentTurn == TurnState.GameOver) 
        {
            return; 
        }

        enemyPieces.RemoveAll(e => e == null); // Cleanup dead enemies
        CurrentTurn = TurnState.EnemyTurn;
        StartCoroutine(EnemyPhaseCoroutine());
    }


    private IEnumerator EnemyPhaseCoroutine()
    {
        yield return new WaitForSeconds(0.25f); 

        List<Piece> enemiesToMove = new List<Piece>(enemyPieces);
        bool playerWasExecuted = false;

        foreach (Piece enemy in enemiesToMove)
        {
            if (enemy == null) continue; 

            if (enemy.CurrentCooldown > 0) enemy.CurrentCooldown--;

            if (enemy.CurrentCooldown == 0)
            {
                BoardNode targetNode = enemy.GetAIMove(GridManager.Instance.grid);

                if (targetNode != null)
                {
                    // DID WE HIT THE PLAYER?
                    if (targetNode.currentPiece == activePlayer)
                    {
                        if (activePlayer.CurrentArmor > 0)
                        {
                            // Armor saves you! Instant rewind.
                            TriggerArmorRewind();   
                            yield break; 
                        }
                        else
                        {
                            // NO ARMOR. EXECUTION INITIATED!
                            playerWasExecuted = true;
                            
                            // Move the enemy onto the player
                            GridManager.Instance.grid[enemy.X, enemy.Y].currentPiece = null;
                            enemy.MoveTo(targetNode);
                            targetNode.currentPiece = enemy;
                            
                            // We break the loop so no other enemies move while the execution happens
                            break; 
                        }
                    }

                    // Normal Move
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
                if (activeCorpses[i] == null) 
                {
                    activeCorpses.RemoveAt(i);
                }
            }
        }
        if (playerWasExecuted)
        {
            CurrentTurn = TurnState.GameOver;
            Debug.Log("GAME OVER! You were crushed!");
            //UIManager.Instance.ShowGameOverScreen()
        }
        else if (CurrentTurn == TurnState.EnemyTurn) 
        {
            CurrentTurn = TurnState.PlayerTurn;
            CheckForPlayerThreats();
        }

    }
    public void SaveState()
    {
        previousTurnState = new BoardState(activePlayer, enemyPieces, activeCorpses);
    }

    public void CheckForPlayerThreats()
    {
        if (activePlayer == null) return;

        bool isPlayerInCheck = false;
        BoardNode playerNode = GridManager.Instance.grid[activePlayer.X, activePlayer.Y];

        foreach (Piece enemy in enemyPieces)
        {
            if (enemy == null || enemy.IsDead || !enemy.gameObject.activeInHierarchy) continue;

            // If ANY enemy can legally move to the player's tile right now, the player is in danger!
            if (enemy.IsValidMove(playerNode, GridManager.Instance.grid))
            {
                isPlayerInCheck = true;
                break; // Stop checking, one threat is enough to trigger the warning
            }
        }

        // Tell the player piece to turn its outline on or off
        activePlayer.SetTargeted(isPlayerInCheck);
    }
    public void TriggerArmorRewind()
    {
        // We removed the "if (armor > 0)" check, because the coroutine now only calls this IF armor > 0!
        
        activePlayer.CurrentArmor--;
        Debug.Log($"ARMOR BROKEN! Rewinding... ({activePlayer.CurrentArmor} Left)");

        // 1. Cleanup Future Corpses
        for (int i = activeCorpses.Count - 1; i >= 0; i--)
        {
            Corpse c = activeCorpses[i];
            if (c != null && !previousTurnState.savedCorpses.Contains(c))
            {
                BoardNode node = GridManager.Instance.GetNodeAtPosition(c.transform.position);
                if (node != null && node.currentCorpse == c) node.currentCorpse = null; 
                Destroy(c.gameObject); 
            }
        }
        activeCorpses = new List<Corpse>(previousTurnState.savedCorpses);

        // 2. Clear Board
        foreach (var node in GridManager.Instance.grid) { node.currentPiece = null; }

        // 3. Restore Player
        BoardNode oldPlayerNode = GridManager.Instance.grid[previousTurnState.playerX, previousTurnState.playerY];
        activePlayer.MoveTo(oldPlayerNode); 
        activePlayer.LoadedAmmo = previousTurnState.playerAmmo;

        // 4. Restore Enemies
        enemyPieces.Clear(); 
        foreach (var data in previousTurnState.savedPieces)
        {
            Piece p = data.pieceReference;
            if (p != null)
            {
                enemyPieces.Add(p); 
                p.gameObject.SetActive(true); 
                p.ForceSetStats(data.hp, data.cooldown); 
                BoardNode oldNode = GridManager.Instance.grid[data.x, data.y];
                p.MoveTo(oldNode); 
            }
        }

        CurrentTurn = TurnState.PlayerTurn;
        CheckForPlayerThreats(); 
    }
}