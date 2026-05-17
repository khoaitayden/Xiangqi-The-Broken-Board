using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems; 

public class PlayerActionController : MonoBehaviour
{
    public static PlayerActionController Instance { get; private set; }

    [Header("Aiming & Shooting")]
    public GameObject projectilePrefab; 
    public AimVisualizer aimVisualizer;
    public float dragThreshold = 0.5f; 
    
    public float cancelAimThreshold = 1.0f; 
    
    private Vector2 currentAimDirection;
    private bool isExecutingAction = false; 

    // --- INPUT STATE TRACKING ---
    private Vector2 _pointerDownPos;
    private bool _isDraggingToAim = false;
    private bool _isPlayerSelectedForMove = false;
    private bool _startedClickOnUI = false; 
    
    // THE FIX: Track if the current drag is outside the cancel zone
    private bool _isValidAim = false; 

    public BoardNode SelectedEnemyNode { get; private set; } 

    private enum SpecialShotMode { None, CrouchingTiger, FlyingGeneral }
    private SpecialShotMode currentShotMode = SpecialShotMode.None;

    private void Awake() { Instance = this; }

    void Update()
    {
        TurnManager turnMan = TurnManager.Instance;
        GridManager gridMan = GridManager.Instance;

        // 1. EARLY EXIT & RESET
        if (turnMan.CurrentTurn != TurnManager.TurnState.PlayerTurn || isExecutingAction || turnMan.activePlayer == null || Time.timeScale == 0f)
        {
            _isDraggingToAim = false;
            _isPlayerSelectedForMove = false;
            _startedClickOnUI = false;
            _isValidAim = false; // Reset aim validity
            
            ClearVisuals(gridMan);
            return;
        }

        Vector2 pointerPos = InputHandler.Instance.PointerWorldPosition;
        BoardNode hoveredNode = gridMan.GetNodeAtPosition(pointerPos);

        // 2. DETECT INITIAL TOUCH
        if (InputHandler.Instance.IsPointerDownThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                _startedClickOnUI = true; 
            }
            else
            {
                _startedClickOnUI = false;
                _pointerDownPos = pointerPos;
            }
        }

        if (_startedClickOnUI)
        {
            if (InputHandler.Instance.IsExecuteTriggered) _startedClickOnUI = false;
            return; 
        }

        // 3. DETECT DRAGGING
        if (InputHandler.Instance.IsPointerDown)
        {
            if (!_isDraggingToAim && Vector2.Distance(_pointerDownPos, pointerPos) > dragThreshold)
            {
                _isDraggingToAim = true;
                _isPlayerSelectedForMove = false; 
            }
        }

        // 4. UPDATE VISUALS & CHECK CANCEL ZONE
        ClearVisuals(gridMan);
        if (_isDraggingToAim)
        {
            // THE FIX: Check how close the pointer is to the player
            float distanceToPlayer = Vector2.Distance(pointerPos, turnMan.activePlayer.transform.position);

            if (distanceToPlayer < cancelAimThreshold)
            {
                // We dragged back to the player! Hide visuals and mark aim as invalid.
                _isValidAim = false; 
            }
            else
            {
                // We are aiming normally.
                _isValidAim = true;
                UpdateAimPreview(pointerPos, turnMan, gridMan);
            }
        }
        else if (_isPlayerSelectedForMove)
        {
            gridMan.UpdatePlayerMoveHighlight(turnMan.activePlayer);
        }
        
        if (SelectedEnemyNode != null) gridMan.UpdateHoverHighlight(SelectedEnemyNode);

