using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerActionController : MonoBehaviour
{
    [Header("Aiming & Shooting")]
    public GameObject projectilePrefab; 
    public AimVisualizer aimVisualizer;
    
    private bool isAimingMode = false;
    private Vector2 currentAimDirection;
    private bool isExecutingAction = false; 

    private enum SpecialShotMode { None, CrouchingTiger, FlyingGeneral }
    private SpecialShotMode currentShotMode = SpecialShotMode.None;

    void Update()
    {
        TurnManager turnMan = TurnManager.Instance;
        GridManager gridMan = GridManager.Instance;

        if (turnMan.CurrentTurn != TurnManager.TurnState.PlayerTurn || isExecutingAction || turnMan.activePlayer == null)
        {
            if (aimVisualizer != null) aimVisualizer.Hide();
            gridMan.ClearAllHighlights(); 
            return;
        }

        Vector2 worldPosition = InputHandler.Instance.MouseWorldPosition;
        BoardNode hoveredNode = gridMan.GetNodeAtPosition(worldPosition);

        if (hoveredNode == null)
        {
            if (aimVisualizer != null) aimVisualizer.Hide();
            gridMan.ClearAllHighlights();
            return; 
        }

        // --- DETERMINE CONTEXT ---
        DetermineInputContext(worldPosition, hoveredNode, turnMan.activePlayer, gridMan);

        // --- HANDLE VISUALS BASED ON CONTEXT ---
        if (isAimingMode)
        {
            DrawAimConeAndHighlightEnemies(turnMan, worldPosition);
            gridMan.UpdateHoverHighlight(hoveredNode);
        }
        else
        {
            if (aimVisualizer != null) aimVisualizer.Hide();
            gridMan.UpdatePlayerMoveHighlight(turnMan.activePlayer); 
        }

        // --- HANDLE CLICKS ---
        if (InputHandler.Instance.IsClickTriggered) 
        {
            bool hasAmmo = turnMan.activePlayer.LoadedAmmo > 0;
            bool artOfWarReady = RunManager.Instance.ArtOfWarEnabled && !RunManager.Instance.ArtOfWarUsedThisFloor;
            bool canShoot = hasAmmo || artOfWarReady;

            // THE FIX: Add a final check to make sure the move is valid before executing it!
            if (!isAimingMode && hoveredNode != null && turnMan.activePlayer.IsValidMove(hoveredNode, gridMan.grid))
            {
                ExecuteMove(hoveredNode, turnMan);
            }
            else if (isAimingMode && canShoot)
            {
                if (!hasAmmo && artOfWarReady)
                {
                    RunManager.Instance.ArtOfWarUsedThisFloor = true;
                    Debug.Log("Art of War used! Fired with 0 Ammo!");
                }
                else
                {
                    turnMan.activePlayer.LoadedAmmo--; 
                }

                StartCoroutine(ExecuteShootCoroutine(turnMan));
            }
        }
    }

    void DetermineInputContext(Vector2 mouseWorldPos, BoardNode hoveredNode, PlayerGeneral player, GridManager gridMan)
    {
        currentShotMode = SpecialShotMode.None; // Reset

        // ALWAYS make the weapon follow the mouse
        currentAimDirection = (mouseWorldPos - (Vector2)player.transform.position).normalized;
        if (currentAimDirection == Vector2.zero) currentAimDirection = Vector2.up; 
        if (player.WeaponPivot != null)
        {
            float angle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
            player.WeaponPivot.rotation = Quaternion.Euler(0, 0, angle);
        }

        // --- THE FIX: Check for the INVALID ADJACENT CORPSE case first ---
        int distX = Mathf.Abs(hoveredNode.x - player.X);
        int distY = Mathf.Abs(hoveredNode.y - player.Y);
        bool isAdjacent = distX <= 1 && distY <= 1 && (distX > 0 || distY > 0);
        bool hasCorpse = hoveredNode.currentCorpse != null;
        bool canStepOnCorpse = RunManager.Instance.CloudStepEnabled;

        if (isAdjacent && hasCorpse && !canStepOnCorpse)
        {
            // This is an invalid tile! We can't move or shoot.
            isAimingMode = false;
            foreach (Piece enemy in TurnManager.Instance.enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }
            return; // Exit the function early so we don't do any other logic.
        }

        // --- Now, determine if we are moving or aiming ---
        if (hoveredNode != null && player.IsValidMove(hoveredNode, gridMan.grid))
        {
            isAimingMode = false;
            foreach (Piece enemy in TurnManager.Instance.enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }
        }
        else
        {
            isAimingMode = true;
            
            // Check for Crouching Tiger
            if (hoveredNode != null && RunManager.Instance.CrouchingTigerEnabled)
            {
                // Note: We already checked for adjacent corpses above, so this will only trigger on adjacent enemies.
                if (isAdjacent && hoveredNode.currentPiece != null)
                {
                    currentShotMode = SpecialShotMode.CrouchingTiger;
                }
            }

            // Check for Flying General
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null && player.X == enemyBoss.X) 
            {
                int minY = Mathf.Min(player.Y, enemyBoss.Y);
                int maxY = Mathf.Max(player.Y, enemyBoss.Y);
                int blockers = 0;
                for (int y = minY + 1; y < maxY; y++)
                {
                    if (!gridMan.grid[player.X, y].IsEmpty()) blockers++;
                }
                int allowedBlockers = RunManager.Instance.MandateOfHeavenEnabled ? 1 : 0;
                
                if (blockers <= allowedBlockers)
                {
                    Vector2 directionToBoss = (enemyBoss.transform.position - player.transform.position).normalized;
                    float angleDifference = Vector2.Angle(currentAimDirection, directionToBoss);
                    const float aimTolerance = 30f; 

                    if (angleDifference < aimTolerance)
                    {
                        currentShotMode = SpecialShotMode.FlyingGeneral;
                    }
                }
            }
        }
    }

    void DrawAimConeAndHighlightEnemies(TurnManager turnMan, Vector2 mouseWorldPos)
    {
        PlayerGeneral player = turnMan.activePlayer;
        Vector3 playerPos = player.transform.position;
        
        foreach (Piece enemy in turnMan.enemyPieces) { if (enemy != null) enemy.SetTargeted(false); }

        if (currentShotMode == SpecialShotMode.FlyingGeneral)
        {
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null && aimVisualizer != null)
            {
                Vector2 directionToBoss = (enemyBoss.transform.position - playerPos).normalized;
                float distanceToBoss = Vector3.Distance(playerPos, enemyBoss.transform.position);
                aimVisualizer.DrawLine(playerPos, directionToBoss, distanceToBoss, 0.15f, new Color(1f, 0f, 1f, 0.4f));
                enemyBoss.SetTargeted(true);
            }
        }
        else if (currentShotMode == SpecialShotMode.CrouchingTiger)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(playerPos, currentAimDirection, 15f);
            
            int hitCount = 0;
            foreach (var hit in hits)
            {
                Piece hitPiece = hit.collider.GetComponent<Piece>();
                
                if (hitPiece != null && !hitPiece.IsPlayer)
                {
                    hitCount++;
                    aimVisualizer.DrawLine(playerPos, currentAimDirection, 15f, 0.1f, new Color(1f, 0.3f, 0f, 0.5f));
                    if (hitCount == 2) { hitPiece.SetTargeted(true); break; } 
                }
                else if (hit.collider.GetComponent<Corpse>() != null)
                {
                    hitCount++;
                    aimVisualizer.DrawLine(playerPos, currentAimDirection, 15f, 0.1f, new Color(1f, 0.3f, 0f, 0.5f));
                }
            }
        }
        else 
        {
            float distanceToMouse = Vector2.Distance(playerPos, mouseWorldPos);
            float currentRangeX = Mathf.Min(distanceToMouse, player.RangeX);
            float currentRangeY = Mathf.Max(currentRangeX, Mathf.Min(distanceToMouse, player.RangeY));

            if (aimVisualizer != null)
            {
                aimVisualizer.DrawCone(playerPos, currentAimDirection, player.FireArc, currentRangeX, currentRangeY);
            }

            float aimAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
            float halfArc = player.FireArc / 2f;
            foreach (Piece enemy in turnMan.enemyPieces)
            {
                if (enemy == null) continue;
                Vector3 toEnemy = enemy.transform.position - playerPos;
                float distance = toEnemy.magnitude;
                float angleToEnemy = Vector2.Angle(currentAimDirection, toEnemy);

                if (angleToEnemy <= halfArc && distance <= player.RangeY) enemy.SetTargeted(true);
            }
        }
    }

    IEnumerator ExecuteShootCoroutine(TurnManager turnMan)
    {
        turnMan.SaveState();
        isExecutingAction = true;
        PlayerGeneral player = turnMan.activePlayer;
        
        foreach (Piece enemy in turnMan.enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }

        if (currentShotMode == SpecialShotMode.FlyingGeneral)
        {
            Debug.Log("FLYING GENERAL EXECUTION!");
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null) enemyBoss.TakeDamage(999);
            yield return new WaitForSeconds(0.5f);
        }
        else if (currentShotMode == SpecialShotMode.CrouchingTiger)
        {
            Debug.Log("CROUCHING TIGER BEAM!");
            RaycastHit2D[] hits = Physics2D.RaycastAll(player.transform.position, currentAimDirection, 15f);
            
            int hitCount = 0;
            foreach (var hit in hits)
            {
                Piece hitPiece = hit.collider.GetComponent<Piece>();
                
                if (hitPiece != null && !hitPiece.IsPlayer)
                {
                    hitCount++;
                    if (hitCount == 2) { hitPiece.TakeDamage(3); break; } 
                }
                else if (hit.collider.GetComponent<Corpse>() != null)
                {
                    hitCount++;
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null) enemyBoss.BeginDamageBatch();

            float aimAngle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
            float halfArc = player.FireArc / 2f;

            for (int i = 0; i < player.Firepower; i++)
            {
                float randomAngle = Random.Range(aimAngle - halfArc, aimAngle + halfArc);
                Quaternion bulletRotation = Quaternion.Euler(0, 0, randomAngle-90);
                GameObject bulletObj = Instantiate(projectilePrefab, player.transform.position, bulletRotation);
                Projectile p = bulletObj.GetComponent<Projectile>();
                p.rangeX = player.RangeX;
                p.rangeY = player.RangeY;
            }

            yield return new WaitUntil(() => FindObjectsByType<Projectile>(FindObjectsSortMode.None).Length == 0);
            if (enemyBoss != null) enemyBoss.EndDamageBatch();
        }

        isExecutingAction = false;
        turnMan.StartEnemyPhase();
    }

    void ExecuteMove(BoardNode targetNode, TurnManager turnMan)
    {
        turnMan.SaveState();
        PlayerGeneral player = turnMan.activePlayer;
        bool isDiagonalMove = Mathf.Abs(targetNode.x - player.X) == 1 && Mathf.Abs(targetNode.y - player.Y) == 1;

        GridManager.Instance.grid[player.X, player.Y].currentPiece = null;
        player.MoveTo(targetNode);
        
        int ammoToRecover = (RunManager.Instance != null && RunManager.Instance.RedHareEnabled && isDiagonalMove) ? 2 : 1;
        player.LoadedAmmo = Mathf.Min(player.LoadedAmmo + ammoToRecover, player.MaxAmmo);
        
        turnMan.StartEnemyPhase();
    }
}