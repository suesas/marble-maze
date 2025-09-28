using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Projects the real ball's screen/viewport position back into world space to place a tracking marker.
/// Can optionally project onto a floor plane or reconstruct a 3D point using camera depth.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Tracking/Ball Position From Camera")]
public class BallPositionFromCamera : MonoBehaviour
{
	[Header("References")]
	[Tooltip("Transform of the real marble/ball to read the position from.")]
	[SerializeField] public Transform ball;
	[Tooltip("Transform of the tracking marker to position based on the ball.")]
	[SerializeField, FormerlySerializedAs("trackingBall")] public Transform trackingMarker;
	[Tooltip("If enabled, project the tracking marker onto the floor plane.")]
	[SerializeField, FormerlySerializedAs("placeTrackingOnFloor")] public bool placeTrackingMarkerOnFloor = false;
	[Tooltip("Optional: floor transform used when projecting onto the floor plane.")]
	[SerializeField] public Transform floor; // optional: used when placing on floor
	[Header("Behaviour")]
	[Tooltip("If true, this component positions the tracking marker each frame.")]
	[SerializeField] public bool enableDirectTracking = false; // if true, this script positions the tracking marker
	[Tooltip("Log world/screen/viewport coordinates for debugging.")]
	[SerializeField] public bool logDebug = false;

	private Camera mainCam;

	/// <summary>
	/// Caches main camera reference and provides a name-based fallback for the ball if not assigned.
	/// </summary>
	private void Awake()
	{
		mainCam = Camera.main;
		if (ball == null)
		{
			var found = GameObject.Find("Ball");
			if (found != null)
			{
				ball = found.transform;
				Debug.LogWarning("BallPositionFromCamera: 'ball' reference was not set. Falling back to name-based lookup 'Ball'. Consider wiring this reference in the inspector for robustness.");
			}
		}
	}

	/// <summary>
	/// Updates the tracking marker based on the ball's projected position if enabled.
	/// </summary>
	private void Update()
	{
		if (ball == null || mainCam == null) return;

		Vector3 worldPos = ball.position;
		Vector3 viewportPos = mainCam.WorldToViewportPoint(worldPos);
		Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);
		Vector3 relativeToCam = mainCam.transform.InverseTransformPoint(worldPos);

		if (logDebug)
		{
			Debug.Log($"World: {worldPos} | Viewport: {viewportPos} | Screen: {screenPos} | RelCam: {relativeToCam}");
		}

		// Drive tracking marker from calculated position if enabled
		if (enableDirectTracking && trackingMarker != null)
		{
			if (placeTrackingMarkerOnFloor)
			{
				// Project camera ray through viewport position onto the floor plane
				Ray ray = mainCam.ViewportPointToRay(new Vector3(viewportPos.x, viewportPos.y, 0f));
				Vector3 planeNormal = Vector3.up;
				Vector3 planePoint = Vector3.zero;
				if (floor != null)
				{
					planeNormal = floor.up;
					planePoint = floor.position;
				}
				Plane plane = new Plane(planeNormal, planePoint);
				if (plane.Raycast(ray, out float distance))
				{
					Vector3 hit = ray.GetPoint(distance);
					// Offset by radius (0.5 for Unity Sphere) along plane normal so the marker sits visibly above floor
					trackingMarker.position = hit + planeNormal * 0.5f;
				}
			}
			else
			{
				// Reconstruct world from camera-calculated viewport + depth
				Vector3 recon = mainCam.ViewportToWorldPoint(new Vector3(viewportPos.x, viewportPos.y, viewportPos.z));
				trackingMarker.position = recon;
			}
		}
	}
}


