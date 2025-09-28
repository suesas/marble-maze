using System;
using UnityEditor;

/// <summary>
/// Thin disposable wrapper for EditorUtility progress bars. Ensures Clear() is called.
/// Usage:
/// using (new ProgressScope("Title", "Working...", 0.1f)) { /* Update calls */ }
/// </summary>
public sealed class ProgressScope : IDisposable
{
    private bool cleared;

    /// <summary>
    /// Shows an optional progress bar immediately.
    /// </summary>
    public ProgressScope(string title = null, string info = null, float progress = 0f)
    {
        if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(info))
        {
            EditorUtility.DisplayProgressBar(title ?? "", info ?? "", progress);
        }
    }

    /// <summary>
    /// Updates the progress bar content and fraction.
    /// </summary>
    public void Update(string title, string info, float progress)
    {
        EditorUtility.DisplayProgressBar(title, info, progress);
    }

    /// <summary>
    /// Clears the progress bar once; subsequent calls are no-ops.
    /// </summary>
    public void Clear()
    {
        if (cleared) return;
        EditorUtility.ClearProgressBar();
        cleared = true;
    }

    /// <summary>
    /// Ensures the progress bar is cleared.
    /// </summary>
    public void Dispose()
    {
        Clear();
    }
}




