using UnityEngine;
using System.Collections.Generic;

public class BitmapLevelBuilder : MonoBehaviour
{
    public string imageName = "Levels/maze1";
    public GameObject wallPrefab, floorPrefab, holePrefab, startPrefab, endPrefab, marble;
    
    private GameObject marbleInstance;

    HashSet<Color> seenColors = new HashSet<Color>();


    void Start()
    {
        Texture2D image = Resources.Load<Texture2D>(imageName);
        if (image == null)
        {
            Debug.LogError("⚠️ Maze image not found at Resources/" + imageName + ".png");
            return;
        }

        float xOffset = -(image.width - 1) / 2f;
        float zOffset = (image.height - 1) / 2f;

        for (int y = 0; y < image.height; y++)
        {
            for (int x = 0; x < image.width; x++)
            {
                Color pixel = image.GetPixel(x, y);
                Vector3 pos = new Vector3(x + xOffset, 0, -y + zOffset);
                GameObject prefab = GetPrefabFromColorFlexible(pixel);  // Use flexible matching
                // GameObject prefab = GetPrefabFromColor(pixel);  // Use exact matching

        if (!seenColors.Contains(pixel))
        {
            seenColors.Add(pixel);
            Debug.Log($"Pixel at ({x},{y}): {pixel}");
        }

                if (prefab != null)
                    Instantiate(prefab, pos, Quaternion.identity, transform);

                if (IsColor((Color32)pixel, new Color32(89, 174, 99, 255), 50) && marble != null)
                    marbleInstance = Instantiate(marble, pos + Vector3.up * 0.5f, Quaternion.identity);
            }
        }
    }

    GameObject GetPrefabFromColor(Color color)
    {
        Color32 c = color;

        // Use actual colors from the debugged image with more generous tolerance for start/end
        if (IsColor(c, new Color32(255, 255, 255, 255), 10)) 
        {
            Debug.Log($"Floor detected: {c}");
            return floorPrefab;  // Floor White
        }
        if (IsColor(c, new Color32(0, 0, 0, 255), 10)) 
        {
            Debug.Log($"Wall detected: {c}");
            return wallPrefab;  // Wall Black
        }
        if (IsColor(c, new Color32(215, 56, 50, 255), 50)) 
        {
            Debug.Log($"END detected: {c}");
            return endPrefab;  // End Red
        }
        if (IsColor(c, new Color32(89, 174, 99, 255), 50)) 
        {
            Debug.Log($"START detected: {c}");
            return startPrefab;  // Start Green
        }
        if (IsColor(c, new Color32(7, 24, 245, 255), 50)) 
        {
            Debug.Log($"Hole detected: {c}");
            return holePrefab;  // Hole Blue
        }

        // More detailed debugging for unrecognized colors
        Debug.LogWarning($"❌ Unrecognized color: R={c.r}, G={c.g}, B={c.b}, A={c.a}");
        
        // Check how close this color is to our target colors
        Debug.Log($"Distance to WHITE: {GetColorDistance(c, new Color32(255, 255, 255, 255))}");
        Debug.Log($"Distance to BLACK: {GetColorDistance(c, new Color32(0, 0, 0, 255))}");
        Debug.Log($"Distance to RED: {GetColorDistance(c, new Color32(215, 56, 50, 255))}");
        Debug.Log($"Distance to GREEN: {GetColorDistance(c, new Color32(89, 174, 99, 255))}");
        Debug.Log($"Distance to BLUE: {GetColorDistance(c, new Color32(7, 24, 245, 255))}");
        
        return null;
    }

    bool IsColor(Color32 a, Color32 b, int tolerance = 25)
    {
        return Mathf.Abs(a.r - b.r) <= tolerance &&
               Mathf.Abs(a.g - b.g) <= tolerance &&
               Mathf.Abs(a.b - b.b) <= tolerance;
    }

    // Helper method to calculate color distance
    float GetColorDistance(Color32 a, Color32 b)
    {
        return Mathf.Sqrt(
            Mathf.Pow(a.r - b.r, 2) + 
            Mathf.Pow(a.g - b.g, 2) + 
            Mathf.Pow(a.b - b.b, 2)
        );
    }

    // Alternative: More flexible color matching
    GameObject GetPrefabFromColorFlexible(Color color)
    {
        Color32 c = color;
        
        // Define target colors and their corresponding prefabs
        var colorTargets = new[]
        {
            (color: new Color32(255, 255, 255, 255), prefab: floorPrefab, name: "Floor"),
            (color: new Color32(0, 0, 0, 255), prefab: wallPrefab, name: "Wall"),
            (color: new Color32(215, 56, 50, 255), prefab: endPrefab, name: "End"),
            (color: new Color32(89, 174, 99, 255), prefab: startPrefab, name: "Start"),
            (color: new Color32(7, 24, 245, 255), prefab: holePrefab, name: "Hole")
        };
        
        float minDistance = float.MaxValue;
        GameObject bestMatch = null;
        string bestMatchName = "";
        
        foreach (var target in colorTargets)
        {
            float distance = GetColorDistance(c, target.color);
            if (distance < minDistance)
            {
                minDistance = distance;
                bestMatch = target.prefab;
                bestMatchName = target.name;
            }
        }
        
        // Use a dynamic threshold - closer colors get matched
        float threshold = 100f; // Adjust this value based on your needs
        
        if (minDistance <= threshold && bestMatch != null)
        {
            Debug.Log($"✅ {bestMatchName} detected: {c} (distance: {minDistance:F1})");
            return bestMatch;
        }
        else
        {
            Debug.LogWarning($"❌ No close match for color: {c} (closest: {bestMatchName}, distance: {minDistance:F1})");
            return null;
        }
    }
}
