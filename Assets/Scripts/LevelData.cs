using System;
using UnityEngine;

/// <summary>
/// Minimal serializable representation of a tile-based level.
/// Dimensions are stored alongside a flattened row-major tile array.
/// </summary>
[Serializable]
public class LevelData
{
    /// <summary>Level width in tiles.</summary>
    public int width;
    /// <summary>Level height in tiles.</summary>
    public int height;
    /// <summary>
    /// Flattened rows of tiles; each string represents one row (e.g., "S...W").
    /// </summary>
    public string[] tiles;
}
