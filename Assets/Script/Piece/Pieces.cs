using UnityEngine;

public abstract class Piece : MonoBehaviour
{
    // --- COORDINATES ---
    // We use properties { get; protected set; } so other scripts can READ them,
    // but only this script (or children like EnemyPawn) can CHANGE them.
    [SerializeField] private int _x;
    public int X { get { return _x; } protected set { _x = value; } }

    [SerializeField] private int _y;
    public int Y { get { return _y; } protected set { _y = value; } }

    [SerializeField] private bool _isPlayer;
    public bool IsPlayer => _isPlayer; 

    public BoardNode CurrentNode { get; protected set; }

    // --- STATS ---
    [Header("Stats for enemy only")]
    [SerializeField] private int _maxHp = 1;
    [SerializeField] private int _currentHp;
    public int CurrentHp { get { return _currentHp; } protected set { _currentHp = value; } }

    // --- VISUALS ---
    [Header("Visuals")]
    [SerializeField] private Sprite _deadSprite;
    [SerializeField] private float _jumpHeight = 0.5f;
    [SerializeField] private float _moveDuration = 0.25f;

    // --- ANIMATION STATE ---
    private float _moveTimer;
    private bool _isMoving;
    private Vector3 _startMovePosition;
    protected bool _isDead = false; 
    public bool IsDead => _isDead;

    // --- COOLDOWN ---
    [Header("Cooldown Settings")]
    [SerializeField] private int _maxCooldown;
    public int MaxCooldown { get { return _maxCooldown; } protected set { _maxCooldown = value; } }

    [SerializeField] private int _currentCooldown;
    public int CurrentCooldown 
    { 
        get { return _currentCooldown; } 
        set { _currentCooldown = value; } 
    }

    [HideInInspector] public Vector3 TargetPosition; 

    protected SpriteRenderer _spriteRenderer;
    protected Color _originalColor;

    protected virtual void Awake()
    {
        CurrentHp = _maxHp;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null) _originalColor = _spriteRenderer.color;
    }
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
    public virtual void MoveTo(BoardNode targetNode)
    {
        // 1. Clear old node logic
        if (CurrentNode != null) CurrentNode.currentPiece = null;

        // 2. Kill whatever is on the target node
        if (targetNode.currentPiece != null && targetNode.currentPiece != this)
        {
            Piece targetPiece = targetNode.currentPiece.GetComponent<Piece>();
            if (targetPiece != null) targetPiece.TakeDamage(999);
            else Destroy(targetNode.currentPiece.gameObject);
        }

        // 3. Update Coordinates using Properties
        X = targetNode.x;
        Y = targetNode.y;
        CurrentNode = targetNode;
        targetNode.currentPiece = this;

        // 4. Setup Animation
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

        if (CurrentNode != null) CurrentNode.currentPiece = null;
        if (!_isPlayer) TurnManager.Instance.enemyPieces.Remove(this);

        // --- CLONE LOGIC ---
        GameObject corpseObj = Instantiate(gameObject, transform.position, Quaternion.identity);
        corpseObj.name = gameObject.name + "_Corpse";
        Destroy(corpseObj.GetComponent<Piece>());

        SpriteRenderer corpseSr = corpseObj.GetComponent<SpriteRenderer>();
        if (corpseSr != null)
        {
            if (_deadSprite != null) corpseSr.sprite = _deadSprite;
            corpseSr.color = new Color(0.6f, 0.6f, 0.6f, 1.0f);
        }

        Corpse corpseScript = corpseObj.AddComponent<Corpse>();
        corpseScript.Init(CurrentNode);

        if (CurrentNode != null) CurrentNode.currentCorpse = corpseScript;
        TurnManager.Instance.activeCorpses.Add(corpseScript);

        gameObject.SetActive(false);
    }

    public void SetTargeted(bool isTargeted)
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = isTargeted ? Color.yellow : _originalColor;
        }
    }

    public abstract bool IsValidMove(BoardNode targetNode, BoardNode[,] grid);
    public virtual BoardNode GetAIMove(BoardNode[,] grid) { return null; }

    protected virtual void Update()
    {
        if (_isMoving)
        {
            _moveTimer += Time.deltaTime;
            float t = _moveTimer / _moveDuration;

            if (t >= 1f)
            {
                transform.position = TargetPosition;
                _isMoving = false;
            }
            else
            {
                // Bezier Curve
                Vector3 p0 = _startMovePosition;
                Vector3 p2 = TargetPosition;
                Vector3 midPoint = (p0 + p2) * 0.5f;
                Vector3 p1 = midPoint + new Vector3(0, _jumpHeight, 0);

                Vector3 position = (1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * p1 + t * t * p2;
                transform.position = position;
            }
        }
        else
        {
            if (TargetPosition != Vector3.zero) transform.position = TargetPosition;

            // Jiggle
            if (!_isPlayer && _currentCooldown <= 1)
            {
                float offsetY = Mathf.Sin(Time.time * 10f) * 0.05f;
                transform.position = TargetPosition + new Vector3(0, offsetY, 0);
            }
        }
    }
}