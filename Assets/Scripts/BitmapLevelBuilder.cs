using UnityEngine;
using System.Collections.Generic;

public class BitmapLevelBuilder : MonoBehaviour
{
    [Header("Level Configuration")]
    public string imageName = "Levels/maze1";
    
    [Header("Prefab References")]
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject holePrefab;
    public GameObject startPrefab;
    public GameObject endPrefab;
    public GameObject marblePrefab;
    
    [Header("Debug Options")]
    public bool enableDebugLogs = true;
    public bool showColorDetection = false;
    
    private GameObject marbleInstance;
    private HashSet<Color32> seenColors = new HashSet<Color32>();
    
    // Color mappings based on debugged values
    private readonly Color32 BLACK_WALL = new Color32(0, 0, 0, 255);
    private readonly Color32 WHITE_FLOOR = new Color32(255, 255, 255, 255);
    private readonly Color32 BLUE_HOLE = new Color32(0, 0, 255, 255);
    private readonly Color32 RED_END = new Color32(237, 28, 36, 255);
    private readonly Color32 GREEN_START = new Color32(34, 177, 76, 255);

    void Start()
    {
        if (!ValidatePrefabs())
        {
            Debug.LogError("❌ Missing prefab references! Please assign all prefabs in the inspector.");
            return;
        }
        
        BuildLevelFromBitmap();
    }
    
    private bool ValidatePrefabs()
    {
        return wallPrefab != null && floorPrefab != null && holePrefab != null && 
               startPrefab != null && endPrefab != null && marblePrefab != null;
    }
    
    private void BuildLevelFromBitmap()
    {
        Texture2D image = Resources.Load<Texture2D>(imageName);
        if (image == null)
        {
            Debug.LogError($"❌ Maze image not found at Resources/{imageName}.png");
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"🗺️ Building level from {imageName} ({image.width}x{image.height})");
        }
        
        // Calculate offset to center the maze
        float xOffset = -(image.width - 1) / 2f;
        float zOffset = (image.height - 1) / 2f;
        
        // Process each pixel
        for (int y = 0; y < image.height; y++)
        {
            for (int x = 0; x < image.width; x++)
            {
                Color32 pixel = (Color32)image.GetPixel(x, y);
                Vector3 position = new Vector3(x + xOffset, 0, -y + zOffset);
                
                // Track unique colors for debugging
                if (showColorDetection && !seenColors.Contains(pixel))
                {
                    seenColors.Add(pixel);
                    Debug.Log($"🎨 New color at ({x},{y}): R={pixel.r}, G={pixel.g}, B={pixel.b}, A={pixel.a}");
                }
                
                // Get and instantiate the appropriate prefab
                GameObject prefabToSpawn = GetPrefabForColor(pixel);
                if (prefabToSpawn != null)
                {
                    Instantiate(prefabToSpawn, position, Quaternion.identity, transform);
                }
                
                // Special handling for marble placement on start position
                if (IsColorMatch(pixel, GREEN_START) && marblePrefab != null)
                {
                    // Floor tiles are 0.2 units thick (Y scale), so top is at Y: 0.1
                    // Marble radius is 0.25, so place at Y: 0.35 (0.1 + 0.25) to rest on floor
                    Vector3 marblePosition = position + Vector3.up * 0.35f;
                    marbleInstance = Instantiate(marblePrefab, marblePosition, Quaternion.identity);
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"🔵 Marble placed at start position: {marblePosition}");
                    }
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"✅ Level built successfully! Total unique colors found: {seenColors.Count}");
        }
    }
    
    private GameObject GetPrefabForColor(Color32 color)
    {
        // Exact color matching based on debugged values
        if (IsColorMatch(color, BLACK_WALL))
        {
            LogColorDetection(color, "Wall (Black)");
            return wallPrefab;
        }
        
        if (IsColorMatch(color, WHITE_FLOOR))
        {
            LogColorDetection(color, "Floor (White)");
            return floorPrefab;
        }
        
        if (IsColorMatch(color, BLUE_HOLE))
        {
            LogColorDetection(color, "Hole (Blue)");
            return holePrefab;
        }
        
        if (IsColorMatch(color, RED_END))
        {
            LogColorDetection(color, "End (Red)");
            return endPrefab;
        }
        
        if (IsColorMatch(color, GREEN_START))
        {
            LogColorDetection(color, "Start (Green)");
            return startPrefab;
        }
        
        // Unknown color
        if (enableDebugLogs)
        {
            Debug.LogWarning($"⚠️ Unknown color: R={color.r}, G={color.g}, B={color.b}, A={color.a}");
        }
        
        return null;
    }
    
    private bool IsColorMatch(Color32 a, Color32 b, int tolerance = 0)
    {
        // Exact match by default, but tolerance can be added if needed
        return Mathf.Abs(a.r - b.r) <= tolerance &&
               Mathf.Abs(a.g - b.g) <= tolerance &&
               Mathf.Abs(a.b - b.b) <= tolerance &&
               Mathf.Abs(a.a - b.a) <= tolerance;
    }
    
    private void LogColorDetection(Color32 color, string type)
    {
        if (enableDebugLogs && showColorDetection)
        {
            Debug.Log($"✅ {type} detected: R={color.r}, G={color.g}, B={color.b}, A={color.a}");
        }
    }
    
    // Public method to get the marble instance (useful for game logic)
    public GameObject GetMarbleInstance()
    {
        return marbleInstance;
    }
    
    // Public method to rebuild the level
    public void RebuildLevel()
    {
        // Clear existing children
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        
        // Clear marble reference
        if (marbleInstance != null)
        {
            DestroyImmediate(marbleInstance);
            marbleInstance = null;
        }
        
        // Reset seen colors
        seenColors.Clear();
        
        // Rebuild
        BuildLevelFromBitmap();
    }
}
