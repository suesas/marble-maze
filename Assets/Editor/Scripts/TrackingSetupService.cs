using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// Sets up visual tracking infrastructure (tracking marker, tracking camera, tracker, HUD) under a level root.
/// Scopes changes to the provided root and avoids global searches where possible.
/// </summary>
public static class TrackingSetupService
{
    public class TrackingOptions
    {
        public bool showTrackingFeedback = false;
        public bool showHud = true;
        public bool showRealHud = true;
        public bool showDisplay = true;
        public float trackingMarkerScaleMultiplier = 1.5f;
        public float trackingCamHeight = 10f;
        public float trackingCamPadding = 1.0f;

        // Tracker sampling & detection
        public int trkSampleWidth = 512;
        public int trkSampleHeight = 512;
        public int trkSampleEveryNth = 1;
        public float trkTargetTrackingFps = 0f; // 0 = disabled; >0 throttles sampling by time
        public int trkMinPixelCount = 10;
        public float trkSmoothing = 0.05f;
        public bool trkSearchAroundLast = true;
        public float trkRoiRadiusViewport = 0.35f;
        public bool trkEnablePrediction = true;
        public int trkMaxMissedFrames = 12;
        public float trkVelocityDamping = 0.85f;
        public int trkMissesBeforeFullScan = 3;
        public float trkRoiExpandFactorOnMiss = 1.75f;
        public float trkFloorViewportPadding = 0.02f;
        public float trkTrackingMarkerHeightOffset = 0.5f;
        public Color trkTargetColor = Color.blue;
        public float trkColorTolerance = 0.15f;
        public bool trkPlaceTrackingMarkerOnFloor = true;
        public bool trkRestrictToFloorViewport = false;
        public bool trkClampToFloorBounds = true;

        // Materials
        public Material trackingMarkerMaterial;

        // Mode
        public bool useShaderMask = false; // if true, assign BallMask.shader; otherwise RGB thresholds
        // Editor: optionally create and save a transparent debug material asset (off by default)
        public bool createDebugMaterialAsset = false;
    }

    private const string LABEL_FLOOR = "Floor";

