using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helper methods to compute aggregate world-space bounds from renderers.
/// All returned <see cref="Bounds"/> are in world space.
/// </summary>
public static class BoundsUtils
{
    /// <summary>
    /// Encapsulates all renderer bounds into a single world-space bounds.
    /// Returns an empty bounds if the input is null or empty.
    /// </summary>
    public static Bounds CalculateCombinedBounds(Renderer[] renderers)
    {
        var bounds = new Bounds();
        if (renderers == null || renderers.Length == 0) return bounds;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    /// <summary>
    /// Encapsulates all renderer bounds into a single world-space bounds.
    /// Returns an empty bounds if the input is null or empty.
    /// </summary>
    public static Bounds CalculateCombinedBounds(List<Renderer> renderers)
    {
        var bounds = new Bounds();
        if (renderers == null || renderers.Count == 0) return bounds;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    /// <summary>
    /// Tries to compute world-space bounds that encapsulate all renderers under <paramref name="root"/>.
    /// Returns true on success and assigns the combined bounds; otherwise returns false.
    /// </summary>
    public static bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        if (root == null) return false;
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0) return false;
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return true;
    }
}


