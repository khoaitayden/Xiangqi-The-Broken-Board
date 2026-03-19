using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    // --- Coordinates ---
    [SerializeField] private int _x;
    [SerializeField] private int _y;
    [SerializeField] private bool _isPlayer;

    public int X { get { return _x; } protected set { _x = value; } }
    public int Y { get { return _y; } protected set { _y = value; } }
    public bool IsPlayer => _isPlayer;
    public BoardNode CurrentNode { get; protected set; }

    // --- Stats ---
    [Header("Stats")]
    [SerializeField] protected PieceStatsSO _stats;
    [SerializeField] private int _currentHp;

    public int MaxHp => _stats != null ? _stats.runtimeMaxHp : 1;
    public int MaxCooldown => _stats != null ? _stats.runtimeMaxCooldown : 3;
    public int CurrentHp { get { return _currentHp; } protected set { _currentHp = value; } }

    // --- Cooldown ---
    [Header("Cooldown")]
    [SerializeField] private int _currentCooldown;

    public int CurrentCooldown
    {
        get { return _currentCooldown; }
        set 
        { 
            _currentCooldown = value; 
            EvaluateJiggleState();
        }
    }

    // --- Visuals ---
    [Header("Visuals")]
    [SerializeField] protected Sprite _deadSprite;
    [SerializeField] private float _jumpHeight = 0.5f;
    [SerializeField] private float _moveDuration = 0.25f;

    [Header("Targeting Outline")]
    [SerializeField] private GameObject _outlinePrefab;
    [SerializeField] private Color _outlineColor = Color.red;      
    [SerializeField] private Color _threatColor = Color.yellow;   
    [SerializeField] private float _pulseSpeed = 6f;
    [SerializeField] private float _minPulseScale = 1.1f;
    [SerializeField] private float _maxPulseScale = 1.2f;
    [HideInInspector] public Vector3 TargetPosition;
    
    private SpriteRenderer _outlineRenderer;
    protected SpriteRenderer _spriteRenderer;
    protected Color _originalColor;

    // --- State & Coroutines ---
    protected bool _isDead = false;
    public bool IsDead => _isDead;
    private bool _isMoving;
    
    // THE FIX: Two separate state variables to track targeting and threats independently.
    private bool _isTargetedByPlayer = false;
    private bool _isThreateningPlayer = false;

    private Coroutine _moveCoroutine;
    private Coroutine _jiggleCoroutine;
    private Coroutine _pulseCoroutine;

    // LIFECYCLE

    protected virtual void Awake()
    {
        CurrentHp = MaxHp;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;

        CreateOutline();
    }

    // INITIALIZATION

    public void InitPosition(int x, int y, BoardNode node)
    {
        _x = x;
        _y = y;
        CurrentNode = node;
        node.currentPiece = this;

        TargetPosition = node.nodeGameObject.transform.position;
        transform.position = TargetPosition;

        EvaluateJiggleState();
    }

    public void ForceSetStats(int hp, int cooldown)
    {
        CurrentHp = hp;
        CurrentCooldown = cooldown;
        _isDead = false;
        gameObject.SetActive(true);
    }

    // ACTIONS

    public virtual Coroutine MoveTo(BoardNode targetNode)
    {
        if (CurrentNode != null) CurrentNode.currentPiece = null;

        if (targetNode.currentPiece != null && targetNode.currentPiece != this)
        {
            Piece targetPiece = targetNode.currentPiece.GetComponent<Piece>();
            if (targetPiece != null) targetPiece.TakeDamage(999);
            else Destroy(targetNode.currentPiece.gameObject);
        }

        X = targetNode.x;
        Y = targetNode.y;
        CurrentNode = targetNode;
        targetNode.currentPiece = this;

        TargetPosition = targetNode.nodeGameObject.transform.position;
        
        if (_moveCoroutine != null) StopCoroutine(_moveCoroutine);
        
        _moveCoroutine = StartCoroutine(MoveRoutine(TargetPosition)); 
        return _moveCoroutine;
    }


    public virtual void TakeDamage(int damage)
    {
        if (_isDead) return;
        CurrentHp -= damage;
        if (CurrentHp <= 0) Die();
    }

    protected virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;

        StopAllCoroutines();

        if (CurrentNode != null) CurrentNode.currentPiece = null;
        if (!_isPlayer) TurnManager.Instance.enemyPieces.Remove(this);

        SpawnCorpse();
        gameObject.SetActive(false);
    }

    // --- COROUTINE ANIMATIONS ---

    private IEnumerator MoveRoutine(Vector3 targetPos)
    {
        _isMoving = true; 
        if (_jiggleCoroutine != null) StopCoroutine(_jiggleCoroutine);

        float timer = 0f;
        Vector3 startPos = transform.position;
        Vector3 p1 = (startPos + targetPos) * 0.5f + new Vector3(0, _jumpHeight, 0);

        while (timer < _moveDuration)
        {
            timer += Time.deltaTime;
            float t = timer / _moveDuration;
            transform.position = (1 - t) * (1 - t) * startPos + 2 * (1 - t) * t * p1 + t * t * targetPos;
            yield return null;
        }

        transform.position = targetPos;
        _isMoving = false;
        
        EvaluateJiggleState(); 
    }

    private void EvaluateJiggleState()
    {
        if (_jiggleCoroutine != null) StopCoroutine(_jiggleCoroutine);

        if (!_isPlayer && _currentCooldown <= 1 && gameObject.activeInHierarchy && !_isMoving)
        {
            _jiggleCoroutine = StartCoroutine(JiggleRoutine());
        }
        else
        {
            if (!_isMoving && TargetPosition != Vector3.zero) 
            {
                transform.position = TargetPosition;
            }
        }
    }

    private IEnumerator JiggleRoutine()
    {
        while (true)
        {
            float offsetY = Mathf.Sin(Time.time * 8f) * 0.05f;
            transform.position = TargetPosition + new Vector3(0, offsetY, 0);
            yield return null;
        }
    }

    // --- TARGETING OUTLINE ---

    private void CreateOutline()
    {
        _outlineRenderer = _outlinePrefab.GetComponent<SpriteRenderer>();
        _outlineRenderer.color = _outlineColor;
        _outlinePrefab.SetActive(false);
    }

    // THE FIX: SetTargeted now just updates the state and calls our new master function.
    public void SetTargeted(bool isTargeted)
    {
        if (_isTargetedByPlayer == isTargeted) return;
        _isTargetedByPlayer = isTargeted;
        UpdateOutlineVisuals();
    }

    // THE FIX: SetThreat also just updates its state and calls the master function.
    public void SetThreat(bool isThreat)
    {
        _isThreateningPlayer = isThreat;
        UpdateOutlineVisuals();
    }

    // THE FIX: A single function to rule them all! This looks at both states to decide visuals.
    private void UpdateOutlineVisuals()
    {
        bool shouldBeActive = _isTargetedByPlayer || _isThreateningPlayer;
        _outlinePrefab.SetActive(shouldBeActive);

        if (shouldBeActive)
        {
            // --- Determine Color ---
            // The player's aim always takes priority for color.
            if (_isTargetedByPlayer)
            {
                _outlineRenderer.color = _outlineColor; // Red for aimed at
            }
            else // Must be a threat, but not aimed at
            {
                _outlineRenderer.color = _threatColor; // Yellow for threat
            }

            // Stop any existing pulse to restart it with the correct parameters
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);

            // --- Determine Pulse Style ---
            // Threatening pieces always get the more aggressive pulse.
            if (_isThreateningPlayer)
            {
                _pulseCoroutine = StartCoroutine(PulseRoutine(9f, 1.15f, 1.3f)); // Aggressive
            }
            else // Must just be targeted, not a threat
            {
                _pulseCoroutine = StartCoroutine(PulseRoutine()); // Normal
            }
        }
        else // Neither targeted nor a threat
        {
            if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
            _pulseCoroutine = null;
        }
    }

    private IEnumerator PulseRoutine()
    {
        yield return PulseRoutine(_pulseSpeed, _minPulseScale, _maxPulseScale);
    }

    private IEnumerator PulseRoutine(float speed, float minScale, float maxScale)
    {
        while (true)
        {
            float sineWave = Mathf.Sin(Time.time * speed); 
            float scaleRange = maxScale - minScale;
            float midPoint = (minScale + maxScale) / 2f;
            float targetScale = midPoint + (sineWave * (scaleRange / 2f));

            _outlinePrefab.transform.localScale = new Vector3(targetScale, targetScale, 1f);
            yield return null;
        }
    }

    // ABSTRACT / VIRTUAL (AI & MOVEMENT VALIDATION)
    // ... (rest of your script is perfect and doesn't need to be changed) ...
    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);
    
    public virtual List<BoardNode> GetValidMoves(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = new List<BoardNode>();
        foreach (BoardNode node in grid)
        {
            if (IsValidMove(node, grid)) validMoves.Add(node);
        }
        return validMoves;
    }

    public virtual BoardNode GetAIMove(BoardNode[,] grid)
    {
        List<BoardNode> validMoves = GetValidMoves(grid);
        return EvaluateAndPickBestMove(validMoves, grid);
    }

    // PRIVATE HELPERS

    private void SpawnCorpse()
    {
        GameObject corpseObj = Instantiate(gameObject, transform.position, Quaternion.identity);
        corpseObj.name = gameObject.name + "_Corpse";
        
        // Cleanup old components
        Destroy(corpseObj.GetComponent<Piece>());
        foreach (Transform child in corpseObj.transform) Destroy(child.gameObject); // Destroy the outline clone

        SpriteRenderer corpseSr = corpseObj.GetComponent<SpriteRenderer>();
        if (corpseSr != null)
        {
            if (_deadSprite != null) corpseSr.sprite = _deadSprite;
            corpseSr.color = new Color(0.6f, 0.6f, 0.6f, 1f);
        }

        Corpse corpseScript = corpseObj.AddComponent<Corpse>();
        corpseScript.Init(CurrentNode);

        if (CurrentNode != null) CurrentNode.currentCorpse = corpseScript;
        TurnManager.Instance.activeCorpses.Add(corpseScript);
    }
    protected BoardNode EvaluateAndPickBestMove(List<BoardNode> validMoves, BoardNode[,] grid)
    {
        const int SURVIVAL_PENALTY = -5000;
        const int SURVIVAL_BONUS = 500;    
        const int BODYGUARD_BONUS = 1000;  
        const int CHECK_BONUS = 100;       
        const int AREA_DENIAL_MULTIPLIER = 15;
        const int DISTANCE_PENALTY = -2;   
        const int DISTANCE_BONUS = 5;      
        if (validMoves.Count == 0) return null;

        PlayerGeneral player = TurnManager.Instance.activePlayer;
        if (player == null) return validMoves[Random.Range(0, validMoves.Count)];

        BoardNode playerNode = grid[player.X, player.Y];
        
        BoardNode bestNode = null;
        int bestScore = int.MinValue;

        int allowedBlockers = (RunManager.Instance != null && RunManager.Instance.MandateOfHeavenEnabled) ? 1 : 0;

        foreach (BoardNode testNode in validMoves)
        {
            if (testNode == playerNode) return testNode; 

            int score = 0;

            int oldX = X;
            int oldY = Y;
            X = testNode.x;
            Y = testNode.y;
            
            grid[oldX, oldY].currentPiece = null;
            testNode.currentPiece = this;

            if (this is EnemyGeneral)
            {
                if (testNode.x == player.X)
                {
                    int minY = Mathf.Min(player.Y, testNode.y);
                    int maxY = Mathf.Max(player.Y, testNode.y);
                    int blockers = 0;

                    for (int y = minY + 1; y < maxY; y++)
                    {
                        if (!grid[player.X, y].IsEmpty()) blockers++;
                    }

                    if (blockers <= allowedBlockers)
                    {
                        score += SURVIVAL_PENALTY; 
                    }
                }
                else
                {
                    score += SURVIVAL_BONUS; 
                }
            }
            else 
            {
                EnemyGeneral boss = Object.FindFirstObjectByType<EnemyGeneral>();
                if (boss != null && boss.X == player.X) 
                {
                    if (testNode.x == player.X && testNode.y > Mathf.Min(player.Y, boss.Y) && testNode.y < Mathf.Max(player.Y, boss.Y))
                    {
                        score += BODYGUARD_BONUS; 
                    }
                }
            }


            if (IsValidMove(playerNode, grid))
            {
                score += CHECK_BONUS; 
            }

            int restrictedEscapeRoutes = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; 
                    
                    int adjX = player.X + dx;
                    int adjY = player.Y + dy;

                    if (adjX >= 0 && adjX < GridManager.Instance.width && adjY >= 0 && adjY < GridManager.Instance.height)
                    {
                        BoardNode adjNode = grid[adjX, adjY];
                        if (adjNode.IsEmpty() && IsValidMove(adjNode, grid))
                        {
                            restrictedEscapeRoutes++;
                        }
                    }
                }
            }
            score += restrictedEscapeRoutes * AREA_DENIAL_MULTIPLIER;

            int distX = Mathf.Abs(testNode.x - player.X);
            int distY = Mathf.Abs(testNode.y - player.Y);
            int distanceToPlayer = distX + distY; 
            
            if (this is EnemyGeneral) score += distanceToPlayer * DISTANCE_BONUS; 
            else score += distanceToPlayer * DISTANCE_PENALTY; 

            score += Random.Range(0, 5);

            grid[oldX, oldY].currentPiece = this;
            testNode.currentPiece = null;
            X = oldX;
            Y = oldY;

            if (score > bestScore)
            {
                bestScore = score;
                bestNode = testNode;
            }
        }

        return bestNode;
    }

    public void StartFadeOut(float duration = 0.5f)
    {
        StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float timer = 0f;
        Color startColor = _spriteRenderer.color;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, timer / duration);
            _spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
            yield return null;
        }
    }
}