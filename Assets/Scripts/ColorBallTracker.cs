using UnityEngine;
using UnityEngine.Serialization;
using Unity.Collections;
using UnityEngine.Rendering;

/// <summary>
/// Samples a downscaled tracking camera render and locates a colored target via RGB thresholding
/// or a mask shader. The centroid is computed in viewport space and projected onto the floor (or
/// placed in 3D) to drive a tracking marker. Includes ROI search, prediction during misses, and
/// async GPU readback to reduce stalls.
/// </summary>
[DefaultExecutionOrder(50)]
[DisallowMultipleComponent]
[AddComponentMenu("Tracking/Color Ball Tracker")]
public class ColorBallTracker : MonoBehaviour
{
	[Header("Camera & Tracking Marker")]
	[Tooltip("Camera that renders the scene for tracking input (typically the top-down tracking camera).")]
	[SerializeField] public Camera sourceCamera;
	[Tooltip("Tracking marker transform that is positioned by this tracker.")]
	[SerializeField, FormerlySerializedAs("trackingBall")] public Transform trackingMarker;
	[Tooltip("Floor transform used when projecting the tracking marker onto the floor plane.")]
	[SerializeField] public Transform floor; // for projecting to plane when desired
	[Tooltip("Optional: real ball transform used to align Y when not projecting onto floor.")]
	[SerializeField] public Transform ball; // optional: real ball for Y alignment
	[Tooltip("If true, project the tracking marker onto the floor plane; otherwise place in 3D.")]
	[SerializeField, FormerlySerializedAs("placeTrackingOnFloor")] public bool placeTrackingMarkerOnFloor = true;
	[Tooltip("Restrict search to the viewport area that covers the floor.")]
	[SerializeField] public bool restrictToFloorViewport = true;
	[Tooltip("Clamp tracking marker position to the detected floor bounds.")]
	[SerializeField] public bool clampToFloorBounds = true;

	[Header("Mask Rendering")]
	[Tooltip("Optional mask shader (e.g., BallMask) to render the ball as white and background as black.")]
	[SerializeField] private Shader maskShader; // optional: render ball as white
	[Tooltip("Background color used when mask shader is active.")]
	[SerializeField] private Color backgroundColor = Color.black; // background for mask render
    [Tooltip("Channel and threshold used when interpreting mask output (0..255).")]
    [SerializeField] private MaskChannel maskInterpretation = MaskChannel.Blue;
    [SerializeField, Range(0,255)] private int maskThreshold = 127;

    private enum MaskChannel { Red, Green, Blue, Alpha }

	[Header("Robustness")]
	[Tooltip("Search in a small ROI around the previous detection first.")]
	[SerializeField] private bool searchAroundLast = true; // focus search around last seen position
	[Tooltip("Base radius of the search ROI in viewport units (0..1).")]
	[SerializeField] private float roiRadiusViewport = 0.18f; // radius of ROI window in viewport units (0..1)
	[Tooltip("Predict motion briefly when detection is lost.")]
	[SerializeField] private bool enablePrediction = true; // predict briefly when detection drops
	[Tooltip("Maximum frames to continue predicting when lost.")]
	[SerializeField] private int maxMissedFrames = 8;
	[Tooltip("Damping of the predicted viewport velocity (0..1).")]
	[SerializeField] private float velocityDamping = 0.85f;

	[Header("Recovery")]
	[Tooltip("After this many misses, ignore ROI and scan the full frame.")]
	[SerializeField] private int missesBeforeFullScan = 3; // after this many misses, ignore ROI and scan the full rect
	[Tooltip("ROI growth factor applied per miss while searching.")]
	[SerializeField] private float roiExpandFactorOnMiss = 1.75f; // expands ROI radius multiplicatively while missed
	[Tooltip("Padding added around the computed floor viewport when restricting (0..0.2 typical).")]
	[SerializeField] private float floorViewportPadding = 0.02f; // padding around floor viewport (0..0.2 typical)

