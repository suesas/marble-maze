using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight on-screen HUD that displays position and smoothed velocity of a target transform.
/// Optimized to reduce canvas rebuilds by updating text at a fixed rate.
/// </summary>
[DisallowMultipleComponent]
public class TrackingHUD : MonoBehaviour
{
	[Header("Target")]
	[Tooltip("Transform to monitor and display (typically the tracking marker).")]
	[SerializeField] public Transform target; // typically the tracking marker
	[Tooltip("Include Y coordinate in the HUD readout.")]
	[SerializeField] public bool showY = false; // show Y in readout

	[Header("Display")]
	[Tooltip("Header title shown at the top of the HUD readout.")]
	[SerializeField] public string title = "HUD";
	[Tooltip("Show a bold header title at the top of the HUD.")]
	[SerializeField] public bool showHeader = true;

	[Header("UI")]
	[Tooltip("UI Text component that displays the HUD readout.")]
	[SerializeField] public Text label;
	[Tooltip("Numeric format string used for coordinates and speed (e.g., F3).")]
	[SerializeField] public string format = "F3"; // numeric format
	[Tooltip("UI update rate in Hz (times per second).")]
	[SerializeField] public float updateHz = 30f; // UI update rate

	[Header("Velocity Smoothing")]
	[Tooltip("Time constant for exponential smoothing of velocity (seconds).")]
	[SerializeField] public float velocitySmoothingSeconds = 0.15f;

	private Vector3 previousPosition;
	private Vector3 smoothedVelocity;
	private float nextUiUpdateAt;
	private bool hasPrev;

	/// <summary>
	/// References are expected to be injected by setup services.
	/// </summary>
	private void Awake()
	{
		// References should be injected by setup service; avoid global Find fallbacks
	}

	/// <summary>
	/// Computes smoothed velocity and updates the HUD text at a throttled rate.
	/// </summary>
	private void Update()
	{
		if (target == null || label == null) return;

		Vector3 pos = target.position;
		float dt = Mathf.Max(Time.deltaTime, 1e-5f);
		Vector3 rawVelocity = Vector3.zero;
		if (hasPrev)
		{
			rawVelocity = (pos - previousPosition) / dt;
			float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(velocitySmoothingSeconds, 1e-4f));
			smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, alpha);
		}
		previousPosition = pos;
		hasPrev = true;

		if (Time.time < nextUiUpdateAt) return;
		nextUiUpdateAt = Time.time + (updateHz > 0f ? (1f / updateHz) : 0f);

		var sb = new StringBuilder(192);
		if (showHeader && !string.IsNullOrEmpty(title))
		{
			if (label.supportRichText)
			{
				sb.Append("<b>").Append(title).Append("</b>\n");
			}
			else
			{
				sb.Append(title).Append("\n");
			}
		}
		sb.Append("Pos ");
		sb.Append("x=").Append(pos.x.ToString(format));
		sb.Append(" z=").Append(pos.z.ToString(format));
		if (showY) sb.Append(" y=").Append(pos.y.ToString(format));
		sb.Append("\nVel ");
		sb.Append("x=").Append(smoothedVelocity.x.ToString(format));
		sb.Append(" z=").Append(smoothedVelocity.z.ToString(format));
		if (showY) sb.Append(" y=").Append(smoothedVelocity.y.ToString(format));
		sb.Append(" | speed=").Append(smoothedVelocity.magnitude.ToString(format));

		label.text = sb.ToString();
		// Avoid SetAllDirty to prevent expensive canvas rebuilds on every frame
	}
}


