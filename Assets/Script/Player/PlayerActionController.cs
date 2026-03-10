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
            gridMan.ClearAllHighlights(); // Explicitly clear highlights when not player's turn
            return;
        }


        Vector2 worldPosition = InputHandler.Instance.MouseWorldPosition;
        BoardNode hoveredNode = gridMan.GetNodeAtPosition(worldPosition);

        if (hoveredNode == null)
        {
            // Mouse is completely off the board. Hide everything!
            if (aimVisualizer != null) aimVisualizer.Hide();
            gridMan.ClearAllHighlights();
            return; 
        }
        gridMan.UpdateHoverHighlight(hoveredNode);

        // --- NORMAL AIMING LOGIC ---
        DetermineInputContext(worldPosition, hoveredNode, turnMan.activePlayer, gridMan);


        if (isAimingMode)
        {
            DrawAimConeAndHighlightEnemies(turnMan, worldPosition); // Pass the mouse position!
        }
        else
        {
            // SAFETY: Hide cone when hovering a move tile
            if (aimVisualizer != null) aimVisualizer.Hide();
        }

        if (InputHandler.Instance.IsClickTriggered) 
        {
            bool hasAmmo = turnMan.activePlayer.LoadedAmmo > 0;
            bool artOfWarReady = RunManager.Instance.ArtOfWarEnabled && !RunManager.Instance.ArtOfWarUsedThisFloor;
            bool canShoot = hasAmmo || artOfWarReady;

            if (!isAimingMode && hoveredNode != null)
            {
                ExecuteMove(hoveredNode, turnMan);
            }
            else if (isAimingMode && canShoot)
            {
                // ART OF WAR EXPENDITURE
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

        // IF IT'S A VALID MOVE (Empty, 1 block away)
        if (hoveredNode != null && player.IsValidMove(hoveredNode, gridMan.grid))
        {
            isAimingMode = false;
            foreach (Piece enemy in TurnManager.Instance.enemyPieces) { if(enemy != null) enemy.SetTargeted(false); }
        }
        else
        {
            isAimingMode = true;
            currentAimDirection = (mouseWorldPos - (Vector2)player.transform.position).normalized;
            if (currentAimDirection == Vector2.zero) currentAimDirection = Vector2.up; 

            // 1. CHECK FOR CROUCHING TIGER (Hovering an adjacent piece)
            if (hoveredNode != null && RunManager.Instance.CrouchingTigerEnabled)
            {
                int distX = Mathf.Abs(hoveredNode.x - player.X);
                int distY = Mathf.Abs(hoveredNode.y - player.Y);
                if (distX <= 1 && distY <= 1 && !hoveredNode.IsEmpty())
                {
                    currentShotMode = SpecialShotMode.CrouchingTiger;
                }
            }

            // 2. CHECK FOR FLYING GENERAL (Same X as Enemy General)
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null && hoveredNode != null && hoveredNode.x == player.X && enemyBoss.X == player.X)
            {
                // Count pieces between Player and Boss
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
                    currentShotMode = SpecialShotMode.FlyingGeneral;
                }
            }
        }
    }

    void DrawAimConeAndHighlightEnemies(TurnManager turnMan, Vector2 mouseWorldPos)
    {
        PlayerGeneral player = turnMan.activePlayer;
        Vector3 playerPos = player.transform.position;
        
        // Un-highlight all enemies
        foreach (Piece enemy in turnMan.enemyPieces) { if (enemy != null) enemy.SetTargeted(false); }

        if (currentShotMode == SpecialShotMode.FlyingGeneral)
        {
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null && aimVisualizer != null)
            {
                // Calculate distance to boss for accurate laser length
                float distanceToBoss = Vector3.Distance(playerPos, enemyBoss.transform.position);

                // Call the new DrawLine method!
                aimVisualizer.DrawLine(playerPos, currentAimDirection, distanceToBoss, 0.2f);

                enemyBoss.SetTargeted(true);
            }
        }
        else if (currentShotMode == SpecialShotMode.CrouchingTiger)
        {
            if (aimVisualizer != null) aimVisualizer.Hide();
            Debug.DrawRay(playerPos, currentAimDirection * 15f, Color.red); 
            RaycastHit2D[] hits = Physics2D.RaycastAll(playerPos, currentAimDirection, 15f);
            int hitCount = 0;
            foreach (var hit in hits)
            {
                Piece hitPiece = hit.collider.GetComponent<Piece>();
                if (hitPiece != null && !hitPiece.IsPlayer)
                {
                    hitCount++;
                    if (hitCount == 2) { hitPiece.SetTargeted(true); break; } 
                }
            }
        }
        else 
        {

            float distanceToMouse = Vector2.Distance(playerPos, mouseWorldPos);

            float currentRangeX = Mathf.Min(distanceToMouse, player.RangeX);
            

            float currentRangeY = Mathf.Max(currentRangeX, Mathf.Min(distanceToMouse, player.RangeY));

            // --- RENDER CONE ---
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
            // INSTANT KILL THE BOSS
            Debug.Log("FLYING GENERAL EXECUTION!");
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null) enemyBoss.TakeDamage(999);
            yield return new WaitForSeconds(0.5f);
        }
        else if (currentShotMode == SpecialShotMode.CrouchingTiger)
        {
            // RAYCAST BEAM (Hits the 2nd target for 3 Damage)
            Debug.Log("CROUCHING TIGER BEAM!");
            RaycastHit2D[] hits = Physics2D.RaycastAll(player.transform.position, currentAimDirection, 15f);
            int hitCount = 0;
            foreach (var hit in hits)
            {
                Piece hitPiece = hit.collider.GetComponent<Piece>();
                if (hitPiece != null && !hitPiece.IsPlayer)
                {
                    hitCount++;
                    if (hitCount == 2) // Jumps over the first adjacent piece!
                    { 
                        hitPiece.TakeDamage(3); 
                        break; 
                    } 
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
        else
        {
            // Find boss and start batching BEFORE bullets spawn
            EnemyGeneral enemyBoss = Object.FindFirstObjectByType<EnemyGeneral>();
            if (enemyBoss != null) enemyBoss.BeginDamageBatch();

            // STANDARD SHOTGUN PELLET SPREAD
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

            if (enemyBoss != null) enemyBoss.EndDamageBatch();
        }

        // --- MISSING CODE ADDED BACK HERE ---
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