using UnityEngine;
using UnityEditor;
using Unity.MLAgents.Policies;
using System.IO;
using Unity.Barracuda;

public partial class SVGLevelImporter : EditorWindow
{
    private static GUIContent T(string label, string tip) { return new GUIContent(label, tip); }

    // Apply sensible defaults once per editor session so the Materials tab is pre-populated
    private static bool s_appliedDefaults;
    // Track last toggle state to react to changes and set default materials
    private bool prevEnableTracking;

    /// <summary>
    /// Finds an asset of type T by its file name (without extension), case-insensitive.
    /// </summary>
    private static TAsset FindAssetByExactName<TAsset>(string exactName) where TAsset : Object
    {
        if (string.IsNullOrEmpty(exactName)) return null;
        string typeName = typeof(TAsset).Name;
        var guids = AssetDatabase.FindAssets($"t:{typeName} {exactName}");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.Equals(Path.GetFileNameWithoutExtension(path), exactName, System.StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<TAsset>(path);
            }
        }
        return null;
    }

    private static void TryAssignMaterialByName(ref Material field, string name)
    {
        if (field != null) return;
        field = FindAssetByExactName<Material>(name);
    }

    private static void TryAssignMaterialByNames(ref Material field, params string[] names)
    {
        if (field != null || names == null) return;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            var m = FindAssetByExactName<Material>(names[i]);
            if (m != null)
            {
                field = m;
                return;
            }
        }
    }

    private static void TryAssignPhysicByName(ref PhysicMaterial field, string name)
    {
        if (field != null) return;
        field = FindAssetByExactName<PhysicMaterial>(name);
    }

    private static void TryAssignPhysicByNames(ref PhysicMaterial field, params string[] names)
    {
        if (field != null || names == null) return;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            var pm = FindAssetByExactName<PhysicMaterial>(names[i]);
            if (pm != null)
            {
                field = pm;
                return;
            }
        }
    }

    /// <summary>
    /// Assigns common materials by name if fields are unset.
    /// </summary>
    private void EnsureDefaultMaterialsAssigned()
    {
        // Materials (search by multiple likely names to tolerate folder/name variations)
        TryAssignMaterialByNames(ref floorMaterial, SVGImporterDefaults.Names.Floor);
        TryAssignMaterialByNames(ref wallMaterial, SVGImporterDefaults.Names.Wall);
        TryAssignMaterialByNames(ref frameMaterial, SVGImporterDefaults.Names.Frame);
        TryAssignMaterialByNames(ref hingeMaterial, SVGImporterDefaults.Names.Hinge);
        TryAssignMaterialByNames(ref startMarkerMaterial, SVGImporterDefaults.Names.Start);
        TryAssignMaterialByNames(ref endMarkerMaterial, SVGImporterDefaults.Names.Goal);
        TryAssignMaterialByNames(ref marbleMarkerMaterial, SVGImporterDefaults.Names.Marble);
        TryAssignMaterialByNames(ref trackingMarkerMaterial, SVGImporterDefaults.Names.TrackingMarker);
        TryAssignMaterialByNames(ref holeTriggerMaterial, SVGImporterDefaults.Names.Hole);
        TryAssignMaterialByNames(ref endTriggerMaterial, SVGImporterDefaults.Names.Goal);
        TryAssignMaterialByNames(ref meshHelperMaterial, SVGImporterDefaults.Names.Hole);

        // Physic materials
        TryAssignPhysicByName(ref marblePhysMaterial, SVGImporterDefaults.Names.PhysMarble);
    }

    /// <summary>
    /// Assigns common physic materials by name if fields are unset.
    /// </summary>
    private void EnsureDefaultPhysicMaterialsAssigned()
    {
        TryAssignPhysicByNames(ref floorPhysMaterial, SVGImporterDefaults.Names.PhysFloor);
        TryAssignPhysicByNames(ref wallPhysMaterial, SVGImporterDefaults.Names.PhysWall);
        TryAssignPhysicByNames(ref marblePhysMaterial, SVGImporterDefaults.Names.PhysMarbleArray);
        TryAssignPhysicByNames(ref framePhysMaterial, SVGImporterDefaults.Names.PhysFrame);
    }

    // Determine if any core fields that should have sensible defaults are still unassigned
    /// <summary>
    /// Returns true when any essential visual/physics materials are missing.
    /// </summary>
    private bool AreCoreMaterialFieldsMissing()
    {
        return floorMaterial == null
            || wallMaterial == null
            || frameMaterial == null
            || startMarkerMaterial == null
            || endMarkerMaterial == null
            || marbleMarkerMaterial == null
            || floorPhysMaterial == null
            || wallPhysMaterial == null
            || marblePhysMaterial == null;
    }

    private void ClearAllMaterialFields()
    {
        floorMaterial = null;
        wallMaterial = null;
        frameMaterial = null;
        hingeMaterial = null;
        startMarkerMaterial = null;
        endMarkerMaterial = null;
        marbleMarkerMaterial = null;
        trackingMarkerMaterial = null;
        holeTriggerMaterial = null;
        endTriggerMaterial = null;
        meshHelperMaterial = null;
        floorPhysMaterial = null;
        wallPhysMaterial = null;
        marblePhysMaterial = null;
        framePhysMaterial = null;
    }

    /// <summary>
    /// Fallback material search using explicit names; tolerates missing assets.
    /// </summary>
    private void RestoreDefaultMaterials()
    {
        // Visual materials
        floorMaterial = FindAssetByExactName<Material>("Floor")
            ?? FindAssetByExactName<Material>("Mat_Floor")
            ?? FindAssetByExactName<Material>("White")
            ?? FindAssetByExactName<Material>("Wood");
        wallMaterial = FindAssetByExactName<Material>("Walls")
            ?? FindAssetByExactName<Material>("Mat_Wall");
        frameMaterial = FindAssetByExactName<Material>("Frame")
            ?? FindAssetByExactName<Material>("Walls");
        hingeMaterial = FindAssetByExactName<Material>("Frame");
        startMarkerMaterial = FindAssetByExactName<Material>("Start");
        endMarkerMaterial = FindAssetByExactName<Material>("Goal");

        if (enableTracking)
        {
            marbleMarkerMaterial = FindAssetByExactName<Material>("RGB_Tracking_Marble")
                ?? FindAssetByExactName<Material>("Marble");
            trackingMarkerMaterial = FindAssetByExactName<Material>("Tracking_Marker")
                ?? FindAssetByExactName<Material>("RGB_Tracking_Marble");
        }
        else
        {
            marbleMarkerMaterial = FindAssetByExactName<Material>("Marble");
            // Keep tracking marker available if present; it will be ignored when tracking is disabled
            trackingMarkerMaterial = FindAssetByExactName<Material>("Tracking_Marker")
                ?? trackingMarkerMaterial;
        }

        holeTriggerMaterial = FindAssetByExactName<Material>("Hole")
            ?? FindAssetByExactName<Material>("Mat_Hole");
        endTriggerMaterial = FindAssetByExactName<Material>("Goal");
        meshHelperMaterial = FindAssetByExactName<Material>("Hole")
            ?? FindAssetByExactName<Material>("Mat_Hole");
    }

    private void RestoreDefaultPhysicMaterials()
    {
        floorPhysMaterial = FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysFloor[0])
            ?? FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysFloor.Length > 1 ? SVGImporterDefaults.Names.PhysFloor[1] : null);
        wallPhysMaterial = FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysWall[0])
            ?? FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysWall.Length > 1 ? SVGImporterDefaults.Names.PhysWall[1] : null);
        marblePhysMaterial = FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysMarbleArray[0])
            ?? FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysMarble);
        framePhysMaterial = FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysFrame[0])
            ?? FindAssetByExactName<PhysicMaterial>(SVGImporterDefaults.Names.PhysFrame.Length > 1 ? SVGImporterDefaults.Names.PhysFrame[1] : null);
    }

    private void RestoreDefaultGeometry()
    {
        floorHeight = SVGImporterDefaults.Values.FloorHeight;
        wallHeight = SVGImporterDefaults.Values.WallHeight;
        innerWallHeight = SVGImporterDefaults.Values.InnerWallHeight;
        marbleStartHeight = SVGImporterDefaults.Values.MarbleStartHeight;
        curveSampleDistance = SVGImporterDefaults.Values.CurveSampleDistance;
        simplifyFloorContours = SVGImporterDefaults.Values.SimplifyFloorContours;
        floorSimplifyTolerance = SVGImporterDefaults.Values.FloorSimplifyTolerance;
        fitToTargetSize = SVGImporterDefaults.Values.FitToTargetSize;
        targetWorldWidth = SVGImporterDefaults.Values.TargetWorldWidth;
        applyPreRotation = SVGImporterDefaults.Values.ApplyPreRotation;
        preRotationYDegrees = SVGImporterDefaults.Values.PreRotationYDegrees;
        mirrorX = SVGImporterDefaults.Values.MirrorX;
        mirrorZ = SVGImporterDefaults.Values.MirrorZ;
        createMeshHelpers = SVGImporterDefaults.Values.CreateMeshHelpers;
        createHoleTrigger = SVGImporterDefaults.Values.CreateHoleTrigger;
        createTiltRig = SVGImporterDefaults.Values.CreateTiltRig;
        frameThickness = SVGImporterDefaults.Values.FrameThickness;
        frameHeight = SVGImporterDefaults.Values.FrameHeight;
        frameGap = SVGImporterDefaults.Values.FrameGap;
        frameBoardClearance = SVGImporterDefaults.Values.FrameBoardClearance;
        frameMatchWallHeight = SVGImporterDefaults.Values.FrameMatchWallHeight;
        hingeRadius = SVGImporterDefaults.Values.HingeRadius;
        holeTriggerHeight = SVGImporterDefaults.Values.HoleTriggerHeight;
        agentTiltSpeed = SVGImporterDefaults.Values.TiltSpeed;
        agentMaxTilt = SVGImporterDefaults.Values.MaxTilt;
    }

    private void RestoreDefaultMarble()
    {
        marbleMass = SVGImporterDefaults.Values.MarbleMass;
        marbleDrag = SVGImporterDefaults.Values.MarbleDrag;
        marbleAngularDrag = SVGImporterDefaults.Values.MarbleAngularDrag;
    }

    private void RestoreDefaultAgent()
    {
        agentMaxStep = SVGImporterDefaults.Values.AgentMaxStep;
        agentKNearestHoles = SVGImporterDefaults.Values.AgentKNearestHoles;
        agentRayHeight = SVGImporterDefaults.Values.AgentRayHeight;
        agentClearanceFactor = SVGImporterDefaults.Values.AgentClearanceFactor;
        agentMilestoneBins = SVGImporterDefaults.Values.AgentMilestoneBins;
        agentMilestoneBonus = SVGImporterDefaults.Values.AgentMilestoneBonus;
        agentRayCount = SVGImporterDefaults.Values.AgentRayCount;
        agentMaxDistance = SVGImporterDefaults.Values.AgentMaxDistance;
    }

    private void RestoreDefaultBehavior()
    {
        behaviorName = SVGImporterDefaults.Values.BehaviorName;
        behaviorTeamId = SVGImporterDefaults.Values.BehaviorTeamId;
        behaviorType = SVGImporterDefaults.Values.BehaviorType;
        useChildSensors = SVGImporterDefaults.Values.UseChildSensors;
        vectorObservationSize = SVGImporterDefaults.Values.VectorObservationSize;
        continuousActionSize = SVGImporterDefaults.Values.ContinuousActionSize;
        stackedVectors = SVGImporterDefaults.Values.StackedVectors;
        decisionPeriod = SVGImporterDefaults.Values.DecisionPeriod;
        takeActionsBetweenDecisions = SVGImporterDefaults.Values.TakeActionsBetweenDecisions;
        inferenceDevice = SVGImporterDefaults.Values.InferenceDevice;
        behaviorModel = null;
    }

    private void RestoreDefaultTracking()
    {
        trackingMode = SvgImportSettings.TrackingMode.RGB;
        showTrackingFeedback = SVGImporterDefaults.Values.ShowTrackingFeedback;
        trkAddHud = SVGImporterDefaults.Values.TrkAddHud;
        trkShowDisplay = SVGImporterDefaults.Values.TrkShowDisplay;
        trackingMarkerScaleMultiplier = SVGImporterDefaults.Values.TrackingMarkerScaleMultiplier;
        trackingCamHeight = SVGImporterDefaults.Values.TrackingCamHeight;
        trackingCamPadding = SVGImporterDefaults.Values.TrackingCamPadding;
        trkPlaceTrackingMarkerOnFloor = SVGImporterDefaults.Values.TrkPlaceOnFloor;
        trkRestrictToFloorViewport = SVGImporterDefaults.Values.TrkRestrictToFloorViewport;
        trkClampToFloorBounds = SVGImporterDefaults.Values.TrkClampToFloor;
        trkSampleWidth = SVGImporterDefaults.Values.TrkSampleWidth;
        trkSampleHeight = SVGImporterDefaults.Values.TrkSampleHeight;
        trkSampleEveryNth = SVGImporterDefaults.Values.TrkSampleEveryNth;
        trkTargetTrackingFps = SVGImporterDefaults.Values.TrkTargetTrackingFps;
        trkMinPixelCount = SVGImporterDefaults.Values.TrkMinPixelCount;
        trkSmoothing = SVGImporterDefaults.Values.TrkSmoothing;
        trkSearchAroundLast = SVGImporterDefaults.Values.TrkSearchAroundLast;
        trkRoiRadiusViewport = SVGImporterDefaults.Values.TrkRoiRadiusViewport;
        trkEnablePrediction = SVGImporterDefaults.Values.TrkEnablePrediction;
        trkMaxMissedFrames = SVGImporterDefaults.Values.TrkMaxMissedFrames;
        trkVelocityDamping = SVGImporterDefaults.Values.TrkVelocityDamping;
        trkMissesBeforeFullScan = SVGImporterDefaults.Values.TrkMissesBeforeFullScan;
        trkRoiExpandFactorOnMiss = SVGImporterDefaults.Values.TrkRoiExpandFactorOnMiss;
        trkFloorViewportPadding = SVGImporterDefaults.Values.TrkFloorViewportPadding;
        trkTrackingMarkerHeightOffset = SVGImporterDefaults.Values.TrkTrackingMarkerHeightOffset;
        trkTargetColor = SVGImporterDefaults.Values.TrkTargetColor;
        trkColorTolerance = SVGImporterDefaults.Values.TrkColorTolerance;
    }

    private void RestoreDefaultImportTab()
    {
        // Features
        enableAgent = SVGImporterDefaults.Values.EnableAgent;
        enableTracking = SVGImporterDefaults.Values.EnableTracking;
        trackingMode = SVGImporterDefaults.Values.DefaultTrackingMode;
        addRealHud = SVGImporterDefaults.Values.AddRealHud;
        prevEnableTracking = enableTracking;

        // Color Mapping
        colorFloor = SVGImporterDefaults.Colors.Floor;
        colorWall = SVGImporterDefaults.Colors.Wall;
        colorStart = SVGImporterDefaults.Colors.Start;
        colorEnd = SVGImporterDefaults.Colors.End;
        colorMarble = SVGImporterDefaults.Colors.Marble;
        colorEndTrigger = SVGImporterDefaults.Colors.EndTrigger;
    }

    private void OnEnable()
    {
        if (s_appliedDefaults) return;
        // Auto-populate visual and physics materials at first open
        EnsureDefaultMaterialsAssigned();
        EnsureDefaultPhysicMaterialsAssigned();
        // If name lookups did not find assets, fall back to explicit default search
        if (AreCoreMaterialFieldsMissing())
        {
            RestoreDefaultMaterials();
            RestoreDefaultPhysicMaterials();
            // Try secondary fallbacks for physics names as well
            EnsureDefaultPhysicMaterialsAssigned();
        }
        s_appliedDefaults = true;
        // Initialize tracking toggle snapshot
        prevEnableTracking = enableTracking;
    }

    // Removed previous in-window Readme UI; now a separate window exists

    /// <summary>
    /// Renders the Import tab (file selection, presets, features, color mapping).
    /// </summary>
    private void DrawImportTab()
    {
		svgAsset = EditorGUILayout.ObjectField(T("SVG File", "Select an .svg asset or TextAsset to import as a level."), svgAsset, typeof(Object), false);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
        settingsAsset = (SvgImportSettings)EditorGUILayout.ObjectField(T("Settings Asset", "Optional SvgImportSettings preset to load/save importer parameters."), settingsAsset, typeof(SvgImportSettings), false);
        using (new EditorGUI.DisabledScope(settingsAsset == null))
        {
            EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button(T("Load From Preset", "Load values from the selected preset asset.")))
            {
                LoadFromSettingsAsset();
            }
			if (GUILayout.Button(T("Save To Preset", "Save current values into the selected preset asset.")))
            {
                SaveToSettingsAsset();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);
        enableAgent = EditorGUILayout.Toggle(T("Enable Agent", "Create and configure the BoardAgent component (includes ML-Agents components)."), enableAgent);
        enableTracking = EditorGUILayout.Toggle(T("Enable Tracking", "Create tracking objects and enable tracking setup."), enableTracking);
        addRealHud = EditorGUILayout.Toggle(T("Show HUD", "Create on-screen HUD for the real ball."), addRealHud);
        // When switching Tracking on/off, set a sensible default material for the marble
        if (enableTracking != prevEnableTracking)
        {
            if (enableTracking)
            {
                var m = FindAssetByExactName<Material>("RGB_Tracking_Marble");
                if (m != null) marbleMarkerMaterial = m;
            }
            else
            {
                var m = FindAssetByExactName<Material>("Marble");
                if (m != null) marbleMarkerMaterial = m;
            }
            prevEnableTracking = enableTracking;
        }
        EditorGUILayout.Space();
		EditorGUILayout.LabelField("Color Mapping", EditorStyles.boldLabel);
        colorFloor = EditorGUILayout.ColorField(T("Floor Color", "Fill color that marks floor shapes in the SVG."), colorFloor);
        colorWall = EditorGUILayout.ColorField(T("Wall Color", "Fill color that marks wall shapes in the SVG."), colorWall);
        colorStart = EditorGUILayout.ColorField(T("Start Marker Color", "Fill color that marks the start marker in the SVG."), colorStart);
        colorEnd = EditorGUILayout.ColorField(T("Goal Marker Color", "Fill color that marks the goal marker in the SVG."), colorEnd);
        colorMarble = EditorGUILayout.ColorField(T("Marble Color", "Fill color that marks the marble spawn in the SVG."), colorMarble);
        colorEndTrigger = EditorGUILayout.ColorField(T("Goal Trigger Color", "Fill color that explicitly marks a goal trigger in the SVG."), colorEndTrigger);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Restore Defaults", "Restore all Import tab settings to defaults.")))
            {
                RestoreDefaultImportTab();
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Configure settings in other tabs, then click Import.", MessageType.Info);
    }

    /// <summary>
    /// Renders geometry, sampling, sizing, orientation, rig, and helpers.
    /// </summary>
    private void DrawGeometryTab()
    {
        floorHeight = EditorGUILayout.FloatField(T("Floor Height", "Vertical thickness of the playable floor (world units)."), floorHeight);
        wallHeight = EditorGUILayout.FloatField(T("Frame Height", "Height of the outer boundary walls and frames (world units)."), wallHeight);
        innerWallHeight = EditorGUILayout.FloatField(T("Inner Walls Height", "Height for interior walls inside the maze (world units)."), innerWallHeight);
		marbleStartHeight = EditorGUILayout.FloatField(T("Marble Gap", "Vertical distance of the marble above the floor top (world units)."), marbleStartHeight);

        EditorGUILayout.Space();
		EditorGUILayout.LabelField("Curve Sampling", EditorStyles.boldLabel);
        curveSampleDistance = EditorGUILayout.Slider(T("Curve Sample Distance", "Distance between samples along SVG curves. Smaller = more triangles, higher fidelity."), curveSampleDistance, 0.02f, 2.0f);
        EditorGUILayout.Space();
		EditorGUILayout.LabelField("Floor Simplify", EditorStyles.boldLabel);
        simplifyFloorContours = EditorGUILayout.Toggle(T("Simplify Floor Top", "Reduces vertex count of the floor top outline using RDP simplification."), simplifyFloorContours);
        using (new EditorGUI.DisabledScope(!simplifyFloorContours))
        {
            floorSimplifyTolerance = EditorGUILayout.Slider(T("Floor Simplify Tolerance", "Tolerance for simplification in board units (SVG units). Larger values remove more detail."), floorSimplifyTolerance, 0.01f, 2.0f);
        }
        EditorGUILayout.Space();
		EditorGUILayout.LabelField("Sizing", EditorStyles.boldLabel);
		fitToTargetSize = EditorGUILayout.Toggle(T("Fit Width", "Uniformly scale the level so its world width matches Target World Width."), fitToTargetSize);
        using (new EditorGUI.DisabledScope(!fitToTargetSize))
        {
            targetWorldWidth = EditorGUILayout.FloatField(T("Target World Width", "Desired world-space width of the imported board (X extent)."), targetWorldWidth);
        }
        EditorGUILayout.Space();
		EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
		applyPreRotation = EditorGUILayout.Toggle(T("Apply Rotation", "Bake a Y-axis rotation directly into meshes and positions (Inspector shows 0° after import)."), applyPreRotation);
        using (new EditorGUI.DisabledScope(!applyPreRotation))
        {
			preRotationYDegrees = EditorGUILayout.FloatField(T("Y Rotation (deg)", "Degrees to rotate around world Y before placing objects."), preRotationYDegrees);
        }
		mirrorX = EditorGUILayout.Toggle(T("Mirror X", "Flip the level along the X axis after import."), mirrorX);
		mirrorZ = EditorGUILayout.Toggle(T("Mirror Z", "Flip the level along the Z axis after import."), mirrorZ);
        EditorGUILayout.Space();
		EditorGUILayout.LabelField("Tilt Rig", EditorStyles.boldLabel);
		createTiltRig = EditorGUILayout.Toggle(T("Tilt Rig", "Build a gimballed frame around the board for physical tilting and visualization."), createTiltRig);
        using (new EditorGUI.DisabledScope(!createTiltRig))
        {
			frameThickness = EditorGUILayout.Slider(T("Frame Thickness", "Beam thickness for inner/outer frames (visual width)."), frameThickness, 0.01f, 5f);
            frameMatchWallHeight = EditorGUILayout.Toggle(T("Match Wall Height", "Match the frame height to the tallest wall of the imported board."), frameMatchWallHeight);
            using (new EditorGUI.DisabledScope(frameMatchWallHeight))
            {
                frameHeight = EditorGUILayout.Slider(T("Frame Height", "Height of the frames when not matching the wall height."), frameHeight, 0.01f, 200f);
            }
			frameGap = EditorGUILayout.Slider(T("Gap Between Frames", "Visible gap between the outer and inner frame."), frameGap, 0.0f, 0.5f);
			frameBoardClearance = EditorGUILayout.Slider(T("Board Clearance", "Gap between the inner frame and the playable board to avoid contact."), frameBoardClearance, 0.0f, 0.2f);
            EditorGUILayout.Space();
            hingeRadius = EditorGUILayout.Slider(T("Hinge Radius", "Visual thickness of hinge cylinders (always created with the rig)."), hingeRadius, 0.005f, 1.0f);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tilt Controls", EditorStyles.boldLabel);
            agentTiltSpeed = EditorGUILayout.FloatField(T("Tilt Speed", "Speed at which the board can tilt (deg/sec)."), agentTiltSpeed);
            agentMaxTilt = EditorGUILayout.FloatField(T("Max Tilt", "Maximum tilt angle allowed (degrees)."), agentMaxTilt);
        }
        EditorGUILayout.Space();
		createMeshHelpers = EditorGUILayout.Toggle(T("Hole Markers", "Create small sphere helpers for detected hole contours (debugging/authoring)."), createMeshHelpers);
		createHoleTrigger = EditorGUILayout.Toggle(T("Hole Triggers", "Create trigger boxes under the board to catch balls falling through holes."), createHoleTrigger);
        using (new EditorGUI.DisabledScope(!createHoleTrigger))
        {
            holeTriggerHeight = EditorGUILayout.FloatField(T("Hole Trigger Height", "Height in board/SVG units (scales with import)."), holeTriggerHeight);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Restore Defaults", "Restore default geometry settings.")))
            {
                RestoreDefaultGeometry();
            }
        }
    }

    /// <summary>
    /// Renders material and physic material assignments with Restore defaults.
    /// </summary>
    private void DrawMaterialsTab()
    {
		EditorGUILayout.LabelField("Materials", EditorStyles.boldLabel);
        floorMaterial = (Material)EditorGUILayout.ObjectField(T("Floor", "Material applied to the floor mesh."), floorMaterial, typeof(Material), false);
        wallMaterial = (Material)EditorGUILayout.ObjectField(T("Wall", "Material applied to inner walls."), wallMaterial, typeof(Material), false);
        frameMaterial = (Material)EditorGUILayout.ObjectField(T("Frame", "Material applied to the outer wall/frames."), frameMaterial, typeof(Material), false);
        using (new EditorGUI.DisabledScope(!createTiltRig))
        {
            hingeMaterial = (Material)EditorGUILayout.ObjectField(T("Hinge", "Material used for visual hinge cylinders."), hingeMaterial, typeof(Material), false);
        }
        startMarkerMaterial = (Material)EditorGUILayout.ObjectField(T("Start Marker", "Material for the start marker cylinder."), startMarkerMaterial, typeof(Material), false);
        endMarkerMaterial = (Material)EditorGUILayout.ObjectField(T("Goal Marker", "Material for the goal marker cylinder."), endMarkerMaterial, typeof(Material), false);
        marbleMarkerMaterial = (Material)EditorGUILayout.ObjectField(T("Marble", "Material for the marble (visual only)."), marbleMarkerMaterial, typeof(Material), false);
        using (new EditorGUI.DisabledScope(!enableTracking))
        {
            trackingMarkerMaterial = (Material)EditorGUILayout.ObjectField(T("Tracking Marker", "Material for the tracking marker overlay."), trackingMarkerMaterial, typeof(Material), false);
        }
        using (new EditorGUI.DisabledScope(!createHoleTrigger))
        {
            holeTriggerMaterial = (Material)EditorGUILayout.ObjectField(T("Hole Trigger", "Optional material to visualize the catch trigger (debug)."), holeTriggerMaterial, typeof(Material), false);
        }
        endTriggerMaterial = (Material)EditorGUILayout.ObjectField(T("Goal Trigger", "Material for the goal trigger sphere (if present)."), endTriggerMaterial, typeof(Material), false);
        using (new EditorGUI.DisabledScope(!createMeshHelpers))
        {
            meshHelperMaterial = (Material)EditorGUILayout.ObjectField(T("Hole Markers", "Material for hole helper gizmos."), meshHelperMaterial, typeof(Material), false);
        }
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Physics Materials", EditorStyles.boldLabel);
		floorPhysMaterial = (PhysicMaterial)EditorGUILayout.ObjectField(T("Floor Physics", "Physics material applied to floor colliders."), floorPhysMaterial, typeof(PhysicMaterial), false);
		wallPhysMaterial = (PhysicMaterial)EditorGUILayout.ObjectField(T("Wall Physics", "Physics material applied to wall colliders."), wallPhysMaterial, typeof(PhysicMaterial), false);
		marblePhysMaterial = (PhysicMaterial)EditorGUILayout.ObjectField(T("Marble Physics", "Physics material applied to the marble collider."), marblePhysMaterial, typeof(PhysicMaterial), false);
        using (new EditorGUI.DisabledScope(!createTiltRig))
        {
			framePhysMaterial = (PhysicMaterial)EditorGUILayout.ObjectField(T("Frame Physics", "Physics material applied to frame beams (colliders)."), framePhysMaterial, typeof(PhysicMaterial), false);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Restore Defaults", "Restore default materials (visual + physics).")))
            {
                RestoreDefaultMaterials();
                RestoreDefaultPhysicMaterials();
                // Make sure anything not found via first-choice names tries fallbacks too
                EnsureDefaultPhysicMaterialsAssigned();
            }
        }
    }

    /// <summary>
    /// Renders marble rigidbody settings.
    /// </summary>
    private void DrawMarbleTab()
    {
		EditorGUILayout.LabelField("Marble", EditorStyles.boldLabel);
		marbleMass = EditorGUILayout.FloatField(T("Mass", "Rigidbody mass of the marble."), marbleMass);
		marbleDrag = EditorGUILayout.FloatField(T("Drag", "Linear drag applied to the marble."), marbleDrag);
		marbleAngularDrag = EditorGUILayout.FloatField(T("Ang. Drag", "Rotational drag applied to the marble."), marbleAngularDrag);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Restore Defaults", "Restore default marble physics values.")))
            {
                RestoreDefaultMarble();
            }
        }
    }

    /// <summary>
    /// Renders agent + ML-Agents settings when agent feature is enabled.
    /// </summary>
    private void DrawAgentTab()
    {
        EditorGUILayout.LabelField("Agent", EditorStyles.boldLabel);
        agentMaxStep = EditorGUILayout.IntField(T("Max Step", "Maximum steps per episode for the agent."), agentMaxStep);
		agentKNearestHoles = EditorGUILayout.IntField(T("Nearest Holes", "Number of nearby holes considered for observations."), agentKNearestHoles);
        agentMilestoneBins = EditorGUILayout.IntField(T("Milestone Bins", "Number of bins along the path for milestone rewards."), agentMilestoneBins);
        agentMilestoneBonus = EditorGUILayout.FloatField(T("Milestone Bonus", "Bonus reward granted when passing a milestone."), agentMilestoneBonus);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Raycasts", EditorStyles.boldLabel);
        agentRayCount = EditorGUILayout.IntField(T("Ray Count", "Number of radial rays cast for sensing obstacles."), agentRayCount);
        agentMaxDistance = EditorGUILayout.FloatField(T("Max Distance", "Maximum distance for obstacle raycasts."), agentMaxDistance);
        agentRayHeight = EditorGUILayout.FloatField(T("Ray Height", "Height above the floor from which obstacle rays are cast."), agentRayHeight);
        agentClearanceFactor = EditorGUILayout.FloatField(T("Clearance Factor", "Safety factor applied around holes/obstacles."), agentClearanceFactor);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("ML-Agents", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!enableAgent))
        {
            behaviorName = EditorGUILayout.TextField(T("Name", "Unique name for the ML-Agents behavior."), behaviorName);
            behaviorTeamId = EditorGUILayout.IntField(T("Team ID", "Team identifier for multi-agent setups."), behaviorTeamId);
            behaviorModel = (NNModel)EditorGUILayout.ObjectField(T("Model", "Optional NN Model to run in Inference mode."), behaviorModel, typeof(NNModel), false);
            behaviorType = (BehaviorType)EditorGUILayout.EnumPopup(T("Type", "Training/Inference mode for this behavior."), behaviorType);
            inferenceDevice = (InferenceDevice)EditorGUILayout.EnumPopup(T("Device", "Inference device for the model."), inferenceDevice);
            useChildSensors = EditorGUILayout.Toggle(T("Child Sensors", "Include sensors on child objects in observations."), useChildSensors);
            vectorObservationSize = EditorGUILayout.IntField(T("Obs Size", "Size of the vector observation for the policy."), vectorObservationSize);
            continuousActionSize = EditorGUILayout.IntField(T("Action Size", "Number of continuous actions (e.g., X/Z tilt)."), continuousActionSize);
            stackedVectors = EditorGUILayout.IntField(T("Stacked Vectors", "Number of stacked vector observations."), stackedVectors);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Decisions", EditorStyles.boldLabel);
            decisionPeriod = EditorGUILayout.IntField(T("Period", "Number of frames between agent decisions."), decisionPeriod);
            takeActionsBetweenDecisions = EditorGUILayout.Toggle(T("Act Between", "If enabled, actions are applied every frame between decisions."), takeActionsBetweenDecisions);
        }
        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(T("Restore Defaults", "Restore default Agent and ML-Agents settings.")))
            {
                RestoreDefaultAgent();
                RestoreDefaultBehavior();
            }
        }
    }

    // Removed separate ML-Agents tab; ML controls now live under Agent tab

    /// <summary>
    /// Renders tracking mode, marker, camera, sampling, detection, and RGB target.
    /// </summary>
    private void DrawTrackingTab()
    {
		EditorGUILayout.LabelField("Tracking", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!enableTracking))
        {
            trackingMode = (SvgImportSettings.TrackingMode)EditorGUILayout.EnumPopup(T("Mode", "RGB: color thresholds; Shader: BallMask shader writes a binary mask."), trackingMode);
            EditorGUILayout.Space();

		EditorGUILayout.LabelField("Marker", EditorStyles.boldLabel);
            showTrackingFeedback = EditorGUILayout.Toggle(T("Show Tracking Marker", "Enable the tracking marker and HUD."), showTrackingFeedback);
            trkAddHud = EditorGUILayout.Toggle(T("Show HUD", "Create on-screen HUD (text + tracking camera preview)."), trkAddHud);
            trkShowDisplay = EditorGUILayout.Toggle(T("Show Display", "Show the tracking camera preview on the HUD."), trkShowDisplay);
            trackingMarkerScaleMultiplier = EditorGUILayout.Slider(T("Scale Multiplier", "Visual size multiplier for the tracking marker relative to the marble."), trackingMarkerScaleMultiplier, 0.5f, 3.0f);
            EditorGUILayout.Space();

			EditorGUILayout.LabelField("Camera", EditorStyles.boldLabel);
			trackingCamHeight = EditorGUILayout.Slider(T("Height", "Distance of the tracking camera above the board."), trackingCamHeight, 2f, 30f);
			trackingCamPadding = EditorGUILayout.Slider(T("Padding", "Extra margin around the board in the tracking view."), trackingCamPadding, 0f, 5f);
            EditorGUILayout.Space();

			EditorGUILayout.LabelField("Sampling", EditorStyles.boldLabel);
            trkSampleWidth = EditorGUILayout.IntSlider(T("Sample Width", "Downscaled texture width for CPU sampling."), trkSampleWidth, 64, 1024);
            trkSampleHeight = EditorGUILayout.IntSlider(T("Sample Height", "Downscaled texture height for CPU sampling."), trkSampleHeight, 64, 1024);
			trkSampleEveryNth = EditorGUILayout.IntSlider(T("Sample Stride", "Process every Nth frame to reduce cost."), trkSampleEveryNth, 1, 4);
            trkTargetTrackingFps = EditorGUILayout.Slider(T("Target Tracking FPS", "0 = off; if > 0, throttles tracking by time instead of frame stride."), trkTargetTrackingFps, 0f, 120f);
            trkSmoothing = EditorGUILayout.Slider(T("Smoothing", "Temporal smoothing of the detected centroid (0 = none)."), trkSmoothing, 0f, 1f);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Detection", EditorStyles.boldLabel);
			trkPlaceTrackingMarkerOnFloor = EditorGUILayout.Toggle(T("Marker On Floor", "Project the tracking marker onto the floor plane instead of placing it in 3D."), trkPlaceTrackingMarkerOnFloor);
			trkRestrictToFloorViewport = EditorGUILayout.Toggle(T("Floor Viewport Only", "Only search within the viewport area that covers the floor."), trkRestrictToFloorViewport);
            trkFloorViewportPadding = EditorGUILayout.Slider(T("Floor Viewport Padding", "Padding added around the computed floor viewport when restricting."), trkFloorViewportPadding, 0f, 0.2f);
			trkClampToFloorBounds = EditorGUILayout.Toggle(T("Clamp To Floor", "Clamp tracking marker position to the floor bounds."), trkClampToFloorBounds);
			trkTrackingMarkerHeightOffset = EditorGUILayout.Slider(T("Marker Height", "Offset above the floor when placing the tracking marker."), trkTrackingMarkerHeightOffset, 0f, 1.5f);
            trkSearchAroundLast = EditorGUILayout.Toggle(T("Search Around Last", "Search in a small ROI around the previous detection first."), trkSearchAroundLast);
            trkRoiRadiusViewport = EditorGUILayout.Slider(T("ROI Radius (viewport)", "Base radius of the search ROI in viewport units (0..1)."), trkRoiRadiusViewport, 0.05f, 0.75f);
            trkEnablePrediction = EditorGUILayout.Toggle(T("Enable Prediction", "Predict motion briefly when detection is lost."), trkEnablePrediction);
            trkMaxMissedFrames = EditorGUILayout.IntSlider(T("Max Missed Frames", "Maximum frames to continue predicting when lost."), trkMaxMissedFrames, 0, 60);
            trkVelocityDamping = EditorGUILayout.Slider(T("Velocity Damping", "Damping of the predicted viewport velocity (0..1)."), trkVelocityDamping, 0f, 1f);
			trkMissesBeforeFullScan = EditorGUILayout.IntSlider(T("Misses Before Scan", "Number of consecutive misses before scanning the full frame."), trkMissesBeforeFullScan, 0, 10);
			trkRoiExpandFactorOnMiss = EditorGUILayout.Slider(T("ROI Growth", "ROI growth factor applied per miss while searching."), trkRoiExpandFactorOnMiss, 1f, 4f);
            EditorGUILayout.Space();

            if (trackingMode == SvgImportSettings.TrackingMode.RGB)
            {
			EditorGUILayout.LabelField("RGB Target", EditorStyles.boldLabel);
				trkTargetColor = EditorGUILayout.ColorField(T("Target", "RGB color to detect."), trkTargetColor);
				trkColorTolerance = EditorGUILayout.Slider(T("Tolerance", "RGB distance tolerance (0..1)."), trkColorTolerance, 0f, 1f);
				trkMinPixelCount = EditorGUILayout.IntSlider(T("Min Pixels", "Minimum number of matching pixels to accept a detection."), trkMinPixelCount, 0, 200);
            }
            else
            {
                EditorGUILayout.HelpBox("Shader mode uses BallMask.shader; RGB color target is ignored.", MessageType.Info);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(T("Restore Defaults", "Restore default tracking settings.")))
                {
                    RestoreDefaultTracking();
                }
            }
        }
    }
}