    /// <summary>
    /// Creates or wires up tracking components under <paramref name="levelRoot"/> and configures them from <paramref name="options"/>.
    /// </summary>
    public static void Setup(Transform levelRoot, GameObject marbleGO, TrackingOptions options)
    {
        if (levelRoot == null) return;

        // Floor reference
        Transform floorT = levelRoot.Find(LABEL_FLOOR);
        if (floorT == null)
        {
            var floorGo = GameObject.Find(LABEL_FLOOR);
            if (floorGo != null) floorT = floorGo.transform;
        }

        // Ensure Ball layer exists and assign to real ball
        int ballLayer = EnsureLayerExistsEditor("Ball");
        if (marbleGO != null && marbleGO.layer != ballLayer) marbleGO.layer = ballLayer;

        bool needsTrackingSystem = options.showTrackingFeedback || options.showHud || options.showDisplay;

        // Create or get TrackingMarker only if the tracking subsystem is needed
        Transform trackingMarkerT = null;
        if (needsTrackingSystem)
        {
            trackingMarkerT = levelRoot.Find("TrackingMarker");
            if (trackingMarkerT == null)
            {
                var legacySpaced = levelRoot.Find("Tracking Marker");
                if (legacySpaced != null)
                {
                    legacySpaced.name = "TrackingMarker";
                    trackingMarkerT = legacySpaced;
                }
            }
            if (trackingMarkerT == null)
            {
                var legacyBall = levelRoot.Find("TrackingBall");
                if (legacyBall != null)
                {
                    legacyBall.name = "TrackingMarker";
                    trackingMarkerT = legacyBall;
                }
            }
            if (trackingMarkerT == null)
            {
                var trackingMarkerGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                trackingMarkerGo.name = "TrackingMarker";
                trackingMarkerGo.transform.SetParent(levelRoot, false);
                trackingMarkerGo.transform.position = marbleGO != null ? marbleGO.transform.position : levelRoot.position;
                // Enlarge tracking marker for visibility compared to the real ball (configurable)
                Vector3 srcScale = marbleGO != null ? marbleGO.transform.localScale : Vector3.one;
                trackingMarkerGo.transform.localScale = srcScale * Mathf.Max(0.1f, options.trackingMarkerScaleMultiplier);
                var col = trackingMarkerGo.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                // Assign material if provided; else fallback to BlueTransparent or Standard
                Material matToUse = options.trackingMarkerMaterial;
                if (matToUse == null)
                {
                    if (options.createDebugMaterialAsset)
                    {
                        var blueMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlueTransparent.mat");
                        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                        {
                            AssetDatabase.CreateFolder("Assets", "Materials");
                        }
                        if (blueMat == null)
                        {
                            blueMat = new Material(Shader.Find("Standard"));
                            blueMat.name = "BlueTransparent";
                            blueMat.color = new Color(0f, 0.4f, 1f, 0.35f);
                            if (blueMat.shader != null && !blueMat.shader.name.Contains("Universal Render Pipeline"))
                            {
                                blueMat.SetFloat("_Mode", 3f);
                                blueMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                blueMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                blueMat.SetInt("_ZWrite", 0);
                                blueMat.DisableKeyword("_ALPHATEST_ON");
                                blueMat.EnableKeyword("_ALPHABLEND_ON");
                                blueMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                                blueMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                            }
                            AssetDatabase.CreateAsset(blueMat, "Assets/Materials/BlueTransparent.mat");
                            AssetDatabase.SaveAssets();
                        }
                        matToUse = blueMat;
                    }
                    else
                    {
                        var tmp = new Material(Shader.Find("Standard"));
                        tmp.name = "BlueTransparent_Runtime";
                        tmp.color = new Color(0f, 0.4f, 1f, 0.35f);
                        if (tmp.shader != null && !tmp.shader.name.Contains("Universal Render Pipeline"))
                        {
                            tmp.SetFloat("_Mode", 3f);
                            tmp.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                            tmp.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                            tmp.SetInt("_ZWrite", 0);
                            tmp.DisableKeyword("_ALPHATEST_ON");
                            tmp.EnableKeyword("_ALPHABLEND_ON");
                            tmp.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                            tmp.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        }
                        matToUse = tmp;
                    }
                }
                var rend = trackingMarkerGo.GetComponent<Renderer>();
                if (rend != null && matToUse != null) rend.sharedMaterial = matToUse;
                trackingMarkerT = trackingMarkerGo.transform;
            }
            else
            {
                // Ensure tracking marker is larger than the real ball when reusing existing object
                Vector3 srcScale = marbleGO != null ? marbleGO.transform.localScale : trackingMarkerT.localScale;
                trackingMarkerT.localScale = srcScale * Mathf.Max(0.1f, options.trackingMarkerScaleMultiplier);
                if (options.trackingMarkerMaterial != null)
                {
                    var rend = trackingMarkerT.GetComponent<Renderer>();
                    if (rend != null) rend.sharedMaterial = options.trackingMarkerMaterial;
                }
            }
        }

        Camera trackingCam = null;
        // Try to find an existing tracking camera regardless of tracking enable state
        {
            Transform trackingCamTExisting = levelRoot.Find("TrackingCamera");
            if (trackingCamTExisting == null) trackingCamTExisting = levelRoot.Find("TrackingCam");
            if (trackingCamTExisting != null)
            {
                var existing = trackingCamTExisting.GetComponent<Camera>();
                if (existing != null) trackingCam = existing;
            }
        }
        if (needsTrackingSystem)
        {
            // Create or get tracking camera (scoped under level root)
            Transform trackingCamT = levelRoot.Find("TrackingCamera");
            if (trackingCamT == null)
            {
                // Backwards compatibility with older scenes (previous name)
                trackingCamT = levelRoot.Find("TrackingCam");
            }
            GameObject trackingCamGo = trackingCamT != null ? trackingCamT.gameObject : null;
            if (trackingCamGo == null)
            {
                trackingCamGo = new GameObject("TrackingCamera");
                trackingCamGo.transform.SetParent(levelRoot, false);
            }
            trackingCam = trackingCam != null ? trackingCam : trackingCamGo.GetComponent<Camera>();
            if (trackingCam == null) trackingCam = trackingCamGo.AddComponent<Camera>();
            trackingCam.orthographic = true;
            trackingCam.clearFlags = CameraClearFlags.SolidColor;
            trackingCam.backgroundColor = Color.black;
            trackingCam.allowHDR = false;
            trackingCam.allowMSAA = false;
            trackingCam.useOcclusionCulling = false;
            trackingCam.rect = new Rect(0f, 0f, 1f, 1f);
            if (trackingCam.targetTexture == null)
            {
                var rt = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
                rt.name = "TrackingCameraRT";
                rt.filterMode = FilterMode.Point;
                trackingCam.targetTexture = rt;
            }
        }

        // Determine game area from level renderers (Floor + Walls) and size camera accordingly
        var renderers = levelRoot.GetComponentsInChildren<Renderer>();
        if (needsTrackingSystem && renderers != null && renderers.Length > 0 && trackingCam != null)
        {
            var bounds = BoundsUtils.CalculateCombinedBounds(renderers);
            float padding = 0.5f;
            float aspect = 1f;
            if (trackingCam.targetTexture != null) aspect = Mathf.Max(0.01f, (float)trackingCam.targetTexture.width / Mathf.Max(1, trackingCam.targetTexture.height));
            else if (trackingCam.aspect > 0.01f) aspect = trackingCam.aspect;
            float halfWidth = bounds.extents.x;
            float halfHeight = bounds.extents.z;
            float orthoByHeight = halfHeight + padding;
            float orthoByWidth = (halfWidth / Mathf.Max(0.01f, aspect)) + padding;
            trackingCam.orthographicSize = Mathf.Max(orthoByHeight, orthoByWidth);

            Vector3 up = floorT != null ? floorT.up : Vector3.up;
            Vector3 lookUp = floorT != null ? floorT.forward : Vector3.forward;
            Vector3 center = bounds.center;
            float camHeight = Mathf.Max(10f, bounds.extents.magnitude);
            trackingCam.transform.position = center + up * camHeight;
            trackingCam.transform.rotation = Quaternion.LookRotation(-up, lookUp);
        }
        if (needsTrackingSystem)
        {
            var boundsForCams = (renderers != null && renderers.Length > 0) ? BoundsUtils.CalculateCombinedBounds(renderers) : new Bounds(levelRoot.position, Vector3.one * 10f);
            var mainCam = EnsureAndSetupMainCamera(boundsForCams, floorT);
            if (mainCam != null && trackingCam != null) trackingCam.depth = mainCam.depth - 1f;
        }

        // Restrict to Ball layer if available
        int ballLayerIdx = LayerMask.NameToLayer("Ball");
        if (ballLayerIdx != -1 && trackingCam != null)
        {
            trackingCam.cullingMask = (1 << ballLayerIdx);
        }

        // Attach TrackingCameraFollower to keep alignment with floor tilt
        if (needsTrackingSystem && trackingCam != null)
        {
            var follower = trackingCam.GetComponent<TrackingCameraFollower>();
            if (follower == null) follower = trackingCam.gameObject.AddComponent<TrackingCameraFollower>();
            follower.floor = floorT;
            follower.trackingCam = trackingCam;
            // Apply UI-configurable height/padding
            {
                var so = new SerializedObject(follower);
                var heightProp = so.FindProperty("height");
                var paddingProp = so.FindProperty("padding");
                if (heightProp != null) heightProp.floatValue = options.trackingCamHeight;
                if (paddingProp != null) paddingProp.floatValue = options.trackingCamPadding;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // Create or get ColorBallTracker (scoped under level root) and configure
        ColorBallTracker colorTracker = null;
        if (needsTrackingSystem)
        {
            Transform trackerT = levelRoot.Find("ColorTracker");
            var trackerGo = trackerT != null ? trackerT.gameObject : new GameObject("ColorTracker");
            if (trackerT == null) trackerGo.transform.SetParent(levelRoot, false);
            colorTracker = trackerGo.GetComponent<ColorBallTracker>();
            if (colorTracker == null) colorTracker = trackerGo.AddComponent<ColorBallTracker>();
            colorTracker.sourceCamera = trackingCam;
            colorTracker.trackingMarker = trackingMarkerT;
            colorTracker.floor = floorT;
            colorTracker.placeTrackingMarkerOnFloor = options.trkPlaceTrackingMarkerOnFloor;
            if (marbleGO != null)
            {
                var soAssign = new SerializedObject(colorTracker);
                var ballProp = soAssign.FindProperty("ball");
                if (ballProp != null) ballProp.objectReferenceValue = marbleGO.transform;
                soAssign.ApplyModifiedPropertiesWithoutUndo();
            }
        // Push tracker UI settings
        {
            var so = new SerializedObject(colorTracker);
            SetIfExists(so, "restrictToFloorViewport", options.trkRestrictToFloorViewport);
            SetIfExists(so, "clampToFloorBounds", options.trkClampToFloorBounds);
            SetIfExists(so, "sampleWidth", options.trkSampleWidth);
            SetIfExists(so, "sampleHeight", options.trkSampleHeight);
            SetIfExists(so, "sampleEveryNthFrame", options.trkSampleEveryNth);
            SetIfExists(so, "trackingFps", options.trkTargetTrackingFps);
            SetIfExists(so, "smoothing", options.trkSmoothing);
            SetIfExists(so, "searchAroundLast", options.trkSearchAroundLast);
            SetIfExists(so, "roiRadiusViewport", options.trkRoiRadiusViewport);
            SetIfExists(so, "enablePrediction", options.trkEnablePrediction);
            SetIfExists(so, "maxMissedFrames", options.trkMaxMissedFrames);
            SetIfExists(so, "velocityDamping", options.trkVelocityDamping);
            SetIfExists(so, "missesBeforeFullScan", options.trkMissesBeforeFullScan);
            SetIfExists(so, "roiExpandFactorOnMiss", options.trkRoiExpandFactorOnMiss);
            SetIfExists(so, "floorViewportPadding", options.trkFloorViewportPadding);
            SetIfExists(so, "trackingMarkerHeightOffset", options.trkTrackingMarkerHeightOffset);
            if (!options.useShaderMask)
            {
                SetIfExists(so, "minPixelCount", options.trkMinPixelCount);
                var tcProp = so.FindProperty("targetColor");
                if (tcProp != null) tcProp.colorValue = options.trkTargetColor;
                SetIfExists(so, "colorTolerance", options.trkColorTolerance);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Apply tracking mode: RGB clears mask shader; Shader assigns BallMask.shader
        if (needsTrackingSystem)
        {
            Shader maskShader = null;
            if (options.useShaderMask)
            {
                maskShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/BallMask.shader");
            }
            var soMask = new SerializedObject(colorTracker);
            var maskProp = soMask.FindProperty("maskShader");
            if (maskProp != null) maskProp.objectReferenceValue = maskShader;
            soMask.ApplyModifiedPropertiesWithoutUndo();
        }

        }

        Text textTracking = null;
        Text textReal = null;
        RawImage display = null;
        if (options.showHud || options.showRealHud || options.showDisplay)
        {
            // HUD Canvas and TrackingHUD (scoped under level root)
            Transform canvasT = levelRoot.Find("HUDCanvas");
            var canvasGo = canvasT != null ? canvasT.gameObject : null;
            Canvas canvas = canvasGo != null ? canvasGo.GetComponent<Canvas>() : null;
            if (canvas == null)
            {
                canvasGo = new GameObject("HUDCanvas");
                canvasGo.transform.SetParent(levelRoot, false);
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = short.MaxValue;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0f;
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            // Show tracking HUD only if explicitly requested
            if (options.showHud)
            {
                // Create/find Tracking HUD text (support "HUDText" as fallback)
                Transform textTrackT = canvas.transform.Find("HUDText_Tracking")
                    ?? canvas.transform.Find("HUDText");
                var textTrackGo = textTrackT != null ? textTrackT.gameObject : null;
                textTracking = textTrackGo != null ? textTrackGo.GetComponent<Text>() : null;
                if (textTracking == null)
                {
                    textTrackGo = new GameObject("HUDText_Tracking");
                    textTrackGo.transform.SetParent(canvas.transform, false);
                    textTracking = textTrackGo.AddComponent<Text>();
                    AssignDefaultFont(textTracking);
                    textTracking.fontSize = 28;
                    textTracking.alignment = TextAnchor.UpperLeft;
                    // Make tracking HUD white for maximum readability
                    textTracking.color = Color.white;
                    textTracking.supportRichText = true;
                    textTracking.raycastTarget = false;
                    var rtTrack = textTracking.GetComponent<RectTransform>();
                    rtTrack.anchorMin = new Vector2(0f, 1f);
                    rtTrack.anchorMax = new Vector2(0f, 1f);
                    rtTrack.pivot = new Vector2(0f, 1f);
                    rtTrack.anchoredPosition = new Vector2(12f, -(12f + 160f + 8f));
                    rtTrack.sizeDelta = new Vector2(600f, 160f);
                    textTracking.text = "<b>Tracked</b>";
                }
                else
                {
                    // Ensure tracking HUD stays white
                    textTracking.color = Color.white;
                }
				// Match styling of the Real HUD: add Outline and Shadow for readability
				EnsureOutlineAndShadow(textTracking);
            }

            // Create/find Real Instance HUD text positioned below tracking HUD text
            if (options.showRealHud)
            {
                Transform textRealT = canvas.transform.Find("HUDText_Real");
                var textRealGo = textRealT != null ? textRealT.gameObject : null;
                textReal = textRealGo != null ? textRealGo.GetComponent<Text>() : null;
                if (textReal == null)
                {
                    textRealGo = new GameObject("HUDText_Real");
                    textRealGo.transform.SetParent(canvas.transform, false);
                    textReal = textRealGo.AddComponent<Text>();
                    AssignDefaultFont(textReal);
                    textReal.fontSize = 28;
                    textReal.alignment = TextAnchor.UpperLeft;
                    textReal.color = Color.white;
                    textReal.supportRichText = true;
                    textReal.raycastTarget = false;
                    var rtReal = textReal.GetComponent<RectTransform>();
                    rtReal.anchorMin = new Vector2(0f, 1f);
                    rtReal.anchorMax = new Vector2(0f, 1f);
                    rtReal.pivot = new Vector2(0f, 1f);
                    // Position Real HUD at the top-left (swap positions with Tracking HUD)
                    rtReal.anchoredPosition = new Vector2(12f, -12f);
                    rtReal.sizeDelta = new Vector2(600f, 160f);
                    textReal.text = "<b>Real</b>";
                }
                else
                {
                    // Keep real HUD text white
                    textReal.color = Color.white;
                }
                EnsureOutlineAndShadow(textReal);
            }
            // Episode HUD: couple to Real HUD toggle and only when an Agent exists (Enable Agent)
            if (options.showRealHud && levelRoot.GetComponent<BoardAgent>() != null)
            {
                Transform textEpisodeT = canvas.transform.Find("HUDText_Episode");
                Text textEpisode = textEpisodeT != null ? textEpisodeT.GetComponent<Text>() : null;
                if (textEpisode == null)
                {
                    var textEpisodeGo = new GameObject("HUDText_Episode");
                    textEpisodeGo.transform.SetParent(canvas.transform, false);
                    textEpisode = textEpisodeGo.AddComponent<Text>();
                    AssignDefaultFont(textEpisode);
                    textEpisode.fontSize = 28;
                    textEpisode.alignment = TextAnchor.UpperRight;
                    textEpisode.color = Color.white;
                    textEpisode.supportRichText = true;
                    textEpisode.raycastTarget = false;
                    var rtEp = textEpisode.GetComponent<RectTransform>();
                    rtEp.anchorMin = new Vector2(1f, 1f);
                    rtEp.anchorMax = new Vector2(1f, 1f);
                    rtEp.pivot = new Vector2(1f, 1f);
                    rtEp.anchoredPosition = new Vector2(-12f, -12f);
                    rtEp.sizeDelta = new Vector2(520f, 200f);
                    textEpisode.text = "<b>Episode</b>";
                }
                EnsureOutlineAndShadow(textEpisode);
                // Attach EpisodeHUD behaviour under level root
                Transform epHudT = levelRoot.Find("EpisodeHUD");
                var epHudGo = epHudT != null ? epHudT.gameObject : new GameObject("EpisodeHUD");
                if (epHudT == null) epHudGo.transform.SetParent(levelRoot, false);
                var epHud = epHudGo.GetComponent<EpisodeHUD>();
                if (epHud == null) epHud = epHudGo.AddComponent<EpisodeHUD>();
                epHud.label = textEpisode;
                epHud.title = "Episode";
                // Wire references if present
                var agent = levelRoot.GetComponent<BoardAgent>();
                if (agent != null) epHud.agent = agent;
                var tilt = Object.FindObjectOfType<TiltController>();
                if (tilt != null) epHud.tiltController = tilt;
            }
            if (options.showDisplay && trackingCam != null)
            {
                // Create or get a small RawImage to display the tracking camera output
                Transform displayT = canvas.transform.Find("TrackingCameraDisplay");
                if (displayT == null)
                {
                    // legacy name support
                    displayT = canvas.transform.Find("TrackingCamDisplay");
                }
                var displayGo = displayT != null ? displayT.gameObject : null;
                display = displayGo != null ? displayGo.GetComponent<RawImage>() : null;
                if (display == null)
                {
                    displayGo = new GameObject("TrackingCameraDisplay");
                    displayGo.transform.SetParent(canvas.transform, false);
                    display = displayGo.AddComponent<RawImage>();
                    // No AspectRatioFitter: we keep a fixed width and compute height from aspect
                }
                if (display != null)
                {
                    // Position the display underneath the HUD text in the top-left corner
                    var rtDisp = display.GetComponent<RectTransform>();
                    rtDisp.anchorMin = new Vector2(0f, 1f); // top-left
                    rtDisp.anchorMax = new Vector2(0f, 1f);
                    rtDisp.pivot = new Vector2(0f, 1f);
                    // Place below both HUD texts (each 160 high) with gaps
                    rtDisp.anchoredPosition = new Vector2(12f, -(12f + 160f + 8f + 160f + 8f));
                    // Compute a compact size based on camera aspect with a fixed width
                    const float previewWidth = 330f; // px; tweak here for size
                    // Assign the tracking camera's target texture so it renders into the UI
                    if (trackingCam != null && trackingCam.targetTexture != null)
                    {
                        display.texture = trackingCam.targetTexture;
                        float w = Mathf.Max(1, trackingCam.targetTexture.width);
                        float h = Mathf.Max(1, trackingCam.targetTexture.height);
                        float aspect = w / h;
                        rtDisp.sizeDelta = new Vector2(previewWidth, Mathf.Max(40f, previewWidth / Mathf.Max(0.01f, aspect)));
                    }
                    else
                    {
                        rtDisp.sizeDelta = new Vector2(previewWidth, previewWidth);
                    }

                    // Create or update a label above the display: "Tracking Camera"
                    Transform labelT = canvas.transform.Find("TrackingCameraLabel");
                    Text label = labelT != null ? labelT.GetComponent<Text>() : null;
                    if (label == null)
                    {
                        var labelGo = new GameObject("TrackingCameraLabel");
                        labelGo.transform.SetParent(canvas.transform, false);
                        label = labelGo.AddComponent<Text>();
                        AssignDefaultFont(label);
                        label.supportRichText = true;
                        label.alignment = TextAnchor.UpperLeft;
                        label.raycastTarget = false;
                        // Match styling of the "Tracked" header
                        label.fontSize = 28;
                        label.text = "<b>Tracking Camera</b>";
                        var rtLabel = label.GetComponent<RectTransform>();
                        rtLabel.anchorMin = new Vector2(0f, 1f);
                        rtLabel.anchorMax = new Vector2(0f, 1f);
                        rtLabel.pivot = new Vector2(0f, 1f);
                        // Keep single-line headline; width equals preview width
                        rtLabel.sizeDelta = new Vector2(previewWidth, 36f);
                    }
                    // Make preview header white to match the Tracked HUD
                    label.color = Color.white;
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    // Use subtle shadow only (no outline) for a clean, readable look
                    EnsureShadowOnly(label);
                    // Position label just above the display using its anchored position
                    var rtExistingLabel = label.GetComponent<RectTransform>();
                    const float labelGap = 12f; // extra padding between label and preview
                    const float labelHeight = 36f; // matches sizeDelta above
                    rtExistingLabel.anchoredPosition = rtDisp.anchoredPosition + new Vector2(0f, labelHeight + labelGap);
                }
            }
            Transform hudT = levelRoot.Find("TrackingHUD");
            var hudGo = hudT != null ? hudT.gameObject : new GameObject("TrackingHUD");
            if (hudT == null) hudGo.transform.SetParent(levelRoot, false);
            if (options.showHud)
            {
                var hud = hudGo.GetComponent<TrackingHUD>();
                if (hud == null) hud = hudGo.AddComponent<TrackingHUD>();
                hud.target = trackingMarkerT;
                hud.label = textTracking;
                hud.title = "Tracked";
                hud.showHeader = true;
            }

            // Real instance HUD: mirror of TrackingHUD but bound to the real ball
            Transform hudRealT = levelRoot.Find("RealHUD");
            var hudRealGo = hudRealT != null ? hudRealT.gameObject : new GameObject("RealHUD");
            if (hudRealT == null) hudRealGo.transform.SetParent(levelRoot, false);
            if (options.showRealHud)
            {
                var hudReal = hudRealGo.GetComponent<TrackingHUD>();
                if (hudReal == null) hudReal = hudRealGo.AddComponent<TrackingHUD>();
                if (marbleGO != null) hudReal.target = marbleGO.transform;
                hudReal.label = textReal;
                hudReal.title = "Real";
                hudReal.showHeader = true;
            }
        }

        // Enable/disable tracking feedback objects as requested
        if (trackingMarkerT != null) trackingMarkerT.gameObject.SetActive(options.showTrackingFeedback);
        var ctBehaviour = colorTracker as Behaviour;
        if (ctBehaviour != null) ctBehaviour.enabled = options.showTrackingFeedback;
        if (trackingCam != null) trackingCam.enabled = (options.showTrackingFeedback || options.showDisplay);
        // Colors already assigned above per label
        if (display != null) display.gameObject.SetActive(options.showDisplay);
    }

    

    public static int EnsureLayerExistsEditor(string layerName)
    {
        int idx = LayerMask.NameToLayer(layerName);
        if (idx != -1) return idx;
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return 0;
        var so = new SerializedObject(assets[0]);
        var layersProp = so.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            var sp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(sp.stringValue))
            {
                sp.stringValue = layerName;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }
        }
        return 0;
    }

    private static Camera EnsureAndSetupMainCamera(Bounds areaBounds, Transform floorT)
    {
        Camera main = Camera.main;
        if (main == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("MainCamera");
            if (tagged != null) main = tagged.GetComponent<Camera>();
        }
        if (main == null)
        {
            return null;
        }

        float aspect = main.aspect > 0.01f ? main.aspect : (16f / 9f);
        float padding = 0.5f;
        float halfW = Mathf.Max(0.001f, areaBounds.extents.x);
        float halfH = Mathf.Max(0.001f, areaBounds.extents.z);
        float orthoByH = halfH + padding;
        float orthoByW = (halfW / aspect) + padding;

        Vector3 up = floorT != null ? floorT.up : Vector3.up;
        Vector3 lookUp = floorT != null ? floorT.forward : Vector3.forward;
        float camHeight = Mathf.Max(10f, areaBounds.extents.magnitude);

        main.orthographic = true;
        main.clearFlags = CameraClearFlags.Skybox;
        main.useOcclusionCulling = false;
        main.allowHDR = false;
        main.allowMSAA = false;
        main.rect = new Rect(0f, 0f, 1f, 1f);
        main.nearClipPlane = 0.1f;
        main.farClipPlane = 100f;
        main.cullingMask = ~0;
        main.orthographicSize = Mathf.Max(orthoByH, orthoByW);
        main.transform.position = areaBounds.center + up * camHeight;
        main.transform.rotation = Quaternion.LookRotation(-up, lookUp);

        return main;
    }

    private static void SetIfExists(SerializedObject so, string propertyName, float value)
    {
        var p = so.FindProperty(propertyName);
        if (p != null) p.floatValue = value;
    }

    private static void SetIfExists(SerializedObject so, string propertyName, int value)
    {
        var p = so.FindProperty(propertyName);
        if (p != null) p.intValue = value;
    }

    private static void SetIfExists(SerializedObject so, string propertyName, bool value)
    {
        var p = so.FindProperty(propertyName);
        if (p != null) p.boolValue = value;
    }

    // UI helpers: keep font lookup and effects consistent across HUD elements
    private static void AssignDefaultFont(Text text)
    {
        if (text == null) return;
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (f != null) text.font = f;
    }

    private static void EnsureOutlineAndShadow(Text text)
    {
        if (text == null) return;
        var outline = text.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        var shadow = text.GetComponent<UnityEngine.UI.Shadow>();
        if (shadow == null) shadow = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    private static void EnsureShadowOnly(Text text)
    {
        if (text == null) return;
        var existingOl = text.GetComponent<UnityEngine.UI.Outline>();
        if (existingOl != null) Object.DestroyImmediate(existingOl);
        var sh = text.GetComponent<UnityEngine.UI.Shadow>();
        if (sh == null) sh = text.gameObject.AddComponent<UnityEngine.UI.Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
        sh.effectDistance = new Vector2(2f, -2f);
    }
}

