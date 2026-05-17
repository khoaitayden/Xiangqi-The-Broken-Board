using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

// The individual player data
[Serializable]
public class PlayerRunData
{
    public string playerName;
    public int floorsCleared;
    public int totalTurns;
    public float totalTimeSeconds;
    public string runResult; 
    public string datePlayed;
}

// A wrapper class so Unity can serialize a List to JSON
[Serializable]
public class RunDatabase
{
    public List<PlayerRunData> records = new List<PlayerRunData>();
}

public class DataPersistenceManager : MonoBehaviour
{
    public static DataPersistenceManager Instance { get; private set; }

    private string _saveFilePath;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        _saveFilePath = Application.persistentDataPath + "/PlayerRunHistory.json";
    }

    // Helper method to load the current JSON file
    public RunDatabase LoadDatabase()
    {
        if (File.Exists(_saveFilePath))
        {
            try
            {
                string json = File.ReadAllText(_saveFilePath);
                RunDatabase db = JsonUtility.FromJson<RunDatabase>(json);
                if (db != null) return db;
            }
            catch (Exception e)
            {
                Debug.LogError($"Save file is corrupted! Creating a new database. Error: {e.Message}");
                return new RunDatabase();
            }
        }
        return new RunDatabase();
    }

    public bool DoesPlayerExist(string name)
    {
        RunDatabase db = LoadDatabase();
        return db.records.Exists(r => r.playerName == name);
    }

    public void SaveRunData(string result)
    {
        // 1. Load existing records
        RunDatabase db = LoadDatabase();

        string currentName = PlayerPrefs.GetString("PlayerName", "Unknown");

        // 2. Look for this exact player name in the database
        PlayerRunData existingRecord = db.records.Find(r => r.playerName == currentName);

        if (existingRecord != null)
        {
            if(IsNewRunBetter(currentName, LevelManager.Instance.CurrentLevelIndex, TurnManager.Instance.CurrentTurnNumber, RunManager.Instance.TotalRunTime))
            {
                existingRecord.floorsCleared = LevelManager.Instance.CurrentLevelIndex;
                existingRecord.totalTurns = TurnManager.Instance.CurrentTurnNumber;
                existingRecord.totalTimeSeconds = RunManager.Instance.TotalRunTime;
                existingRecord.runResult = result;
                existingRecord.datePlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Debug.Log($"Overwrote previous run for {currentName}.");
            } 
        }
        else
        {
            // 3. Create a brand new record if they don't exist
            PlayerRunData newRecord = new PlayerRunData
            {
                playerName = currentName,
                floorsCleared = LevelManager.Instance.CurrentLevelIndex,
                totalTurns = TurnManager.Instance.CurrentTurnNumber,
                totalTimeSeconds = RunManager.Instance.TotalRunTime,
                runResult = result,
                datePlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            db.records.Add(newRecord);
            Debug.Log($"Created new record for {currentName}.");
        }

        // 4. Sort the records before saving them to disk
        SortRecords(db.records);

        // 5. Save the entire database back to the file
        string newJson = JsonUtility.ToJson(db, true);
        File.WriteAllText(_saveFilePath, newJson); 
    }

    public bool IsNewRunBetter(string inputName, int newFloors, int newTurns, float newTime)
    {
        RunDatabase db = LoadDatabase();

        PlayerRunData existingRecord = db.records.Find(r => r.playerName == inputName);

        if (existingRecord == null) return true;

        if (newFloors > existingRecord.floorsCleared) return true;
        if (newFloors == existingRecord.floorsCleared && newTurns < existingRecord.totalTurns) return true;
        if (newFloors == existingRecord.floorsCleared && newTurns == existingRecord.totalTurns && newTime < existingRecord.totalTimeSeconds) return true;

        return false;
    }

    public List<PlayerRunData> GetLeaderboard()
    {
        RunDatabase db = LoadDatabase();
        
        // Use the shared sorting logic
        SortRecords(db.records);

        return db.records;
    }

    private void SortRecords(List<PlayerRunData> recordsToSort)
    {
        recordsToSort.Sort((a, b) => 
        {
            // 1. Highest Floors Cleared
            int floorCompare = b.floorsCleared.CompareTo(a.floorsCleared);
            if (floorCompare != 0) return floorCompare;

            // 2. Tie-breaker: Fewest Total Turns (Ascending)
            int turnCompare = a.totalTurns.CompareTo(b.totalTurns);
            if (turnCompare != 0) return turnCompare;

            // 3. Tie-breaker: Fastest Time (Ascending)
            return a.totalTimeSeconds.CompareTo(b.totalTimeSeconds);
        });
    }
}