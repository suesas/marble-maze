using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Keeps a tracking camera aligned with a (potentially tilted) floor so
/// the top-down projection remains consistent for ColorBallTracker.
/// It positions the camera above the floor along the floor's local up axis and
/// looks straight down.
/// </summary>
[DefaultExecutionOrder(20)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Tracking/Tracking Camera Follower")]
public class TrackingCameraFollower : MonoBehaviour
{
	[SerializeField] public Transform floor;
	[FormerlySerializedAs("trackingCam")] [SerializeField] public Camera trackingCam;
	[SerializeField] public float height = 10f;
	[SerializeField] public float padding = 1.0f; // extra orthographic size padding

	private void Awake()
	{
		if (trackingCam == null) trackingCam = GetComponent<Camera>();
		if (floor == null)
		{
			var go = GameObject.Find("Floor");
			if (go != null) floor = go.transform;
		}
	}

	private void LateUpdate()
	{
		if (trackingCam == null || floor == null) return;
		// Compute bounds from renderers when available (more reliable than localScale)
        Bounds b;
        if (!BoundsUtils.TryGetWorldBounds(floor, out b))
		{
			// Fallback: approximate from localScale
			Vector3 center = floor.position;
			float halfX = 5f * Mathf.Abs(floor.localScale.x);
			float halfZ = 5f * Mathf.Abs(floor.localScale.z);
			b = new Bounds(center, new Vector3(halfX * 2f, 0f, halfZ * 2f));
		}

		Vector3 up = floor.up;
		Vector3 centerXZ = new Vector3(b.center.x, floor.position.y, b.center.z);
		Vector3 pos = centerXZ + up * Mathf.Max(0.1f, height);
		trackingCam.transform.position = pos;
		// Look straight down the floor normal
		trackingCam.transform.rotation = Quaternion.LookRotation(-up, floor.forward);
		// Maintain orthographic size that covers bounds extents
		float halfXBound = b.extents.x;
		float halfZBound = b.extents.z;
		trackingCam.orthographic = true;
		trackingCam.orthographicSize = Mathf.Max(halfXBound, halfZBound) + Mathf.Max(0f, padding);
	}
}