	[Header("Sampling")]
	[Tooltip("Downscaled texture width for CPU sampling.")]
	[SerializeField] private int sampleWidth = 256;
	[Tooltip("Downscaled texture height for CPU sampling.")]
	[SerializeField] private int sampleHeight = 256;
	[Tooltip("Process every Nth frame to reduce cost.")]
	[SerializeField] private int sampleEveryNthFrame = 1;
	[Tooltip("0 = off; if > 0, throttles tracking by time instead of frame stride.")]
	[SerializeField] private float trackingFps = 0f; // 0 = disabled; >0 throttles sampling by time
	[Tooltip("Minimum number of matching pixels to accept a detection (RGB mode).")]
	[SerializeField] private int minPixelCount = 20;
	[Tooltip("Temporal smoothing of the detected centroid (0 = none).")]
	[SerializeField] private float smoothing = 0.5f; // 0=no smoothing, 1=full hold

	[Header("Target Color (RGB)")]
	[Tooltip("RGB color to detect when in RGB mode.")]
	[SerializeField] private Color targetColor = Color.blue;
	[Tooltip("RGB distance tolerance (0..1) when in RGB mode.")]
	[SerializeField, Range(0f, 1f)] private float colorTolerance = 0.15f; // Euclidean RGB distance

	[Header("Async Readback")]
	[Tooltip("Use non-blocking GPU→CPU copy via AsyncGPUReadback.")]
	[SerializeField] private bool useAsyncGpuReadback = true; // non-blocking GPU→CPU copy
	[Tooltip("Continue motion prediction while awaiting GPU readback (if enabled).")]
	[SerializeField] private bool predictWhileWaiting = true; // optionally predict while awaiting readback
	private bool readbackPending;

	private RenderTexture rt;
	private Texture2D tex;
	private int frameCount;
	private bool hasPrev;
	private Vector2 prevViewport;
	private Vector2 viewportVelocity;
	private int missedFrames;
	[SerializeField, FormerlySerializedAs("trackingHeightOffset")] private float trackingMarkerHeightOffset = 0.5f;
	private float nextSampleTime;

    // We keep the tracking camera rendering to its own RenderTexture (so UI previews work)
    // and downsample that into a small RT for CPU/GPU sampling.
    private bool replacementShaderSet;
    private bool backgroundOverridden;
    private CameraClearFlags originalClearFlags;
    private Color originalBackgroundColor;

	//private void Awake()
	//{
	//	if (sourceCamera == null) sourceCamera = Camera.main;
	//}

	private void OnEnable()
	{
        AllocateBuffers();
        nextSampleTime = 0f;
	}

	private void OnDisable()
	{
        // Restore replacement shader if we had set it
        try
        {
            if (sourceCamera != null && replacementShaderSet)
            {
                sourceCamera.ResetReplacementShader();
                replacementShaderSet = false;
                if (backgroundOverridden)
                {
                    sourceCamera.clearFlags = originalClearFlags;
                    sourceCamera.backgroundColor = originalBackgroundColor;
                    backgroundOverridden = false;
                }
            }
            // Release auto-created target texture if we created it here
            if (sourceCamera != null && sourceCamera.targetTexture != null && sourceCamera.targetTexture.name == "TrackingCamRT_Auto")
            {
                var toRelease = sourceCamera.targetTexture;
                sourceCamera.targetTexture = null;
                toRelease.Release();
                DestroyImmediate(toRelease);
            }
        }
        catch { }
		ReleaseBuffers();
	}

    /// <summary>
    /// Allocates or re-allocates the downsampled render and CPU texture.
    /// </summary>
    private void AllocateBuffers()
	{
		ReleaseBuffers();
        rt = new RenderTexture(sampleWidth, sampleHeight, 0, RenderTextureFormat.ARGB32);
		rt.filterMode = FilterMode.Point;
		tex = new Texture2D(sampleWidth, sampleHeight, TextureFormat.RGBA32, false);
	}

    /// <summary>
    /// Releases GPU/CPU sampling buffers.
    /// </summary>
    private void ReleaseBuffers()
	{
		if (rt != null)
		{
			if (RenderTexture.active == rt) RenderTexture.active = null;
			rt.Release();
			rt = null;
		}
		if (tex != null)
		{
			Object.Destroy(tex);
			tex = null;
		}
	}

