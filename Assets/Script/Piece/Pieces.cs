using System.Collections.Generic;
using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    // FIELDS

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
        set { _currentCooldown = value; }
    }

    // --- Visuals ---
    [Header("Visuals")]
    [SerializeField] private Sprite _deadSprite;
    [SerializeField] private float _jumpHeight = 0.5f;
    [SerializeField] private float _moveDuration = 0.25f;

    [HideInInspector] public Vector3 TargetPosition;

    protected SpriteRenderer _spriteRenderer;
    protected Color _originalColor;

    // --- State ---
    protected bool _isDead = false;
    public bool IsDead => _isDead;

    private float _moveTimer;
    private bool _isMoving;
    private Vector3 _startMovePosition;

    // LIFECYCLE

    protected virtual void Awake()
    {
        CurrentHp = MaxHp;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
    }

    protected virtual void Update()
    {
        if (_isMoving)
            UpdateMovement();
        else
            UpdateIdle();
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
    }

    public void ForceSetStats(int hp, int cooldown)
    {
        CurrentHp = hp;
        CurrentCooldown = cooldown;
        _isDead = false;
        gameObject.SetActive(true);
    }

    // ACTIONS

    public virtual void MoveTo(BoardNode targetNode)
    {
        if (CurrentNode != null)
            CurrentNode.currentPiece = null;

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
        _startMovePosition = transform.position;
        _moveTimer = 0f;
        _isMoving = true;
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

        if (CurrentNode != null)
            CurrentNode.currentPiece = null;

        if (!_isPlayer)
            TurnManager.Instance.enemyPieces.Remove(this);

        SpawnCorpse();
        gameObject.SetActive(false);
    }

    public void SetTargeted(bool isTargeted)
    {
        if (_spriteRenderer != null)
            _spriteRenderer.color = isTargeted ? Color.yellow : _originalColor;
    }

    // ABSTRACT / VIRTUAL (AI & MOVEMENT VALIDATION)

    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);
    public virtual BoardNode GetAIMove(BoardNode[,] grid) { return null; }

    // PRIVATE HELPERS

    private void SpawnCorpse()
    {
        GameObject corpseObj = Instantiate(gameObject, transform.position, Quaternion.identity);
        corpseObj.name = gameObject.name + "_Corpse";
        Destroy(corpseObj.GetComponent<Piece>());

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

    private void UpdateMovement()
    {
        _moveTimer += Time.deltaTime;
        float t = _moveTimer / _moveDuration;

        if (t >= 1f)
        {
            transform.position = TargetPosition;
            _isMoving = false;
            return;
        }

        // Quadratic Bezier arc
        Vector3 p0 = _startMovePosition;
        Vector3 p2 = TargetPosition;
        Vector3 p1 = (p0 + p2) * 0.5f + new Vector3(0, _jumpHeight, 0);
        transform.position = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
    }

    private void UpdateIdle()
    {
        if (TargetPosition != Vector3.zero)
            transform.position = TargetPosition;

        // Jiggle when action is imminent
        if (!_isPlayer && _currentCooldown <= 1)
        {
            float offsetY = Mathf.Sin(Time.time * 10f) * 0.05f;
            transform.position = TargetPosition + new Vector3(0, offsetY, 0);
        }
    }
    protected BoardNode EvaluateAndPickBestMove(List<BoardNode> validMoves, BoardNode[,] grid)
    {
        if (validMoves.Count == 0) return null;

        PlayerGeneral player = TurnManager.Instance.activePlayer;
        if (player == null) return validMoves[Random.Range(0, validMoves.Count)];

        BoardNode playerNode = grid[player.X, player.Y];
        
        BoardNode bestNode = null;
        int bestScore = int.MinValue;

        foreach (BoardNode testNode in validMoves)
        {
            // 0-STEP: INSTANT WIN
            if (testNode == playerNode) return testNode; 

            int score = 0;

            // --- SIMULATE THE FUTURE ---
            int oldX = X;
            int oldY = Y;
            X = testNode.x;
            Y = testNode.y;
            
            grid[oldX, oldY].currentPiece = null;
            testNode.currentPiece = this;

            // 1-STEP LOOKAHEAD: "CHECK"
            if (IsValidMove(playerNode, grid))
            {
                score += 100; 
            }

            // 2-STEP LOOKAHEAD: "CHECKMATE / AREA DENIAL"
            int restrictedEscapeRoutes = 0;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue; // Skip player's current tile
                    
                    int adjX = player.X + dx;
                    int adjY = player.Y + dy;

                    if (adjX >= 0 && adjX < GridManager.Instance.width && adjY >= 0 && adjY < GridManager.Instance.height)
                    {
                        BoardNode adjNode = grid[adjX, adjY];
                        // If the escape route is empty AND I can attack it from my new simulated position
                        if (adjNode.IsEmpty() && IsValidMove(adjNode, grid))
                        {
                            restrictedEscapeRoutes++;
                        }
                    }
                }
            }
            score += restrictedEscapeRoutes * 15;

            grid[oldX, oldY].currentPiece = this;
            testNode.currentPiece = null;
            X = oldX;
            Y = oldY;

            // 3. DISTANCE HEURISTIC
            int distX = Mathf.Abs(testNode.x - player.X);
            int distY = Mathf.Abs(testNode.y - player.Y);
            int distanceToPlayer = distX + distY; 
            score -= distanceToPlayer * 2; 

            score += Random.Range(0, 3);

            // SAVE THE BEST SCORE
            if (score > bestScore)
            {
                bestScore = score;
                bestNode = testNode;
            }
        }

        return bestNode;
    }
}