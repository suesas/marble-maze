using UnityEngine;

/// <summary>
/// Cardanic rig with two orthogonal pivots for applying independent X/Z tilts.
/// The outer pivot typically represents Z tilt, the inner pivot X tilt. Optionally freezes outer.
/// </summary>
[DisallowMultipleComponent]
public class GimbalRig : MonoBehaviour
{
    public Transform outerZ; // TiltRig/OuterFrame (rotates around Z)
    public Transform innerX; // TiltRig/OuterFrame/InnerFrame (rotates around X)
    public float maxTilt = 30f;
    public bool swapAxes = false; // if true: outer uses X, inner uses Z
    public float outerSign = 1f; // set to -1 to invert direction
    public float innerSign = 1f; // set to -1 to invert direction
    public bool freezeOuter = true; // if true, never rotate outer pivot

    void Awake() { AutoWire(); }
    void OnValidate() { AutoWire(); }
    /// <summary>
    /// Attempts to find default child transforms by name when not assigned.
    /// </summary>
    private void AutoWire()
    {
        if (outerZ == null)
        {
            var t = transform.Find("OuterFrame");
            if (t != null) outerZ = t;
        }
        if (innerX == null)
        {
            var t = transform.Find("OuterFrame/InnerFrame");
            if (t != null) innerX = t;
        }
    }

    /// <summary>
    /// Applies clamped tilt angles to the rig, honoring axis mapping and freeze settings.
    /// </summary>
    public void SetTilt(float tiltX, float tiltZ)
    {
        float clampedX = Mathf.Clamp(tiltX, -maxTilt, maxTilt);
        float clampedZ = Mathf.Clamp(tiltZ, -maxTilt, maxTilt);
        if (freezeOuter)
        {
            if (outerZ != null) outerZ.localRotation = Quaternion.identity;
            // drive only inner
            if (!swapAxes)
            {
                if (innerX != null) innerX.localRotation = Quaternion.Euler(clampedX * Mathf.Sign(innerSign), 0f, 0f);
            }
            else
            {
                if (innerX != null) innerX.localRotation = Quaternion.Euler(0f, 0f, clampedZ * Mathf.Sign(innerSign));
            }
        }
        else
        {
            if (!swapAxes)
            {
                if (outerZ != null) outerZ.localRotation = Quaternion.Euler(0f, 0f, clampedZ * Mathf.Sign(outerSign));
                if (innerX != null) innerX.localRotation = Quaternion.Euler(clampedX * Mathf.Sign(innerSign), 0f, 0f);
            }
            else
            {
                if (outerZ != null) outerZ.localRotation = Quaternion.Euler(0f, 0f, clampedX * Mathf.Sign(outerSign));
                if (innerX != null) innerX.localRotation = Quaternion.Euler(0f, 0f, clampedZ * Mathf.Sign(innerSign));
            }
        }
    }

    /// <summary>
    /// Resets both pivots to the identity rotation.
    /// </summary>
    public void ResetRig()
    {
        SetTilt(0f, 0f);
    }
}