    /// <summary>
    /// Main sampling loop: throttles work, handles async readback, and updates marker position.
    /// </summary>
    private void LateUpdate()
	{
		if (sourceCamera == null || trackingMarker == null) return;
		if (!isActiveAndEnabled) return;

        // Keep replacement shader setting in sync if it changes at runtime. We do NOT
        // change the camera's targetTexture; this ensures the HUD preview stays valid.
        if (maskShader != null && !replacementShaderSet)
        {
            sourceCamera.SetReplacementShader(maskShader, "");
            replacementShaderSet = true;
            // Ensure solid background while using mask shader
            originalClearFlags = sourceCamera.clearFlags;
            originalBackgroundColor = sourceCamera.backgroundColor;
            sourceCamera.clearFlags = CameraClearFlags.SolidColor;
            sourceCamera.backgroundColor = backgroundColor;
            backgroundOverridden = true;
        }
        else if (maskShader == null && replacementShaderSet)
        {
            sourceCamera.ResetReplacementShader();
            replacementShaderSet = false;
            // Restore camera background settings
            if (backgroundOverridden)
            {
                sourceCamera.clearFlags = originalClearFlags;
                sourceCamera.backgroundColor = originalBackgroundColor;
                backgroundOverridden = false;
            }
        }
		// Throttle sampling by target FPS if set; otherwise by frame stride
		if (trackingFps > 0f)
		{
			float now = Time.unscaledTime;
			if (now < nextSampleTime) return;
			nextSampleTime = now + (1f / Mathf.Max(0.001f, trackingFps));
		}
		else
		{
			if ((frameCount++ % sampleEveryNthFrame) != 0) return;
		}

		// Ensure GPU/CPU buffers match current sampling dimensions (can be changed at runtime)
		if (rt == null || tex == null || rt.width != sampleWidth || rt.height != sampleHeight || tex.width != sampleWidth || tex.height != sampleHeight)
		{
			AllocateBuffers();
		}

        // Ensure the tracking camera has a target texture (HUD may already set one)
        if (sourceCamera.targetTexture == null)
        {
            var camRt = new RenderTexture(Mathf.Max(1, sampleWidth), Mathf.Max(1, sampleHeight), 0, RenderTextureFormat.ARGB32);
            camRt.name = "TrackingCamRT_Auto";
            camRt.filterMode = FilterMode.Point;
            sourceCamera.targetTexture = camRt;
        }
        // Downsample camera output into our small RT for efficient analysis without forcing an extra Render call
        Graphics.Blit(sourceCamera.targetTexture, rt);

		// Async GPU readback path (non-blocking)
		if (useAsyncGpuReadback)
		{
			if (readbackPending)
			{
				// Optionally keep moving via prediction while awaiting GPU readback
				if (predictWhileWaiting && enablePrediction && hasPrev && missedFrames < maxMissedFrames)
				{
					missedFrames++;
					viewportVelocity *= Mathf.Clamp01(velocityDamping);
					Vector2 vPred = new Vector2(
						Mathf.Clamp01(prevViewport.x + viewportVelocity.x),
						Mathf.Clamp01(prevViewport.y + viewportVelocity.y)
					);
					prevViewport = vPred;
					if (placeTrackingMarkerOnFloor)
					{
						if (TryProjectViewportToFloor(vPred, out Vector3 worldOnFloorPred))
						{
							trackingMarker.position = worldOnFloorPred;
						}
					}
				}
				return;
			}

			// Compute ROI window (shared logic)
			int xMinA, yMinA, xMaxA, yMaxA;
			ComputeRoi(out xMinA, out yMinA, out xMaxA, out yMaxA);

			readbackPending = true;
			AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, request =>
			{
				readbackPending = false;
				if (request.hasError) { return; }
				var pixelsA = request.GetData<Color32>();
				ProcessPixelsAndUpdateTrackingMarker(pixelsA, xMinA, yMinA, xMaxA, yMaxA);
			});

			return;
		}

        // Synchronous ReadPixels fallback path
        RenderTexture.active = rt;
		tex.ReadPixels(new Rect(0, 0, sampleWidth, sampleHeight), 0, 0, false);
		tex.Apply(false);
		RenderTexture.active = null;

		// Determine sampling ROI (shared logic)
		int xMin, yMin, xMax, yMax;
		ComputeRoi(out xMin, out yMin, out xMax, out yMax);

