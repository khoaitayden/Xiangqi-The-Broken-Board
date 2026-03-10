using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Player UI")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI armorText;
    public TextMeshProUGUI weaponStatsText; // NEW: Drag your new Text object here

    [Header("Enemy Hover UI")]
    public GameObject enemyPanel; 
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
            
            // NEW: Show Firepower and Arc
            weaponStatsText.text = $"Firepower: {player.Firepower} Pellets\nSpread Arc: {player.FireArc}°";
        }
        else
        {
            ammoText.text = "Ammo: 0";
            armorText.text = "Armor: 0";
            if (weaponStatsText != null) weaponStatsText.text = "";
        }
    }

    private void UpdateEnemyHoverInfo()
    {
        Vector2 mouseWorldPos = InputHandler.Instance.MouseWorldPosition;
        BoardNode hoveredNode = GridManager.Instance.GetNodeAtPosition(mouseWorldPos);

        if (hoveredNode == null)
        {
            enemyPanel.SetActive(false);
            return;
        }

        if (hoveredNode.currentPiece != null && !hoveredNode.currentPiece.IsPlayer)
        {
            Piece enemy = hoveredNode.currentPiece;
            
            string cleanName = enemy.gameObject.name.Replace("Enemy", "").Replace("(Clone)", "");

            enemyNameText.text = cleanName.ToUpper();
            enemyHPText.text = $"HP: {enemy.CurrentHp} / {enemy.MaxHp}";
            enemyCooldownText.text = $"Cooldown: {enemy.CurrentCooldown}";
            
            enemyPanel.SetActive(true);
        }
        else if (hoveredNode.currentCorpse != null)
        {
            Corpse corpse = hoveredNode.currentCorpse;
            
            enemyNameText.text = "CORPSE";
            enemyHPText.text = $"Fades in: {corpse.turnsRemaining} turns";
            enemyCooldownText.text = ""; 
            
            enemyPanel.SetActive(true);
        }
        else
        {
            enemyPanel.SetActive(false);
        }
    }
}