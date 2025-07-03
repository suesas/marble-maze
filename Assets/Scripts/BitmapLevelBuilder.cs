using UnityEngine;
using System.Collections.Generic;

public class BitmapLevelBuilder : MonoBehaviour
{
    [Header("Level Configuration")]
    public string imageName = "Levels/maze1";
    
    [Header("Tile Prefab References")]
    public GameObject floorPrefab;
    public GameObject holePrefab;
    public GameObject startPrefab;
    public GameObject endPrefab;
    public GameObject marblePrefab;
    
    [Header("Wall Prefab References")]
    public GameObject wallSoloPrefab;      // Isolated wall piece
    public GameObject wallEndPrefab;       // Dead end wall
    public GameObject wallStraightPrefab;  // Straight wall segment
    public GameObject wallCornerPrefab;    // L-shaped corner
    public GameObject wallTJunctionPrefab; // T-shaped junction
    public GameObject wallCrossPrefab;     // Cross intersection
    
    [Header("Debug Options")]
    public bool enableDebugLogs = true;
    public bool showColorDetection = false;
    public bool showWallBitmasks = false;
    
    private GameObject marbleInstance;
    private HashSet<Color32> seenColors = new HashSet<Color32>();
    private Texture2D levelImage;
    
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
        return floorPrefab != null && holePrefab != null && startPrefab != null && 
               endPrefab != null && marblePrefab != null &&
               wallSoloPrefab != null && wallEndPrefab != null && wallStraightPrefab != null &&
               wallCornerPrefab != null && wallTJunctionPrefab != null && wallCrossPrefab != null;
    }
    
    private void BuildLevelFromBitmap()
    {
        levelImage = Resources.Load<Texture2D>(imageName);
        if (levelImage == null)
        {
            Debug.LogError($"❌ Maze image not found at Resources/{imageName}.png");
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"🗺️ Building level from {imageName} ({levelImage.width}x{levelImage.height})");
        }
        
        // Calculate offset to center the maze
        float xOffset = -(levelImage.width - 1) / 2f;
        float zOffset = (levelImage.height - 1) / 2f;
        
        // First pass: Process non-wall tiles
        for (int y = 0; y < levelImage.height; y++)
        {
            for (int x = 0; x < levelImage.width; x++)
            {
                Color32 pixel = (Color32)levelImage.GetPixel(x, y);
                Vector3 position = new Vector3(x + xOffset, 0, -y + zOffset);
                
                // Track unique colors for debugging
                if (showColorDetection && !seenColors.Contains(pixel))
                {
                    seenColors.Add(pixel);
                    Debug.Log($"🎨 New color at ({x},{y}): R={pixel.r}, G={pixel.g}, B={pixel.b}, A={pixel.a}");
                }
                
                // Get and instantiate the appropriate prefab (except walls)
                GameObject prefabToSpawn = GetNonWallPrefabForColor(pixel);
                if (prefabToSpawn != null)
                {
                    Instantiate(prefabToSpawn, position, Quaternion.identity, transform);
                }
                
                // Special handling for marble placement on start position
                if (IsColorMatch(pixel, GREEN_START) && marblePrefab != null)
                {
                    Vector3 marblePosition = position + Vector3.up * 0.35f;
                    marbleInstance = Instantiate(marblePrefab, marblePosition, Quaternion.identity);
                    Rigidbody marbleRigidbody = marbleInstance.GetComponent<Rigidbody>();
                    marbleRigidbody.WakeUp();
                    marbleRigidbody.sleepThreshold = 0.0f;

                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"🔵 Marble placed at start position: {marblePosition}");
                    }
                }
            }
        }
        
        // Second pass: Process walls with intelligent placement
        for (int y = 0; y < levelImage.height; y++)
        {
            for (int x = 0; x < levelImage.width; x++)
            {
                Color32 pixel = (Color32)levelImage.GetPixel(x, y);
                
                if (IsColorMatch(pixel, BLACK_WALL))
                {
                    Vector3 position = new Vector3(x + xOffset, 0, -y + zOffset);
                    PlaceIntelligentWall(x, y, position);
                }
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"✅ Level built successfully! Total unique colors found: {seenColors.Count}");
        }
    }
    
    private void PlaceIntelligentWall(int x, int y, Vector3 position)
    {
        // First, place a floor tile underneath the wall
        if (floorPrefab != null)
        {
            Instantiate(floorPrefab, position, Quaternion.identity, transform);
        }
        
        int bitmask = GetWallBitmask(x, y);
        
        if (showWallBitmasks)
        {
            Debug.Log($"🧱 Wall at ({x},{y}) - Bitmask: {bitmask}");
        }
        
        GameObject wallPrefab = GetWallPrefabForBitmask(bitmask);
        float rotationY = GetWallRotationForBitmask(bitmask);
        
        if (wallPrefab != null)
        {
            Quaternion rotation = Quaternion.Euler(0, rotationY, 0);
            Instantiate(wallPrefab, position, rotation, transform);
            
            if (showWallBitmasks)
            {
                Debug.Log($"🔧 Placed {wallPrefab.name} at ({x},{y}) with rotation {rotationY}° (with floor underneath)");
            }
        }
    }
    
    private int GetWallBitmask(int x, int y)
    {
        int bitmask = 0;
        
        // Check cardinal directions: N=1, E=2, S=4, W=8
        if (IsWall(x, y - 1)) bitmask += 1; // North
        if (IsWall(x + 1, y)) bitmask += 2; // East
        if (IsWall(x, y + 1)) bitmask += 4; // South
        if (IsWall(x - 1, y)) bitmask += 8; // West
        
        return bitmask;
    }
    
    private bool IsWall(int x, int y)
    {
        // Check bounds
        if (x < 0 || y < 0 || x >= levelImage.width || y >= levelImage.height) 
            return false;
        
        Color32 pixel = (Color32)levelImage.GetPixel(x, y);
        return IsColorMatch(pixel, BLACK_WALL);
    }
    
    private GameObject GetWallPrefabForBitmask(int bitmask)
    {
        switch (bitmask)
        {
            case 0:  return wallSoloPrefab;      // Isolated
            
            case 1:  return wallEndPrefab;       // End North
            case 2:  return wallEndPrefab;       // End East
            case 4:  return wallEndPrefab;       // End South
            case 8:  return wallEndPrefab;       // End West
            
            case 5:  return wallStraightPrefab;  // Straight N+S
            case 10: return wallStraightPrefab;  // Straight E+W
            
            case 3:  return wallCornerPrefab;    // Corner NE
            case 6:  return wallCornerPrefab;    // Corner SE
            case 12: return wallCornerPrefab;    // Corner SW
            case 9:  return wallCornerPrefab;    // Corner NW
            
            case 14: return wallTJunctionPrefab; // T-junction (open North)
            case 7:  return wallTJunctionPrefab; // T-junction (open West)
            case 11: return wallTJunctionPrefab; // T-junction (open South)
            case 13: return wallTJunctionPrefab; // T-junction (open East)
            
            case 15: return wallCrossPrefab;     // Cross (all directions)
            
            default:
                if (enableDebugLogs)
                {
                    Debug.LogWarning($"⚠️ Unknown wall bitmask: {bitmask}");
                }
                return wallSoloPrefab; // Fallback to solo wall
        }
    }
    
    private float GetWallRotationForBitmask(int bitmask)
    {
        switch (bitmask)
        {
            // Solo wall - no rotation needed
            case 0:  return 0f;
            
            // End walls
            case 1:  return 0f;    // North
            case 2:  return 90f;   // East
            case 4:  return 180f;  // South
            case 8:  return 270f;  // West
            
            // Straight walls
            case 5:  return 0f;    // Vertical (N+S)
            case 10: return 90f;   // Horizontal (E+W)
            
            // Corner walls
            case 3:  return 270f;  // NE
            case 6:  return 0f;    // SE
            case 12: return 90f;   // SW
            case 9:  return 180f;  // NW
            
            // T-junction walls (opening direction)
            case 14: return 180f;  // Open North
            case 7:  return 90f;   // Open West
            case 11: return 0f;    // Open South
            case 13: return 270f;  // Open East
            
            // Cross - no rotation needed
            case 15: return 0f;
            
            default: return 0f;
        }
    }
    
    private GameObject GetNonWallPrefabForColor(Color32 color)
    {
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
        
        // Don't process walls here - they're handled in the second pass
        if (IsColorMatch(color, BLACK_WALL))
        {
            return null;
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
