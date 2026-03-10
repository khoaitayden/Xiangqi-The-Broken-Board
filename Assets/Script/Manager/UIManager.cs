using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Player UI")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI armorText;

    [Header("Enemy Hover UI")]
    public GameObject enemyPanel; // Parent object to hide/show everything at once
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI enemyHPText;
    public TextMeshProUGUI enemyCooldownText;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    void Update()
    {
        UpdatePlayerStats();
        UpdateEnemyHoverInfo();
    }

    private void UpdatePlayerStats()
    {
        PlayerGeneral player = TurnManager.Instance.activePlayer;

        if (player != null)
        {
            ammoText.text = $"Ammo: {player.LoadedAmmo} / {player.MaxAmmo}";
            armorText.text = $"Armor: {player.CurrentArmor}";
        }
        else
        {
            ammoText.text = "Ammo: 0";
            armorText.text = "Armor: 0";
        }
    }

    private void UpdateEnemyHoverInfo()
    {
        // 1. Get the hovered node
        Vector2 mouseWorldPos = InputHandler.Instance.MouseWorldPosition;
        BoardNode hoveredNode = GridManager.Instance.GetNodeAtPosition(mouseWorldPos);

        // 2. SAFETY CHECK: Mouse off the board
        if (hoveredNode == null)
        {
            enemyPanel.SetActive(false);
            return;
        }

        if (hoveredNode.currentPiece != null && !hoveredNode.currentPiece.IsPlayer)
        {
            Piece enemy = hoveredNode.currentPiece;
            
            // Format name nicely
            string cleanName = enemy.gameObject.name.Replace("Enemy", "").Replace("(Clone)", "");

            enemyNameText.text = cleanName.ToUpper();
            enemyHPText.text = $"HP: {enemy.CurrentHp} / {enemy.MaxHp}";
            enemyCooldownText.text = $"Cooldown: {enemy.CurrentCooldown}";
            
            enemyPanel.SetActive(true);
        }
        // 4. CHECK FOR CORPSE
        else if (hoveredNode.currentCorpse != null)
        {
            Corpse corpse = hoveredNode.currentCorpse;
            
            enemyNameText.text = "CORPSE";
            enemyHPText.text = $"Fades in: {corpse.turnsRemaining} turns";
            enemyCooldownText.text = ""; 
            
            enemyPanel.SetActive(true);
        }
        // 5. EMPTY TILE OR PLAYER TILE
        else
        {
            enemyPanel.SetActive(false);
        }
    }
}