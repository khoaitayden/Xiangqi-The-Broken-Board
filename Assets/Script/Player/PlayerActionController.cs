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

        if (turnMan.currentTurn != TurnManager.TurnState.PlayerTurn || isExecutingAction || turnMan.activePlayer == null) return;

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
            else if (isAimingMode && turnMan.activePlayer.loadedAmmo > 0)
            {
                StartCoroutine(ExecuteShootCoroutine(turnMan));
            }
            else if (isAimingMode && turnMan.activePlayer.loadedAmmo <= 0)
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

        float halfArc = player.fireArc / 2f;
        Vector3 edge1 = Quaternion.Euler(0, 0, aimAngle - halfArc) * Vector3.right;
        Vector3 edge2 = Quaternion.Euler(0, 0, aimAngle + halfArc) * Vector3.right;

        Debug.DrawRay(playerPos, edge1 * player.rangeX, Color.red);
        Debug.DrawRay(playerPos, edge2 * player.rangeX, Color.red);
        
        Debug.DrawRay(playerPos + edge1 * player.rangeX, edge1 * (player.rangeY - player.rangeX), Color.yellow);
        Debug.DrawRay(playerPos + edge2 * player.rangeX, edge2 * (player.rangeY - player.rangeX), Color.yellow);

        foreach (Piece enemy in turnMan.enemyPieces)
        {
            if (enemy == null) continue;
            
            Vector3 toEnemy = enemy.transform.position - playerPos;
            float distance = toEnemy.magnitude;
            float angleToEnemy = Vector2.Angle(currentAimDirection, toEnemy);

            if (angleToEnemy <= halfArc && distance <= player.rangeY) enemy.SetTargeted(true);
            else enemy.SetTargeted(false);
        }
    }

    void ExecuteMove(BoardNode targetNode, TurnManager turnMan)
    {
        PlayerGeneral player = turnMan.activePlayer;
        GridManager.Instance.grid[player.currentX, player.currentY].currentPiece = null;
        
        player.MoveTo(targetNode);
        
        if (player.loadedAmmo < player.maxAmmo) player.loadedAmmo++;
        
        turnMan.StartEnemyPhase();
    }

    IEnumerator ExecuteShootCoroutine(TurnManager turnMan)
    {
        isExecutingAction = true;
        PlayerGeneral player = turnMan.activePlayer;
        player.loadedAmmo--; 
        
        foreach (Piece enemy in turnMan.enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }

        float aimAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
        float halfArc = player.fireArc / 2f;

        for (int i = 0; i < player.firepower; i++)
        {
            float randomAngle = Random.Range(aimAngle - halfArc, aimAngle + halfArc);
            Quaternion bulletRotation = Quaternion.Euler(0, 0, randomAngle);

            GameObject bulletObj = Instantiate(projectilePrefab, player.transform.position, bulletRotation);
            Projectile p = bulletObj.GetComponent<Projectile>();
            p.rangeX = player.rangeX;
            p.rangeY = player.rangeY;
        }

        yield return new WaitUntil(() => FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0);

        isExecutingAction = false;
        turnMan.StartEnemyPhase();
    }
}