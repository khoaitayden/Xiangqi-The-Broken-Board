using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI playerStatsText;
    public TextMeshProUGUI enemyInfoText;

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
            // E.g., "AMMO: 2/2   ARMOR: 1"
            playerStatsText.text = $"AMMO: {player.LoadedAmmo}/{player.MaxAmmo}\nARMOR: {player.CurrentArmor}";
        }
        else
        {
            playerStatsText.text = "PLAYER DEAD";
        }
    }

    private void UpdateEnemyHoverInfo()
    {
        // 1. Get the node the mouse is currently hovering over
        Vector2 mouseWorldPos = InputHandler.Instance.MouseWorldPosition;
        BoardNode hoveredNode = GridManager.Instance.GetNodeAtPosition(mouseWorldPos);

        // 2. Check if there is an Enemy on that node
        if (hoveredNode != null && !hoveredNode.IsEmpty() && !hoveredNode.currentPiece.IsPlayer)
        {
            Piece enemy = hoveredNode.currentPiece;
            
            // Format the name nicely (e.g., "EnemyHorse(Clone)" -> "Horse")
            string cleanName = enemy.gameObject.name.Replace("Enemy", "").Replace("(Clone)", "");

            // Show HP and Cooldown
            string cooldownText = enemy.CurrentCooldown == 0 ? "<color=red>READY TO STRIKE</color>" : $"Attacks in: {enemy.CurrentCooldown} turns";
            
            enemyInfoText.text = $"<b>{cleanName.ToUpper()}</b>\nHP: {enemy.CurrentHp}/{enemy.MaxHp}\n{cooldownText}";
            
            // Optional: Show background panel if you add one later
            enemyInfoText.gameObject.SetActive(true);
        }
        else if (hoveredNode != null && hoveredNode.currentCorpse != null)
        {
            // Bonus: Show info for Corpses!
            Corpse corpse = hoveredNode.currentCorpse;
            enemyInfoText.text = $"<b>CORPSE</b>\nFades in: {corpse.turnsRemaining} turns";
            enemyInfoText.gameObject.SetActive(true);
        }
        else
        {
            // Hide the text if we aren't hovering anything interesting
            enemyInfoText.text = "";
            enemyInfoText.gameObject.SetActive(false);
        }
    }
}