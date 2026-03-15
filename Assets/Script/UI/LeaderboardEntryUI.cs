using UnityEngine;
using TMPro;

public class LeaderboardEntryUI : MonoBehaviour
{
    public TextMeshProUGUI rankText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI floorText;
    public TextMeshProUGUI turnsText;
    public TextMeshProUGUI timeText;

    public void Setup(int rank, PlayerRunData data)
    {
        rankText.text = $"#{rank}";
        nameText.text = data.playerName;
        floorText.text = data.floorsCleared.ToString();
        turnsText.text = data.totalTurns.ToString();
        
        // Format time properly to MM:SS
        int mins = Mathf.FloorToInt(data.totalTimeSeconds / 60f);
        int secs = Mathf.FloorToInt(data.totalTimeSeconds % 60f);
        timeText.text = $"{mins:00}:{secs:00}";
    }
}