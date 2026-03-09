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
}