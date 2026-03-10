using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class EnemyGeneral : Piece
{
    private int _pendingDamage = 0;
    private bool _isBatchingDamage = false;
    protected override void Awake()
    {
        base.Awake();
    }

    public override bool IsValidMove(BoardNode targetNode, BoardNode[,] grid)
    {
        // DESPERATION RULE CHECK
        bool leavesPalace = RunManager.Instance != null && RunManager.Instance.BossLeavesPalace;
        bool inBounds = leavesPalace 
            ? (targetNode.y >= 5) // Can move anywhere on their half of the board
            : targetNode.isEnemyPalace; // Restricted to Palace

        if (!inBounds) return false; 

        int absX = Mathf.Abs(targetNode.x - X);
        int absY = Mathf.Abs(targetNode.y - Y);

        if ((absX == 1 && absY == 0) || (absX == 0 && absY == 1))
        {
            if (targetNode.IsEmpty() || (targetNode.currentPiece != null && targetNode.currentPiece.IsPlayer)) return true;
        }
        return false;
    }

    public override BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 };

        bool leavesPalace = RunManager.Instance != null && RunManager.Instance.BossLeavesPalace;

        for (int i = 0; i < 4; i++)
        {
            int tX = X + dx[i];
            int tY = Y + dy[i];

            // DESPERATION BOUNDARY CHECK
            bool inBounds = leavesPalace 
                ? (tX >= 0 && tX <= 8 && tY >= 5 && tY <= 9) 
                : (tX >= 3 && tX <= 5 && tY >= 7 && tY <= 9);

            if (inBounds) 
            {
                BoardNode testNode = grid[tX, tY];
                if (IsValidMove(testNode, grid))
                {
                    if (!testNode.IsEmpty() && testNode.currentPiece.IsPlayer) return testNode;
                    validMoves.Add(testNode);
                }
            }
        }
        return EvaluateAndPickBestMove(validMoves, grid);
    }
    protected override void Die()
    {
        base.Die();
        DraftManager draft = FindFirstObjectByType<DraftManager>();
        if (draft != null)
        {
            draft.ShowDraftScreen();
        }
    }

    public void BeginDamageBatch()
    {
        _pendingDamage = 0;
        _isBatchingDamage = true;
    }

    public void EndDamageBatch()
    {
        _isBatchingDamage = false;
        
        if (_pendingDamage <= 0) return;

        // Apply AdvisorsProtectGeneral ONCE for the whole volley
        if (RunManager.Instance != null && RunManager.Instance.AdvisorsProtectGeneral)
        {
            bool advisorAlive = TurnManager.Instance.enemyPieces
                .Any(p => p is EnemyAdvisor);
            
            if (advisorAlive)
            {
                _pendingDamage = Mathf.Max(0, _pendingDamage - 1);
                Debug.Log("Advisor shielded the General! Reduced volley by 1.");
            }
        }

        base.TakeDamage(_pendingDamage);
    }

    public override void TakeDamage(int damage)
    {
        if (_isBatchingDamage)
        {
            _pendingDamage += damage;  // Just accumulate, don't apply yet
            return;
        }

        // Fallback: single hit (e.g. Flying General instant kill)
        base.TakeDamage(damage);
    }
}