using UnityEngine;

/// <summary>
/// Constrains this transform's local rotation to either the X or Z axis, or fully freezes it.
/// When a tilt driver (e.g., GimbalRig or TiltController) is present at runtime, the component
/// avoids redundant rotation writes to reduce transform churn.
/// </summary>
[DisallowMultipleComponent]
public class AxisLock : MonoBehaviour
{
    /// <summary>
    /// Axis to keep while zeroing the other local Euler angles.
    /// </summary>
    public enum Axis { X, Z }
    public Axis axis = Axis.X;
    public bool freezeAll = false; // if true: keep identity rotation

    private bool checkedForDrivers;
    private bool driversPresent;

    /// <summary>
    /// Applies rotation constraints each frame, skipping writes if a driver is active in play mode.
    /// </summary>
    void LateUpdate()
    {
        // Skip writes when a rig/driver controls rotations to avoid extra transform sync cost.
        if (Application.isPlaying)
        {
            if (!checkedForDrivers)
            {
                var rig = FindObjectOfType<GimbalRig>();
                var tc = FindObjectOfType<TiltController>();
                driversPresent = (rig != null || tc != null);
                checkedForDrivers = true;
            }
            if (driversPresent)
            {
                return;
            }
        }
        if (freezeAll)
        {
            transform.localRotation = Quaternion.identity;
            return;
        }
        Vector3 e = transform.localEulerAngles;
        if (axis == Axis.X)
        {
            transform.localRotation = Quaternion.Euler(e.x, 0f, 0f);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0f, 0f, e.z);
        }
    }
}


