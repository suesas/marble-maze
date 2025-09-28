using UnityEngine;

/// <summary>
/// High-level driver that applies tilt inputs to a <see cref="GimbalRig"/>.
/// Supports keyboard input for manual play and programmatic control for agents.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Gameplay/Tilt Controller")]
public class TiltController : MonoBehaviour
{
    public float tiltSpeed = 30f;
    public float maxTilt = 30f;

    // Gimbal rig reference
    public GimbalRig rig;
    public Transform outerFrame; // optional: auto-wiring helper
    public Transform innerFrame; // optional: auto-wiring helper
    public Transform boardRoot;  // optional: not used by rig, kept for compatibility
    public bool syncWithBoardRotation = false; // off by default

    public bool keyboardInput = true;
    private float currentTiltX = 0f;
    private float currentTiltZ = 0f;

    /// <summary>
    /// Auto-wires rig and frame references; initializes to neutral tilt.
    /// </summary>
    void Awake()
    {
        if (rig == null)
        {
            rig = GetComponent<GimbalRig>();
        }
        if (outerFrame == null)
        {
            var t = transform.Find("OuterFrame");
            if (t != null) outerFrame = t;
        }
        if (innerFrame == null)
        {
            var t = transform.Find("OuterFrame/InnerFrame");
            if (t != null) innerFrame = t;
        }
        // push into rig
        if (rig != null)
        {
            // Wire strictly: inner = InnerFrame, outer = OuterFrame
            rig.innerX = innerFrame;
            rig.outerZ = outerFrame;
            rig.SetTilt(0f, 0f); // initialize to identity
        }
        if (boardRoot == null && innerFrame != null)
        {
            // Expect the board (Level_*) to be a direct child of InnerFrame
            for (int i = 0; i < innerFrame.childCount; i++)
            {
                var ch = innerFrame.GetChild(i);
                if (ch.name.StartsWith("Level_")) { boardRoot = ch; break; }
            }
        }
    }

    /// <summary>
    /// Sets the target tilt angles; values are clamped to <see cref="maxTilt"/>.
    /// </summary>
    public void SetTilt(float tiltX, float tiltZ)
    {
        currentTiltX = Mathf.Clamp(tiltX, -maxTilt, maxTilt);
        currentTiltZ = Mathf.Clamp(tiltZ, -maxTilt, maxTilt);
        if (rig != null) rig.SetTilt(currentTiltX, currentTiltZ);
    }

    /// <summary>
    /// Applies tilt during physics to keep colliders in sync.
    /// </summary>
    void FixedUpdate()
    {
        // Apply tilt during the physics step to keep collider poses in sync with physics
        if (rig != null)
        {
            rig.SetTilt(currentTiltX, currentTiltZ);
            // Only rotate the boardRoot around Z directly if the outer frame is frozen
            if (rig.freezeOuter && boardRoot != null)
            {
                boardRoot.localRotation = Quaternion.Euler(0f, 0f, currentTiltZ);
            }
        }
        else
        {
            // No rig present: rotate this transform directly for manual control
            transform.rotation = Quaternion.Euler(currentTiltX, 0, currentTiltZ);
        }
    }

    /// <summary>
    /// Handles optional keyboard control and optional board rotation synchronization.
    /// </summary>
    void Update()
    {
        // Optional: sync board rotation into frames if explicitly enabled
        if (syncWithBoardRotation && boardRoot != null && outerFrame != null && innerFrame != null)
        {
            Vector3 e = boardRoot.localEulerAngles;
            float ex = NormalizeAngle(e.x);
            float ez = NormalizeAngle(e.z);
            if (Mathf.Abs(ex) > 0.001f || Mathf.Abs(ez) > 0.001f)
            {
                currentTiltX = Mathf.Clamp(ex, -maxTilt, maxTilt);
                currentTiltZ = Mathf.Clamp(ez, -maxTilt, maxTilt);
                boardRoot.localRotation = Quaternion.identity;
            }
        }

        if (keyboardInput)
        {
            float inputX = Input.GetAxis("Vertical");
            float inputZ = -Input.GetAxis("Horizontal");
            currentTiltX = Mathf.Clamp(currentTiltX + inputX * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);
            currentTiltZ = Mathf.Clamp(currentTiltZ + inputZ * tiltSpeed * Time.deltaTime, -maxTilt, maxTilt);
        }

    }

    /// <summary>
    /// Resets target tilt and underlying rig to neutral.
    /// </summary>
    public void ResetRig()
    {
        currentTiltX = 0f;
        currentTiltZ = 0f;
        if (rig != null) rig.ResetRig();
        if (boardRoot != null) boardRoot.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Normalizes an angle to the range [-180, 180].
    /// </summary>
    private static float NormalizeAngle(float a)
    {
        while (a > 180f) a -= 360f;
        while (a < -180f) a += 360f;
        return a;
    }

}
