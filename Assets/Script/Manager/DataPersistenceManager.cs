using UnityEngine;
using System.IO; // Required for File reading/writing
using System;

[Serializable]
public class PlayerRunData
{
    public string playerName;
    public string phoneNumber;
    public int floorsCleared;
    public int totalTurns;
    public float totalTimeSeconds;
    public string runResult; // "Won" or "Died"
    public string datePlayed;
}

public class DataPersistenceManager : MonoBehaviour
{
    public static DataPersistenceManager Instance { get; private set; }

    private string _saveFilePath;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        // Saves to the persistent data path (AppData/LocalLow on Windows, valid on Mobile too)
        _saveFilePath = Application.persistentDataPath + "/PlayerRunHistory.json";
    }

    public void SaveRunData(string result)
    {
        // 1. Gather all the data
        PlayerRunData newData = new PlayerRunData
        {
            playerName = PlayerPrefs.GetString("PlayerName", "Unknown"),
            phoneNumber = PlayerPrefs.GetString("PlayerPhone", "Unknown"),
            floorsCleared = LevelManager.Instance.CurrentLevelIndex,
            totalTurns = TurnManager.Instance.CurrentTurnNumber,
            totalTimeSeconds = RunManager.Instance.TotalRunTime,
            runResult = result,
            datePlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // 2. Convert to JSON
        string json = JsonUtility.ToJson(newData, true);

        // 3. Append to the file (so we keep a history of ALL runs)
        // If you only want to save the most recent run, use File.WriteAllText instead.
        File.AppendAllText(_saveFilePath, json + "\n,\n");

        Debug.Log($"Run Saved to: {_saveFilePath}");
    }
}