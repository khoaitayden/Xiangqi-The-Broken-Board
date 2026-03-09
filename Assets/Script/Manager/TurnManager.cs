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
                if (activeCorpses[i] == null) 
                {
                    activeCorpses.RemoveAt(i);
                }
            }
        }

        // 2. NEW SAFETY CHECK: Only give the turn back to the player if the state is STILL EnemyTurn.
        if (CurrentTurn == TurnState.EnemyTurn) 
        {
            CurrentTurn = TurnState.PlayerTurn;
        }
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
            activeCorpses = new List<Corpse>(previousTurnState.savedCorpses);

            foreach (var node in GridManager.Instance.grid)
            {
                node.currentPiece = null;
            }

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
                    enemyPieces.Add(p); 

                    p.gameObject.SetActive(true);
                    p.ForceSetStats(data.hp, data.cooldown);
                    
                    BoardNode oldNode = GridManager.Instance.grid[data.x, data.y];
                    p.MoveTo(oldNode);
                }
            }

            _currentTurn = TurnState.PlayerTurn;
        }
        else
        {
            Debug.Log("GAME OVER! No Armor Left!");
            _currentTurn = TurnState.GameOver;
        }
    }
}