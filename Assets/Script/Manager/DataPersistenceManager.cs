using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System;

// The individual player data
[Serializable]
public class PlayerRunData
{
    public string playerName;
    public string phoneNumber;
    public int floorsCleared;
    public int totalTurns;
    public float totalTimeSeconds;
    public string runResult; 
    public string datePlayed;
}

// NEW: A wrapper class so Unity can serialize a List to JSON
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

    public bool DoesPlayerExist(string name, string phone)
    {
        RunDatabase db = LoadDatabase();
        return db.records.Exists(r => r.playerName == name && r.phoneNumber == phone);
    }

    public void SaveRunData(string result)
    {
        // 1. Load existing records
        RunDatabase db = LoadDatabase();

        string currentName = PlayerPrefs.GetString("PlayerName", "Unknown");
        string currentPhone = PlayerPrefs.GetString("PlayerPhone", "Unknown");

        // 2. Look for this exact player in the database
        PlayerRunData existingRecord = db.records.Find(r => r.playerName == currentName && r.phoneNumber == currentPhone);

        if (existingRecord != null)
        {
            if(IsNewRunBetter(currentName, currentPhone, LevelManager.Instance.CurrentLevelIndex, TurnManager.Instance.CurrentTurnNumber, RunManager.Instance.TotalRunTime))
            {
                existingRecord.floorsCleared = LevelManager.Instance.CurrentLevelIndex;
                existingRecord.totalTurns = TurnManager.Instance.CurrentTurnNumber;
                existingRecord.totalTimeSeconds = RunManager.Instance.TotalRunTime;
                existingRecord.runResult = result;
                existingRecord.datePlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Debug.Log($"Overwrote previous run for {currentName}.");
            } else 
                Debug.Log($"Existing run for {currentName} is better than the new run. Not overwriting.");
        }
        else
        {
            // 3b. Create a brand new record if they don't exist
            PlayerRunData newRecord = new PlayerRunData
            {
                playerName = currentName,
                phoneNumber = currentPhone,
                floorsCleared = LevelManager.Instance.CurrentLevelIndex,
                totalTurns = TurnManager.Instance.CurrentTurnNumber,
                totalTimeSeconds = RunManager.Instance.TotalRunTime,
                runResult = result,
                datePlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };
            db.records.Add(newRecord);
            Debug.Log($"Created new record for {currentName}.");
        }

        // 4. Save the entire database back to the file
        string newJson = JsonUtility.ToJson(db, true);
        File.WriteAllText(_saveFilePath, newJson); // WriteAllText OVERWRITES the whole file
    }

    public bool IsPhoneStolen(string inputName, string inputPhone)
    {
        RunDatabase db = LoadDatabase();

        PlayerRunData recordWithThisPhone = db.records.Find(r => r.phoneNumber == inputPhone);

        if (recordWithThisPhone != null && recordWithThisPhone.playerName != inputName)
        {
            return true;
        }

        return false;
    }

    public bool IsNewRunBetter(string inputName, string inputPhone, int newFloors, int newTurns, float newTime)
    {
        RunDatabase db = LoadDatabase();

        PlayerRunData existingRecord = db.records.Find(r => r.playerName == inputName && r.phoneNumber == inputPhone);

        if (existingRecord == null) return true;

        if (newFloors > existingRecord.floorsCleared) return true;
        if (newFloors == existingRecord.floorsCleared && newTurns < existingRecord.totalTurns) return true;
        if (newFloors == existingRecord.floorsCleared && newTurns == existingRecord.totalTurns && newTime < existingRecord.totalTimeSeconds) return true;

        return false;
    }

    public List<PlayerRunData> GetLeaderboard()
    {
        RunDatabase db = LoadDatabase();

        // Sort the records!
        db.records.Sort((a, b) => 
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

        return db.records;
    }
}