        // 5. DETECT RELEASE (EXECUTE)
        if (InputHandler.Instance.IsExecuteTriggered)
        {
            if (_isDraggingToAim)
            {
                // THE FIX: Only shoot if we didn't drag back to cancel!
                if (_isValidAim) 
                {
                    TryExecuteShoot(turnMan);
                }

                // Reset drag states regardless of whether we shot or cancelled
                _isDraggingToAim = false;
                _isValidAim = false;
            }
            else
            {
                HandleQuickClick(hoveredNode, turnMan, gridMan);
            }
        }
    }

    private void HandleQuickClick(BoardNode clickedNode, TurnManager turnMan, GridManager gridMan)
    {
        if (clickedNode == null) 
        {
            _isPlayerSelectedForMove = false;
            SelectedEnemyNode = null;
            return;
        }

        if (_isPlayerSelectedForMove && turnMan.activePlayer.IsValidMove(clickedNode, gridMan.grid))
        {
            ExecuteMove(clickedNode, turnMan);
            _isPlayerSelectedForMove = false;
            SelectedEnemyNode = null;
            return;
        }

        if (clickedNode.currentPiece != null)
        {
            if (clickedNode.currentPiece.IsPlayer)
            {
                _isPlayerSelectedForMove = true;
                SelectedEnemyNode = null; 
            }
            else
            {
                _isPlayerSelectedForMove = false;
                SelectedEnemyNode = clickedNode; 
            }
        }
        else if (clickedNode.currentCorpse != null)
        {
            _isPlayerSelectedForMove = false;
            SelectedEnemyNode = clickedNode; 
        }
        else
        {
            _isPlayerSelectedForMove = false;
            SelectedEnemyNode = null;
        }
    }

    private void UpdateAimPreview(Vector2 pointerPos, TurnManager turnMan, GridManager gridMan)
    {
        PlayerGeneral player = turnMan.activePlayer;
        currentShotMode = SpecialShotMode.None;

        currentAimDirection = (pointerPos - (Vector2)player.transform.position).normalized;
        if (currentAimDirection == Vector2.zero) currentAimDirection = Vector2.up; 
        
        if (player.WeaponPivot != null)
        {
            float angle = Mathf.Atan2(currentAimDirection.y, currentAimDirection.x) * Mathf.Rad2Deg;
            player.WeaponPivot.rotation = Quaternion.Euler(0, 0, angle);
        }

        DetermineSpecialShots(player, gridMan);
        DrawAimConeAndHighlightEnemies(turnMan, pointerPos);
    }

    private void DetermineSpecialShots(PlayerGeneral player, GridManager gridMan)
    {
        EnemyGeneral enemyBoss = Object.FindAnyObjectByType<EnemyGeneral>();
        if (enemyBoss != null && player.X == enemyBoss.X) 
        {
            int minY = Mathf.Min(player.Y, enemyBoss.Y);
            int maxY = Mathf.Max(player.Y, enemyBoss.Y);
            int blockers = 0;
            for (int y = minY + 1; y < maxY; y++) if (!gridMan.grid[player.X, y].IsEmpty()) blockers++;
            int allowedBlockers = RunManager.Instance.MandateOfHeavenEnabled ? 1 : 0;
            
            if (blockers <= allowedBlockers)
            {
                Vector2 directionToBoss = (enemyBoss.transform.position - player.transform.position).normalized;
                if (Vector2.Angle(currentAimDirection, directionToBoss) < 30f) currentShotMode = SpecialShotMode.FlyingGeneral;
            }
        }

        if (RunManager.Instance.CrouchingTigerEnabled && currentShotMode == SpecialShotMode.None)
        {
            RaycastHit2D hit = Physics2D.Raycast(player.transform.position, currentAimDirection, 1.5f);
            if (hit.collider != null)
            {
                Piece hitPiece = hit.collider.GetComponent<Piece>();
                if (hitPiece != null && !hitPiece.IsPlayer) currentShotMode = SpecialShotMode.CrouchingTiger;
            }
        }
    }

    private void TryExecuteShoot(TurnManager turnMan)
    {
        PlayerGeneral player = turnMan.activePlayer;
        bool hasAmmo = player.LoadedAmmo > 0;
        bool artOfWarReady = RunManager.Instance.ArtOfWarEnabled && !RunManager.Instance.ArtOfWarUsedThisFloor;

        if (hasAmmo || artOfWarReady)
        {
            if (!hasAmmo && artOfWarReady)
            {
                RunManager.Instance.ArtOfWarUsedThisFloor = true;
                Debug.Log("Art of War used! Fired with 0 Ammo!");
            }
            else
            {
                player.LoadedAmmo--; 
            }
            StartCoroutine(ExecuteShootCoroutine(turnMan));
        }
    }

    private void ClearVisuals(GridManager gridMan)
    {
        if (aimVisualizer != null) aimVisualizer.Hide();
        gridMan.ClearAllHighlights(); 
    }

    void DrawAimConeAndHighlightEnemies(TurnManager turnMan, Vector2 mouseWorldPos)
    {
        PlayerGeneral player = turnMan.activePlayer;
        Vector3 playerPos = player.transform.position;
        
        foreach (Piece enemy in turnMan.enemyPieces) { if (enemy != null) enemy.SetTargeted(false); }

        if (currentShotMode == SpecialShotMode.FlyingGeneral)
        {
            EnemyGeneral enemyBoss = Object.FindAnyObjectByType<EnemyGeneral>();
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
            EnemyGeneral enemyBoss = Object.FindAnyObjectByType<EnemyGeneral>();
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
            EnemyGeneral enemyBoss = Object.FindAnyObjectByType<EnemyGeneral>();
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