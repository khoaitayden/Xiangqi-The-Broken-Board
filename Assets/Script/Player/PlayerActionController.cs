using UnityEngine;
using System.Collections;

public class PlayerActionController : MonoBehaviour
{
    [Header("Aiming & Shooting")]
    public GameObject projectilePrefab; 
    
    private bool isAimingMode = false;
    private Vector2 currentAimDirection;
    private bool isExecutingAction = false; 

    void Update()
    {
        TurnManager turnMan = TurnManager.Instance;
        GridManager gridMan = GridManager.Instance;

        if (turnMan.CurrentTurn != TurnManager.TurnState.PlayerTurn || isExecutingAction || turnMan.activePlayer == null) return;

        Vector2 worldPosition = InputHandler.Instance.MouseWorldPosition;
        BoardNode hoveredNode = gridMan.GetNodeAtPosition(worldPosition);

        DetermineInputContext(worldPosition, hoveredNode, turnMan.activePlayer, gridMan);

        if (isAimingMode)
        {
            DrawAimConeAndHighlightEnemies(turnMan);
        }

        if (InputHandler.Instance.IsClickTriggered) 
        {
            if (!isAimingMode && hoveredNode != null)
            {
                ExecuteMove(hoveredNode, turnMan);
            }
            else if (isAimingMode && turnMan.activePlayer.LoadedAmmo > 0)
            {
                StartCoroutine(ExecuteShootCoroutine(turnMan));
            }
            else if (isAimingMode && turnMan.activePlayer.LoadedAmmo <= 0)
            {
                Debug.Log("Out of Ammo! Move to Reload!");
            }
        }
    }

    void DetermineInputContext(Vector2 mouseWorldPos, BoardNode hoveredNode, PlayerGeneral player, GridManager gridMan)
    {
        if (hoveredNode != null && player.IsValidMove(hoveredNode, gridMan.grid))
        {
            isAimingMode = false;
            foreach (Piece enemy in TurnManager.Instance.enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }
        }
        else
        {
            isAimingMode = true;
            Vector2 playerPos = player.transform.position;
            currentAimDirection = (mouseWorldPos - playerPos).normalized;
            if (currentAimDirection == Vector2.zero) currentAimDirection = Vector2.up; 
        }
    }

    void DrawAimConeAndHighlightEnemies(TurnManager turnMan)
    {
        PlayerGeneral player = turnMan.activePlayer;
        Vector3 playerPos = player.transform.position;
        float aimAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;

        float halfArc = player.FireArc / 2f;
        Vector3 edge1 = Quaternion.Euler(0, 0, aimAngle - halfArc) * Vector3.right;
        Vector3 edge2 = Quaternion.Euler(0, 0, aimAngle + halfArc) * Vector3.right;

        Debug.DrawRay(playerPos, edge1 * player.RangeX, Color.red);
        Debug.DrawRay(playerPos, edge2 * player.RangeX, Color.red);
        
        Debug.DrawRay(playerPos + edge1 * player.RangeX, edge1 * (player.RangeY - player.RangeX), Color.yellow);
        Debug.DrawRay(playerPos + edge2 * player.RangeX, edge2 * (player.RangeY - player.RangeX), Color.yellow);

        foreach (Piece enemy in turnMan.enemyPieces)
        {
            if (enemy == null) continue;
            
            Vector3 toEnemy = enemy.transform.position - playerPos;
            float distance = toEnemy.magnitude;
            float angleToEnemy = Vector2.Angle(currentAimDirection, toEnemy);

            if (angleToEnemy <= halfArc && distance <= player.RangeY) enemy.SetTargeted(true);
            else enemy.SetTargeted(false);
        }
    }

    void ExecuteMove(BoardNode targetNode, TurnManager turnMan)
    {
        turnMan.SaveState();
        PlayerGeneral player = turnMan.activePlayer;

        // Calculate if the move was diagonal before we actually move
        bool isDiagonalMove = Mathf.Abs(targetNode.x - player.X) == 1 && Mathf.Abs(targetNode.y - player.Y) == 1;

        GridManager.Instance.grid[player.X, player.Y].currentPiece = null;
        player.MoveTo(targetNode);
        
        // --- RELOAD LOGIC (THE RED HARE) ---
        int ammoToRecover = 1;

        if (RunManager.Instance != null && RunManager.Instance.RedHareEnabled && isDiagonalMove)
        {
            ammoToRecover = 2; // Red Hare Bonus!
        }

        // Add ammo, clamping it so we don't go over Max Ammo
        player.LoadedAmmo = Mathf.Min(player.LoadedAmmo + ammoToRecover, player.MaxAmmo);
        
        Debug.Log("Current Ammo: " + player.LoadedAmmo);
        
        turnMan.StartEnemyPhase();
    }

    IEnumerator ExecuteShootCoroutine(TurnManager turnMan)
    {
        turnMan.SaveState();
        isExecutingAction = true;
        PlayerGeneral player = turnMan.activePlayer;
        player.LoadedAmmo--; 
        Debug.Log("Current Ammo: "+player.LoadedAmmo);

        
        foreach (Piece enemy in turnMan.enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }

        float aimAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
        float halfArc = player.FireArc / 2f;

        for (int i = 0; i < player.Firepower; i++)
        {
            float randomAngle = Random.Range(aimAngle - halfArc, aimAngle + halfArc);
            Quaternion bulletRotation = Quaternion.Euler(0, 0, randomAngle);

            GameObject bulletObj = Instantiate(projectilePrefab, player.transform.position, bulletRotation);
            Projectile p = bulletObj.GetComponent<Projectile>();
            p.rangeX = player.RangeX;
            p.rangeY = player.RangeY;
        }

        yield return new WaitUntil(() => FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0);

        isExecutingAction = false;
        turnMan.StartEnemyPhase();
    }
}