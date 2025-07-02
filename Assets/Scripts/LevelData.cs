using System;
using UnityEngine;

[Serializable]
public class LevelData
{
    public int width;
    public int height;
    public string[] tiles; // flaches Array von Zeilen (String-Zeilen wie "S...W")
}
