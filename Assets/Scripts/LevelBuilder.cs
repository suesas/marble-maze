using System;
using UnityEngine;

public class LevelBuilder : MonoBehaviour
{
    public GameObject wallPrefab, floorPrefab, holePrefab, startPrefab, endPrefab, marble;
    public string levelName = "Levels/level1";

    private GameObject marbleInstance;

    void Start()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(levelName);
        if (jsonFile == null)
        {
            Debug.LogError("⚠️ Level JSON not found at Resources/" + levelName);
            return;
        }

        LevelData level = JsonUtility.FromJson<LevelData>(jsonFile.text);
        if (level == null || level.tiles == null || level.tiles.Length == 0)
        {
            Debug.LogError("⚠️ JSON parsing failed or 'tiles' is null/empty");
            return;
        }

        BuildOuterWalls(level.width, level.height);
        BuildLevel(level);
    }

    void BuildLevel(LevelData level)
    {
        float xOffset = -(level.width / 2f) + 0.5f;
        float zOffset = (level.height / 2f) - 0.5f;

        for (int y = 0; y < level.height; y++)
        {
            string row = level.tiles[y];
            for (int x = 0; x < level.width; x++)
            {
                char symbol = row[x];
                Vector3 pos = new Vector3(x + xOffset, 0, -y + zOffset);

                GameObject prefab = symbol switch
                {
                    'S' => startPrefab,
                    'E' => endPrefab,
                    'W' => wallPrefab,
                    'H' => holePrefab,
                    '.' => floorPrefab,
                    _ => null
                };

                if (prefab != null)
                    Instantiate(prefab, pos, Quaternion.identity, transform);

                if (symbol == 'S' && marble != null)
                    marbleInstance = Instantiate(marble, pos + Vector3.up * 0.5f, Quaternion.identity);
                    Rigidbody rb = marbleInstance.GetComponent<Rigidbody>();
                    rb.WakeUp();
                    rb.sleepThreshold = 0f; 
            }
        }
    }

    void BuildOuterWalls(int width, int height)
    {
        float xOffset = -(width / 2f) + 0.5f;
        float zOffset = (height / 2f) - 0.5f;

        for (int x = -1; x <= width; x++)
        {
            Instantiate(wallPrefab, new Vector3(x + xOffset, 0, 1 + zOffset), Quaternion.identity, transform);
            Instantiate(wallPrefab, new Vector3(x + xOffset, 0, -height + zOffset), Quaternion.identity, transform);
        }

        for (int y = 0; y < height; y++)
        {
            Instantiate(wallPrefab, new Vector3(-1 + xOffset, 0, -y + zOffset), Quaternion.identity, transform);
            Instantiate(wallPrefab, new Vector3(width + xOffset, 0, -y + zOffset), Quaternion.identity, transform);
        }
    }
}
