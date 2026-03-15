using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum TurnState { MainMenu, PlayerTurn, EnemyTurn, GameOver, Drafting, Paused } 
    
    [Header("Game State")]
    [SerializeField] private TurnState _currentTurn = TurnState.MainMenu; 
    public TurnState CurrentTurn 
    { 
        get { return _currentTurn; } 
        set { _currentTurn = value; } 
    }
    private TurnState _stateBeforePause; 
    private BoardState previousTurnState;
    public int CurrentTurnNumber { get; private set; } = 1;
    public PlayerGeneral activePlayer; 
    public List<Piece> enemyPieces = new List<Piece>();
    public List<Corpse> activeCorpses = new List<Corpse>();
    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }
    private void Start()
    {
        CurrentTurnNumber = 1;
    }


    public void StartEnemyPhase()
    {
        if (CurrentTurn == TurnState.Drafting || CurrentTurn == TurnState.GameOver) return;

        CurrentTurnNumber++;

        enemyPieces.RemoveAll(e => e == null); 
        CurrentTurn = TurnState.EnemyTurn;
        StartCoroutine(EnemyPhaseCoroutine());
    }


    private IEnumerator EnemyPhaseCoroutine()
    {
        yield return new WaitForSeconds(0.25f); 

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
                        if (activePlayer.CurrentArmor > 0)
                        {
                            TriggerArmorRewind();   
                            yield break; 
                        }
                        else
                        {
                            // --- RE-ORCHESTRATED DEATH SEQUENCE ---
                            // 1. Start the enemy's jump and WAIT for it to land.
                            Coroutine deathJump = enemy.MoveTo(targetNode);
                            yield return deathJump;

                            // 2. The moment it lands, SHAKE THE SCREEN!
                            ScreenShakeManager.Instance.ShakeScreen(2f);

                            // 3. NOW, tell the enemy piece to start its fade-out animation.
                            enemy.StartFadeOut();

                            // 4. Wait for the dust to settle as the enemy fades.
                            yield return new WaitForSeconds(0.75f);
                            
                            // 5. Set the game state and show the UI.
                            CurrentTurn = TurnState.GameOver;
                            Debug.Log("GAME OVER! You were crushed!");
                            UIManager.Instance.ShowDeathScreen();
                            
                            yield break; 
                        }
                    }

                    // Normal Move
                    GridManager.Instance.grid[enemy.X, enemy.Y].currentPiece = null;
                    enemy.MoveTo(targetNode);
                    enemy.CurrentCooldown = enemy.MaxCooldown;
                }
                else 
                {
                    enemy.CurrentCooldown = 0;
                }
            }
        }

        // --- Standard End-of-Turn Cleanup ---
        for (int i = activeCorpses.Count - 1; i >= 0; i--)
        {
            if (activeCorpses[i] != null)
            {
                activeCorpses[i].Decay();
                if (activeCorpses[i] == null) activeCorpses.RemoveAt(i);
            }
        }
        
        if (CurrentTurn == TurnState.EnemyTurn) 
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
    public void ResetTurnCounter()
    {
        CurrentTurnNumber = 1;
    }
    public void PauseGame()
    {
        if (CurrentTurn == TurnState.Paused || CurrentTurn == TurnState.MainMenu) return;
        
        _stateBeforePause = CurrentTurn;
        CurrentTurn = TurnState.Paused;
    }

    public void ResumeGame()
    {
        if (CurrentTurn == TurnState.Paused)
        {
            CurrentTurn = _stateBeforePause;
        }
    }
}