		// Shared CPU path using helper
		NativeArray<Color32> pixels = tex.GetRawTextureData<Color32>();
		ProcessPixelsAndUpdateTrackingMarker(pixels, xMin, yMin, xMax, yMax);
	}

	private void ComputeRoi(out int xMin, out int yMin, out int xMax, out int yMax)
	{
		xMin = 0; yMin = 0; xMax = sampleWidth - 1; yMax = sampleHeight - 1;
		if (restrictToFloorViewport && floor != null)
		{
			GetFloorViewportRect(sourceCamera, floor, out Vector2 vmin, out Vector2 vmax);
			float pad = Mathf.Clamp01(floorViewportPadding);
			vmin.x = Mathf.Clamp01(vmin.x - pad);
			vmin.y = Mathf.Clamp01(vmin.y - pad);
			vmax.x = Mathf.Clamp01(vmax.x + pad);
			vmax.y = Mathf.Clamp01(vmax.y + pad);
			xMin = Mathf.Clamp(Mathf.FloorToInt(vmin.x * sampleWidth), 0, sampleWidth - 1);
			yMin = Mathf.Clamp(Mathf.FloorToInt(vmin.y * sampleHeight), 0, sampleHeight - 1);
			xMax = Mathf.Clamp(Mathf.CeilToInt(vmax.x * sampleWidth), 0, sampleWidth - 1);
			yMax = Mathf.Clamp(Mathf.CeilToInt(vmax.y * sampleHeight), 0, sampleHeight - 1);
		}
		if (searchAroundLast && hasPrev && missedFrames < missesBeforeFullScan)
		{
			int cx = Mathf.Clamp(Mathf.RoundToInt(prevViewport.x * sampleWidth), 0, sampleWidth - 1);
			int cy = Mathf.Clamp(Mathf.RoundToInt(prevViewport.y * sampleHeight), 0, sampleHeight - 1);
			float roiScale = Mathf.Pow(Mathf.Max(1.0f, roiExpandFactorOnMiss), Mathf.Clamp(missedFrames, 0, missesBeforeFullScan));
			int r = Mathf.RoundToInt(roiRadiusViewport * roiScale * Mathf.Min(sampleWidth, sampleHeight));
			int wxMin = Mathf.Max(0, cx - r);
			int wxMax = Mathf.Min(sampleWidth - 1, cx + r);
			int wyMin = Mathf.Max(0, cy - r);
			int wyMax = Mathf.Min(sampleHeight - 1, cy + r);
			xMin = Mathf.Max(xMin, wxMin);
			xMax = Mathf.Min(xMax, wxMax);
			yMin = Mathf.Max(yMin, wyMin);
			yMax = Mathf.Min(yMax, wyMax);
		}
	}

    /// <summary>
    /// Processes a pixel ROI and updates the tracking marker accordingly.
    /// </summary>
    private void ProcessPixelsAndUpdateTrackingMarker(NativeArray<Color32> pixels, int xMin, int yMin, int xMax, int yMax)
	{
		Vector2 sum = Vector2.zero;
		int count = 0;
		int hitMinX = sampleWidth, hitMaxX = -1, hitMinY = sampleHeight, hitMaxY = -1;
		for (int y = yMin; y <= yMax; y++)
		{
			int row = y * sampleWidth;
			for (int x = xMin; x <= xMax; x++)
			{
				var c32 = pixels[row + x];
				bool isBallPixel = maskShader != null ? MaskPasses(c32) : IsTargetRgb(new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f, 1f));
				if (isBallPixel)
				{
					sum += new Vector2(x, y);
					count++;
					if (x < hitMinX) hitMinX = x;
					if (x > hitMaxX) hitMaxX = x;
					if (y < hitMinY) hitMinY = y;
					if (y > hitMaxY) hitMaxY = y;
				}
			}
		}
		if (maskShader == null && count < minPixelCount)
		{
			Vector2 sum2 = Vector2.zero; int cnt2 = 0;
			for (int y = yMin; y <= yMax; y++)
			{
				int row = y * sampleWidth;
				for (int x = xMin; x <= xMax; x++)
				{
					var c32b = pixels[row + x];
					Color cb = new Color(c32b.r / 255f, c32b.g / 255f, c32b.b / 255f, 1f);
					bool okLoose = IsTargetRgbLoose(cb);
					if (okLoose) { sum2 += new Vector2(x, y); cnt2++; }
				}
			}
			if (cnt2 > 0) { sum = sum2; count = cnt2; }
		}
		bool touchesBorder = (count > 0) && (hitMinX <= xMin || hitMaxX >= xMax || hitMinY <= yMin || hitMaxY >= yMax);
		if (touchesBorder)
		{
			Vector2 centroidRoi = sum / Mathf.Max(1, count);
			int cx2 = Mathf.Clamp(Mathf.RoundToInt(centroidRoi.x), 0, sampleWidth - 1);
			int cy2 = Mathf.Clamp(Mathf.RoundToInt(centroidRoi.y), 0, sampleHeight - 1);
			int r2 = Mathf.RoundToInt(Mathf.Min(sampleWidth, sampleHeight) * Mathf.Max(roiRadiusViewport * 2.0f, 0.2f));
			int xxMin = Mathf.Max(0, cx2 - r2);
			int xxMax = Mathf.Min(sampleWidth - 1, cx2 + r2);
			int yyMin = Mathf.Max(0, cy2 - r2);
			int yyMax = Mathf.Min(sampleHeight - 1, cy2 + r2);
			Vector2 sum2 = Vector2.zero; int cnt2 = 0;
			for (int y = yyMin; y <= yyMax; y++)
			{
				int row = y * sampleWidth;
				for (int x = xxMin; x <= xxMax; x++)
				{
					var c = pixels[row + x];
					bool ok = maskShader != null ? MaskPasses(c) : IsTargetRgb(new Color(c.r / 255f, c.g / 255f, c.b / 255f, 1f));
					if (ok) { sum2 += new Vector2(x, y); cnt2++; }
				}
			}
			if (cnt2 > 0) { sum = sum2; count = cnt2; }
		}
		if (count == 0 && missedFrames >= missesBeforeFullScan)
		{
			Vector2 sumFall = Vector2.zero; int cntFall = 0;
			for (int y = 0; y < sampleHeight; y++)
			{
				int row = y * sampleWidth;
				for (int x = 0; x < sampleWidth; x++)
				{
					var cFull = pixels[row + x];
					bool ok = maskShader != null ? MaskPasses(cFull) : IsTargetRgbLoose(new Color(cFull.r / 255f, cFull.g / 255f, cFull.b / 255f, 1f));
					if (ok) { sumFall += new Vector2(x, y); cntFall++; }
				}
			}
			if (cntFall > 0) { sum = sumFall; count = cntFall; }
		}
		if (count > 0)
		{
			Vector2 centroid = sum / count;
			float vx = (centroid.x + 0.5f) / sampleWidth;
			float vy = (centroid.y + 0.5f) / sampleHeight;
			Vector2 v = new Vector2(vx, vy);
			if (hasPrev)
			{
				viewportVelocity = v - prevViewport;
				v = Vector2.Lerp(prevViewport, v, 1f - Mathf.Clamp01(smoothing));
			}
			prevViewport = v; hasPrev = true; missedFrames = 0;
		if (placeTrackingMarkerOnFloor)
			{
				if (TryProjectViewportToFloor(v, out Vector3 worldOnFloor))
				{
				trackingMarker.position = worldOnFloor;
				}
			}
			else
			{
				Vector3 world = ViewportWorldConsistent(new Vector3(v.x, v.y, sourceCamera.nearClipPlane + 1f));
			float y = trackingMarker.position.y;
				if (ball != null) y = ball.position.y;
			trackingMarker.position = new Vector3(world.x, y, world.z);
			}
		}
		else if (enablePrediction && hasPrev && missedFrames < maxMissedFrames)
		{
			missedFrames++;
			viewportVelocity *= Mathf.Clamp01(velocityDamping);
			Vector2 vPred = new Vector2(
				Mathf.Clamp01(prevViewport.x + viewportVelocity.x),
				Mathf.Clamp01(prevViewport.y + viewportVelocity.y)
			);
			prevViewport = vPred;
			if (placeTrackingMarkerOnFloor)
			{
				if (TryProjectViewportToFloor(vPred, out Vector3 worldOnFloorPred))
				{
					trackingMarker.position = worldOnFloorPred;
				}
			}
		}
	}

    /// <summary>
    /// Interprets a mask pixel according to the selected channel and threshold.
    /// </summary>
    private bool MaskPasses(Color32 c)
    {
        int thr = Mathf.Clamp(maskThreshold, 0, 255);
        switch (maskInterpretation)
        {
            case MaskChannel.Red:   return c.r > thr;
            case MaskChannel.Green: return c.g > thr;
            case MaskChannel.Blue:  return c.b > thr;
            case MaskChannel.Alpha: return c.a > thr;
            default: return c.b > thr;
        }
    }

    // Generate a ray using the tracking camera's current projection
    /// <summary>
    /// Generates a ray using the tracking camera's current projection.
    /// </summary>
    private Ray ViewportRayConsistent(Vector2 viewport)
    {
        return sourceCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
    }

    /// <summary>
    /// Converts viewport to world using the tracking camera's current projection.
    /// </summary>
    private Vector3 ViewportWorldConsistent(Vector3 viewport)
    {
        return sourceCamera.ViewportToWorldPoint(viewport);
    }

    /// <summary>
    /// Projects a viewport point onto the floor plane and optionally clamps to floor bounds.
    /// </summary>
    private bool TryProjectViewportToFloor(Vector2 viewport, out Vector3 world)
	{
		world = Vector3.zero;
		Ray ray = ViewportRayConsistent(viewport);
		Vector3 planeNormal = floor != null ? floor.up : Vector3.up;
		Vector3 planePoint = floor != null ? floor.position : Vector3.zero;
		Plane plane = new Plane(planeNormal, planePoint);
		if (plane.Raycast(ray, out float dist))
		{
			Vector3 hit = ray.GetPoint(dist);
			world = hit + planeNormal * trackingMarkerHeightOffset;
            if (clampToFloorBounds && BoundsUtils.TryGetWorldBounds(floor, out Bounds b))
			{
				float minX = b.min.x, maxX = b.max.x;
				float minZ = b.min.z, maxZ = b.max.z;
				world.x = Mathf.Clamp(world.x, minX, maxX);
				world.z = Mathf.Clamp(world.z, minZ, maxZ);
			}
			return true;
		}
		return false;
	}

    private bool IsTargetRgb(Color c)
	{
		return IsTargetRgbWithTolerance(c, colorTolerance);
	}

	private bool IsTargetRgbLoose(Color c)
	{
		return IsTargetRgbWithTolerance(c, colorTolerance * 2f);
	}

    /// <summary>
    /// Returns true if <paramref name="c"/> is within the RGB Euclidean distance tolerance.
    /// </summary>
    private bool IsTargetRgbWithTolerance(Color c, float tol)
	{
		float dr = c.r - targetColor.r;
		float dg = c.g - targetColor.g;
		float db = c.b - targetColor.b;
		float dist2 = dr * dr + dg * dg + db * db;
		float tol2 = tol * tol;
		return dist2 <= tol2;
	}

    /// <summary>
    /// Sets the RGB target color at runtime.
    /// </summary>
    public void SetTargetColor(Color color)
	{
		targetColor = color;
	}

    /// <summary>
    /// Sets the RGB distance tolerance (0..1) at runtime.
    /// </summary>
    public void SetColorTolerance(float tolerance01)
	{
		colorTolerance = Mathf.Clamp01(tolerance01);
	}

    /// <summary>
    /// Computes the floor's viewport rectangle from renderer bounds (or a scale-based fallback).
    /// </summary>
    private static void GetFloorViewportRect(Camera cam, Transform floor, out Vector2 min, out Vector2 max)
	{
		Vector3[] corners;
        if (BoundsUtils.TryGetWorldBounds(floor, out Bounds b))
		{
			corners = new Vector3[4]
			{
				new Vector3(b.min.x, b.center.y, b.min.z),
				new Vector3(b.min.x, b.center.y, b.max.z),
				new Vector3(b.max.x, b.center.y, b.min.z),
				new Vector3(b.max.x, b.center.y, b.max.z)
			};
		}
		else
		{
			Vector3 center = floor.position;
			float hx = 5f * Mathf.Abs(floor.localScale.x);
			float hz = 5f * Mathf.Abs(floor.localScale.z);
			corners = new Vector3[4]
			{
				center + new Vector3(-hx, 0f, -hz),
				center + new Vector3(-hx, 0f,  hz),
				center + new Vector3( hx, 0f, -hz),
				center + new Vector3( hx, 0f,  hz)
			};
		}
		Vector2 vmin = new Vector2(1f, 1f);
		Vector2 vmax = new Vector2(0f, 0f);
		for (int i = 0; i < corners.Length; i++)
		{
			Vector3 v = cam.WorldToViewportPoint(corners[i]);
			vmin = Vector2.Min(vmin, new Vector2(v.x, v.y));
			vmax = Vector2.Max(vmax, new Vector2(v.x, v.y));
		}
		min = vmin; max = vmax;
	}

    
}


