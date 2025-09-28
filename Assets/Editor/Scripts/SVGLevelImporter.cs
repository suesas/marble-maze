using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using Unity.VectorGraphics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using System.Text.RegularExpressions;
using UnityEngine.AI;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Actuators;
using Unity.Barracuda;

public partial class SVGLevelImporter : EditorWindow
{
    /// <summary>
    /// Opens the SVG Level Importer window.
    /// </summary>
    [MenuItem("Tools/Import SVG Level")]
    public static void ShowWindow()
    {
        GetWindow<SVGLevelImporter>("SVG Level Importer");
    }

    
    // Optional settings preset (ScriptableObject) to load/save importer parameters
    private SvgImportSettings settingsAsset;

    private Object svgAsset;
    private float floorHeight = SVGImporterDefaults.Values.FloorHeight;
    private float wallHeight = SVGImporterDefaults.Values.WallHeight;
    private float innerWallHeight = SVGImporterDefaults.Values.InnerWallHeight;
    private float marbleStartHeight = SVGImporterDefaults.Values.MarbleStartHeight;
    private float curveSampleDistance = SVGImporterDefaults.Values.CurveSampleDistance;
    private bool simplifyFloorContours = SVGImporterDefaults.Values.SimplifyFloorContours;
    private float floorSimplifyTolerance = SVGImporterDefaults.Values.FloorSimplifyTolerance;
    private bool fitToTargetSize = SVGImporterDefaults.Values.FitToTargetSize;
    private float targetWorldWidth = SVGImporterDefaults.Values.TargetWorldWidth;
    private bool applyPreRotation = SVGImporterDefaults.Values.ApplyPreRotation;
    private float preRotationYDegrees = SVGImporterDefaults.Values.PreRotationYDegrees;
    private bool mirrorX = SVGImporterDefaults.Values.MirrorX;
    private bool mirrorZ = SVGImporterDefaults.Values.MirrorZ;
    private bool createMeshHelpers = SVGImporterDefaults.Values.CreateMeshHelpers;
    private bool createHoleTrigger = SVGImporterDefaults.Values.CreateHoleTrigger;
    private bool createTiltRig = SVGImporterDefaults.Values.CreateTiltRig;
    private float frameThickness = SVGImporterDefaults.Values.FrameThickness;
    private float frameHeight = SVGImporterDefaults.Values.FrameHeight;
    private float frameGap = SVGImporterDefaults.Values.FrameGap;
    private float frameBoardClearance = SVGImporterDefaults.Values.FrameBoardClearance;
    private bool frameMatchWallHeight = SVGImporterDefaults.Values.FrameMatchWallHeight;
    // Hinge visuals (always created when a rig is built)
    private float hingeRadius = SVGImporterDefaults.Values.HingeRadius;
    private Material hingeMaterial;
    private enum Tab { Import, Geometry, Materials, Marble, Agent, Tracking }
    private Tab selectedTab = Tab.Import;
    private static readonly string[] TAB_LABELS = System.Enum.GetValues(typeof(Tab)).Cast<Tab>().Select(GetTabLabel).ToArray();
    private static string GetTabLabel(Tab tab)
    {
        return tab.ToString();
    }

   
    private float marbleMass = 5f;
    private float marbleDrag = 0.01f;
    private float marbleAngularDrag = 0.05f;

   
    // Catch trigger height in SVG/board units (scales with import)
    private float holeTriggerHeight = SVGImporterDefaults.Values.HoleTriggerHeight;

    private Material floorMaterial;
    private Material wallMaterial;
    private Material startMarkerMaterial;
    private Material endMarkerMaterial;
    private Material marbleMarkerMaterial;
    private Material holeTriggerMaterial;
    private Material meshHelperMaterial;
    private Material endTriggerMaterial;
    private Material trackingMarkerMaterial; // user-selectable tracking marker material
    private Material frameMaterial;

    private PhysicMaterial floorPhysMaterial;
    private PhysicMaterial wallPhysMaterial;
    private PhysicMaterial marblePhysMaterial;
    private PhysicMaterial framePhysMaterial;

   
    // Tracking & camera UI
    private float trackingMarkerScaleMultiplier = SVGImporterDefaults.Values.TrackingMarkerScaleMultiplier;
    private float trackingCamHeight = SVGImporterDefaults.Values.TrackingCamHeight;
    private float trackingCamPadding = SVGImporterDefaults.Values.TrackingCamPadding;

    private SvgImportSettings.TrackingMode trackingMode = SvgImportSettings.TrackingMode.RGB;
    private bool enableTracking = SVGImporterDefaults.Values.EnableTracking;
    private bool enableAgent = SVGImporterDefaults.Values.EnableAgent;
    // ML-Agents now tied to Agent; no separate toggle

    private bool showTrackingFeedback = SVGImporterDefaults.Values.ShowTrackingFeedback;
    private bool trkAddHud = SVGImporterDefaults.Values.TrkAddHud;
    private bool addRealHud = SVGImporterDefaults.Values.AddRealHud;
    private bool trkShowDisplay = SVGImporterDefaults.Values.TrkShowDisplay;
    private bool trkPlaceTrackingMarkerOnFloor = SVGImporterDefaults.Values.TrkPlaceOnFloor;
    private bool trkRestrictToFloorViewport = SVGImporterDefaults.Values.TrkRestrictToFloorViewport;
    private bool trkClampToFloorBounds = SVGImporterDefaults.Values.TrkClampToFloor;
    private int trkSampleWidth = SVGImporterDefaults.Values.TrkSampleWidth;
    private int trkSampleHeight = SVGImporterDefaults.Values.TrkSampleHeight;
    private int trkSampleEveryNth = SVGImporterDefaults.Values.TrkSampleEveryNth;
    private float trkTargetTrackingFps = SVGImporterDefaults.Values.TrkTargetTrackingFps;
    private int trkMinPixelCount = SVGImporterDefaults.Values.TrkMinPixelCount;
    private float trkSmoothing = SVGImporterDefaults.Values.TrkSmoothing;
    private bool trkSearchAroundLast = SVGImporterDefaults.Values.TrkSearchAroundLast;
    private float trkRoiRadiusViewport = SVGImporterDefaults.Values.TrkRoiRadiusViewport;
    private bool trkEnablePrediction = SVGImporterDefaults.Values.TrkEnablePrediction;
    private int trkMaxMissedFrames = SVGImporterDefaults.Values.TrkMaxMissedFrames;
    private float trkVelocityDamping = SVGImporterDefaults.Values.TrkVelocityDamping;
    private int trkMissesBeforeFullScan = SVGImporterDefaults.Values.TrkMissesBeforeFullScan;
    private float trkRoiExpandFactorOnMiss = SVGImporterDefaults.Values.TrkRoiExpandFactorOnMiss;
    private float trkFloorViewportPadding = SVGImporterDefaults.Values.TrkFloorViewportPadding;
    private float trkTrackingMarkerHeightOffset = SVGImporterDefaults.Values.TrkTrackingMarkerHeightOffset;
    private Color trkTargetColor = SVGImporterDefaults.Values.TrkTargetColor;
    private float trkColorTolerance = SVGImporterDefaults.Values.TrkColorTolerance;

    private int agentMaxStep = SVGImporterDefaults.Values.AgentMaxStep;
    private float agentTiltSpeed = SVGImporterDefaults.Values.TiltSpeed;
    private float agentMaxTilt = SVGImporterDefaults.Values.MaxTilt;
    private int agentKNearestHoles = SVGImporterDefaults.Values.AgentKNearestHoles;
    // Legacy sizing values removed
    private float agentRayHeight = SVGImporterDefaults.Values.AgentRayHeight;
    // Legacy ball radius removed
    private float agentClearanceFactor = SVGImporterDefaults.Values.AgentClearanceFactor;
    // Newly exposed agent parameters
    private int agentMilestoneBins = SVGImporterDefaults.Values.AgentMilestoneBins;
    private float agentMilestoneBonus = SVGImporterDefaults.Values.AgentMilestoneBonus;
    private int agentRayCount = SVGImporterDefaults.Values.AgentRayCount;
    private float agentMaxDistance = SVGImporterDefaults.Values.AgentMaxDistance;
   
    private string behaviorName = SVGImporterDefaults.Values.BehaviorName;
    private int behaviorTeamId = SVGImporterDefaults.Values.BehaviorTeamId;
    private BehaviorType behaviorType = SVGImporterDefaults.Values.BehaviorType;
    private bool useChildSensors = SVGImporterDefaults.Values.UseChildSensors;
    private int vectorObservationSize = SVGImporterDefaults.Values.VectorObservationSize;
    private int continuousActionSize = SVGImporterDefaults.Values.ContinuousActionSize;
    private int stackedVectors = SVGImporterDefaults.Values.StackedVectors;
    private NNModel behaviorModel;
    private InferenceDevice inferenceDevice = SVGImporterDefaults.Values.InferenceDevice;
   
    private int decisionPeriod = SVGImporterDefaults.Values.DecisionPeriod;
    private bool takeActionsBetweenDecisions = SVGImporterDefaults.Values.TakeActionsBetweenDecisions;
    
    private enum MarkerType { None, Start, End, Marble, EndTrigger }

    private static readonly Color COLOR_FLOOR = SVGImporterDefaults.Colors.Floor;
    private static readonly Color COLOR_WALL = SVGImporterDefaults.Colors.Wall;
    private static readonly Color COLOR_START = SVGImporterDefaults.Colors.Start;
    private static readonly Color COLOR_END = SVGImporterDefaults.Colors.End;
    private static readonly Color COLOR_MARBLE = SVGImporterDefaults.Colors.Marble;
    private static readonly Color COLOR_END_TRIGGER = SVGImporterDefaults.Colors.EndTrigger;

    // Configurable color mapping (defaults set from constants above)
    private Color colorFloor = COLOR_FLOOR;
    private Color colorWall = COLOR_WALL;
    private Color colorStart = COLOR_START;
    private Color colorEnd = COLOR_END;
    private Color colorMarble = COLOR_MARBLE;
    private Color colorEndTrigger = COLOR_END_TRIGGER;

    // Derived at import time: marble radius in world units after all scaling
    private float computedMarbleRadiusWorld = -1f;

   
    private const string LABEL_FLOOR = "Floor";
    private const string LABEL_WALL_PREFIX = "Wall";
    private const string LABEL_HOLE_PREFIX = "Hole";
    private const string TAG_HOLE = "Hole";
    private const string NAME_MESH_HELPER = "HoleMarkers";
    private const string NAME_NAVMESH_SURFACE = "NavMeshSurface";
    private const string NAME_HOLE_TRIGGER = "HoleTrigger";
    private const string NAME_END_TRIGGER = "GoalTrigger";
    private const string NAME_START_MARKER = "StartMarker";
    private const string NAME_END_MARKER = "GoalMarker";
    private const string NAME_MARBLE_MARKER = "Marble";

   
    private const int AREA_WALKABLE = 0;
    private const int AREA_NOT_WALKABLE = 1;

    // Removed unused ParseHtmlColor helper

    // Converts file/system names like "game_area_new" or "game-area new" to PascalCase: GameAreaNew
    private static string ToPascalCase(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Level";
        var parts = Regex.Split(raw, "[^A-Za-z0-9]+");
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (string.IsNullOrEmpty(p)) continue;
            if (p.Length == 1) sb.Append(char.ToUpperInvariant(p[0]));
            else sb.Append(char.ToUpperInvariant(p[0])).Append(p.Substring(1));
        }
        string result = sb.Length > 0 ? sb.ToString() : "Level";
        if (!string.IsNullOrEmpty(result) && char.IsDigit(result[0])) result = "N" + result;
        return result;
    }

    // Settings synchronization helpers
    private void LoadFromSettingsAsset()
    {
        if (settingsAsset == null) return;
        // Geometry & sizing
        floorHeight = settingsAsset.floorHeight;
        wallHeight = settingsAsset.wallHeight;
        innerWallHeight = settingsAsset.innerWallHeight;
        marbleStartHeight = settingsAsset.marbleStartHeight;
        curveSampleDistance = settingsAsset.curveSampleDistance;
        simplifyFloorContours = settingsAsset.simplifyFloorContours;
        floorSimplifyTolerance = settingsAsset.floorSimplifyTolerance;
        fitToTargetSize = settingsAsset.fitToTargetSize;
        targetWorldWidth = settingsAsset.targetWorldWidth;
        applyPreRotation = settingsAsset.applyPreRotation;
        preRotationYDegrees = settingsAsset.preRotationYDegrees;
        mirrorX = settingsAsset.mirrorX;
        mirrorZ = settingsAsset.mirrorZ;

        // Frames / Rig
        createTiltRig = settingsAsset.createTiltRig;
        frameThickness = settingsAsset.frameThickness;
        frameHeight = settingsAsset.frameHeight;
        frameGap = settingsAsset.frameGap;
        frameBoardClearance = settingsAsset.frameBoardClearance;
        frameMatchWallHeight = settingsAsset.frameMatchWallHeight;
        hingeRadius = settingsAsset.hingeRadius;

        // Helpers / Triggers
        createMeshHelpers = settingsAsset.createMeshHelpers;
        createHoleTrigger = settingsAsset.createHoleTrigger;
        holeTriggerHeight = settingsAsset.holeTriggerHeight;

        // Materials
        floorMaterial = settingsAsset.floorMaterial;
        wallMaterial = settingsAsset.wallMaterial;
        frameMaterial = settingsAsset.frameMaterial;
        hingeMaterial = settingsAsset.hingeMaterial;
        startMarkerMaterial = settingsAsset.startMarkerMaterial;
        endMarkerMaterial = settingsAsset.endMarkerMaterial;
        marbleMarkerMaterial = settingsAsset.marbleMarkerMaterial;
        trackingMarkerMaterial = settingsAsset.trackingMarkerMaterial;
        holeTriggerMaterial = settingsAsset.holeTriggerMaterial;
        endTriggerMaterial = settingsAsset.endTriggerMaterial;
        meshHelperMaterial = settingsAsset.meshHelperMaterial;

        // Physic materials
        floorPhysMaterial = settingsAsset.floorPhysMaterial;
        wallPhysMaterial = settingsAsset.wallPhysMaterial;
        marblePhysMaterial = settingsAsset.marblePhysMaterial;
        framePhysMaterial = settingsAsset.framePhysMaterial;

        // Marble physics
        marbleMass = settingsAsset.marbleMass;
        marbleDrag = settingsAsset.marbleDrag;
        marbleAngularDrag = settingsAsset.marbleAngularDrag;

        // Agent
        agentMaxStep = settingsAsset.agentMaxStep;
        // Shared tilt settings live in Geometry section of settings
        agentTiltSpeed = settingsAsset.tiltSpeed;
        agentMaxTilt = settingsAsset.maxTilt;
        agentKNearestHoles = settingsAsset.agentKNearestHoles;
        agentRayHeight = settingsAsset.agentRayHeight;
        agentClearanceFactor = settingsAsset.agentClearanceFactor;
        // New agent fields
        agentMilestoneBins = settingsAsset.agentMilestoneBins;
        agentMilestoneBonus = settingsAsset.agentMilestoneBonus;
        agentRayCount = settingsAsset.agentRayCount;
        agentMaxDistance = settingsAsset.agentMaxDistance;

        // BehaviorParameters
        behaviorName = settingsAsset.behaviorName;
        behaviorTeamId = settingsAsset.behaviorTeamId;
        behaviorType = settingsAsset.behaviorType;
        useChildSensors = settingsAsset.useChildSensors;
        vectorObservationSize = settingsAsset.vectorObservationSize;
        continuousActionSize = settingsAsset.continuousActionSize;
        stackedVectors = settingsAsset.stackedVectors;
        decisionPeriod = settingsAsset.decisionPeriod;
        takeActionsBetweenDecisions = settingsAsset.takeActionsBetweenDecisions;
        behaviorModel = settingsAsset.behaviorModel;
        inferenceDevice = settingsAsset.inferenceDevice;

        // Features
        enableAgent = settingsAsset.enableAgent;
        enableTracking = settingsAsset.enableTracking;

        // Tracking
        trackingMode = settingsAsset.trackingMode;
        showTrackingFeedback = settingsAsset.showTrackingFeedback;
        trkAddHud = settingsAsset.trkAddHud;
        addRealHud = settingsAsset.addRealHud;
        trkShowDisplay = settingsAsset.trkShowDisplay;
        trackingMarkerScaleMultiplier = settingsAsset.trackingMarkerScaleMultiplier;
        trackingCamHeight = settingsAsset.trackingCamHeight;
        trackingCamPadding = settingsAsset.trackingCamPadding;
        trkPlaceTrackingMarkerOnFloor = settingsAsset.trkPlaceTrackingMarkerOnFloor;
        trkRestrictToFloorViewport = settingsAsset.trkRestrictToFloorViewport;
        trkClampToFloorBounds = settingsAsset.trkClampToFloorBounds;
        trkSampleWidth = settingsAsset.trkSampleWidth;
        trkSampleHeight = settingsAsset.trkSampleHeight;
        trkSampleEveryNth = settingsAsset.trkSampleEveryNth;
        trkTargetTrackingFps = settingsAsset.trkTargetTrackingFps;
        trkMinPixelCount = settingsAsset.trkMinPixelCount;
        trkSmoothing = settingsAsset.trkSmoothing;
        trkSearchAroundLast = settingsAsset.trkSearchAroundLast;
        trkRoiRadiusViewport = settingsAsset.trkRoiRadiusViewport;
        trkEnablePrediction = settingsAsset.trkEnablePrediction;
        trkMaxMissedFrames = settingsAsset.trkMaxMissedFrames;
        trkVelocityDamping = settingsAsset.trkVelocityDamping;
        trkMissesBeforeFullScan = settingsAsset.trkMissesBeforeFullScan;
        trkRoiExpandFactorOnMiss = settingsAsset.trkRoiExpandFactorOnMiss;
        trkFloorViewportPadding = settingsAsset.trkFloorViewportPadding;
        trkTrackingMarkerHeightOffset = settingsAsset.trkTrackingMarkerHeightOffset;
        trkTargetColor = settingsAsset.trkTargetColor;
        trkColorTolerance = settingsAsset.trkColorTolerance;

        // Color Mapping
        colorFloor = settingsAsset.colorFloor;
        colorWall = settingsAsset.colorWall;
        colorStart = settingsAsset.colorStart;
        colorEnd = settingsAsset.colorEnd;
        colorMarble = settingsAsset.colorMarble;
        colorEndTrigger = settingsAsset.colorEndTrigger;

        EditorUtility.SetDirty(settingsAsset);
        AssetDatabase.SaveAssets();
    }

    private void SaveToSettingsAsset()
    {
        if (settingsAsset == null) return;
        // Geometry & sizing
        settingsAsset.floorHeight = floorHeight;
        settingsAsset.wallHeight = wallHeight;
        settingsAsset.innerWallHeight = innerWallHeight;
        settingsAsset.marbleStartHeight = marbleStartHeight;
        settingsAsset.curveSampleDistance = curveSampleDistance;
        settingsAsset.simplifyFloorContours = simplifyFloorContours;
        settingsAsset.floorSimplifyTolerance = floorSimplifyTolerance;
        settingsAsset.fitToTargetSize = fitToTargetSize;
        settingsAsset.targetWorldWidth = targetWorldWidth;
        settingsAsset.applyPreRotation = applyPreRotation;
        settingsAsset.preRotationYDegrees = preRotationYDegrees;
        settingsAsset.mirrorX = mirrorX;
        settingsAsset.mirrorZ = mirrorZ;

        // Frames / Rig
        settingsAsset.createTiltRig = createTiltRig;
        settingsAsset.frameThickness = frameThickness;
        settingsAsset.frameHeight = frameHeight;
        settingsAsset.frameGap = frameGap;
        settingsAsset.frameBoardClearance = frameBoardClearance;
        settingsAsset.frameMatchWallHeight = frameMatchWallHeight;
        settingsAsset.hingeRadius = hingeRadius;

        // Helpers / Triggers
        settingsAsset.createMeshHelpers = createMeshHelpers;
        settingsAsset.createHoleTrigger = createHoleTrigger;
        settingsAsset.holeTriggerHeight = holeTriggerHeight;

        // Materials
        settingsAsset.floorMaterial = floorMaterial;
        settingsAsset.wallMaterial = wallMaterial;
        settingsAsset.frameMaterial = frameMaterial;
        settingsAsset.hingeMaterial = hingeMaterial;
        settingsAsset.startMarkerMaterial = startMarkerMaterial;
        settingsAsset.endMarkerMaterial = endMarkerMaterial;
        settingsAsset.marbleMarkerMaterial = marbleMarkerMaterial;
        settingsAsset.trackingMarkerMaterial = trackingMarkerMaterial;
        settingsAsset.holeTriggerMaterial = holeTriggerMaterial;
        settingsAsset.endTriggerMaterial = endTriggerMaterial;
        settingsAsset.meshHelperMaterial = meshHelperMaterial;

        // Physic materials
        settingsAsset.floorPhysMaterial = floorPhysMaterial;
        settingsAsset.wallPhysMaterial = wallPhysMaterial;
        settingsAsset.marblePhysMaterial = marblePhysMaterial;
        settingsAsset.framePhysMaterial = framePhysMaterial;

        // Marble physics
        settingsAsset.marbleMass = marbleMass;
        settingsAsset.marbleDrag = marbleDrag;
        settingsAsset.marbleAngularDrag = marbleAngularDrag;

        // Agent
        settingsAsset.agentMaxStep = agentMaxStep;
        // Persist shared tilt settings
        settingsAsset.tiltSpeed = agentTiltSpeed;
        settingsAsset.maxTilt = agentMaxTilt;
        settingsAsset.agentKNearestHoles = agentKNearestHoles;
        settingsAsset.agentRayHeight = agentRayHeight;
        settingsAsset.agentClearanceFactor = agentClearanceFactor;
        // New agent fields
        settingsAsset.agentMilestoneBins = agentMilestoneBins;
        settingsAsset.agentMilestoneBonus = agentMilestoneBonus;
        settingsAsset.agentRayCount = agentRayCount;
        settingsAsset.agentMaxDistance = agentMaxDistance;

        // BehaviorParameters
        settingsAsset.behaviorName = behaviorName;
        settingsAsset.behaviorTeamId = behaviorTeamId;
        settingsAsset.behaviorType = behaviorType;
        settingsAsset.useChildSensors = useChildSensors;
        settingsAsset.vectorObservationSize = vectorObservationSize;
        settingsAsset.continuousActionSize = continuousActionSize;
        settingsAsset.stackedVectors = stackedVectors;
        settingsAsset.decisionPeriod = decisionPeriod;
        settingsAsset.takeActionsBetweenDecisions = takeActionsBetweenDecisions;
        settingsAsset.behaviorModel = behaviorModel;
        settingsAsset.inferenceDevice = inferenceDevice;

        // Features
        settingsAsset.enableAgent = enableAgent;
        settingsAsset.enableTracking = enableTracking;

        // Tracking
        settingsAsset.trackingMode = trackingMode;
        settingsAsset.showTrackingFeedback = showTrackingFeedback;
        settingsAsset.trkAddHud = trkAddHud;
        settingsAsset.addRealHud = addRealHud;
        settingsAsset.trkShowDisplay = trkShowDisplay;
        settingsAsset.trackingMarkerScaleMultiplier = trackingMarkerScaleMultiplier;
        settingsAsset.trackingCamHeight = trackingCamHeight;
        settingsAsset.trackingCamPadding = trackingCamPadding;
        settingsAsset.trkPlaceTrackingMarkerOnFloor = trkPlaceTrackingMarkerOnFloor;
        settingsAsset.trkRestrictToFloorViewport = trkRestrictToFloorViewport;
        settingsAsset.trkClampToFloorBounds = trkClampToFloorBounds;
        settingsAsset.trkSampleWidth = trkSampleWidth;
        settingsAsset.trkSampleHeight = trkSampleHeight;
        settingsAsset.trkSampleEveryNth = trkSampleEveryNth;
        settingsAsset.trkTargetTrackingFps = trkTargetTrackingFps;
        settingsAsset.trkMinPixelCount = trkMinPixelCount;
        settingsAsset.trkSmoothing = trkSmoothing;
        settingsAsset.trkSearchAroundLast = trkSearchAroundLast;
        settingsAsset.trkRoiRadiusViewport = trkRoiRadiusViewport;
        settingsAsset.trkEnablePrediction = trkEnablePrediction;
        settingsAsset.trkMaxMissedFrames = trkMaxMissedFrames;
        settingsAsset.trkVelocityDamping = trkVelocityDamping;
        settingsAsset.trkMissesBeforeFullScan = trkMissesBeforeFullScan;
        settingsAsset.trkRoiExpandFactorOnMiss = trkRoiExpandFactorOnMiss;
        settingsAsset.trkFloorViewportPadding = trkFloorViewportPadding;
        settingsAsset.trkTrackingMarkerHeightOffset = trkTrackingMarkerHeightOffset;
        settingsAsset.trkTargetColor = trkTargetColor;
        settingsAsset.trkColorTolerance = trkColorTolerance;

        // Color Mapping
        settingsAsset.colorFloor = colorFloor;
        settingsAsset.colorWall = colorWall;
        settingsAsset.colorStart = colorStart;
        settingsAsset.colorEnd = colorEnd;
        settingsAsset.colorMarble = colorMarble;
        settingsAsset.colorEndTrigger = colorEndTrigger;

        EditorUtility.SetDirty(settingsAsset);
        AssetDatabase.SaveAssets();
    }

	private static bool ColorsApproximatelyEqual(Color a, Color b, float tolerance = 0.01f)
	{
		// Compare RGB only and ignore alpha to be resilient to SVG fill opacity
		var av = new Vector3(a.r, a.g, a.b);
		var bv = new Vector3(b.r, b.g, b.b);
		return Vector3.Distance(av, bv) < tolerance;
	}

    /// <summary>
    /// Sets common renderer probe defaults suitable for static level geometry.
    /// </summary>
    private static void ConfigureRendererProbeDefaults(Renderer renderer)
    {
        if (renderer == null) return;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
        renderer.allowOcclusionWhenDynamic = true;
    }

    /// <summary>
    /// Maps a fill color to a semantic marker type using configurable tolerances.
    /// </summary>
    private MarkerType GetMarkerType(Color color)
    {
        if (ColorsApproximatelyEqual(color, colorStart, 0.05f)) return MarkerType.Start;
        if (ColorsApproximatelyEqual(color, colorEnd, 0.05f)) return MarkerType.End;
        if (ColorsApproximatelyEqual(color, colorMarble, 0.05f)) return MarkerType.Marble;
        if (ColorsApproximatelyEqual(color, colorEndTrigger, 0.05f)) return MarkerType.EndTrigger;
        return MarkerType.None;
    }

    /// <summary>
    /// Returns true if the color matches the configured floor color.
    /// </summary>
    private bool IsFloorColor(Color color)
    {
        return ColorsApproximatelyEqual(color, colorFloor, 0.05f);
    }

    private static void ConfigureMeshColliderCookingAllOptions(MeshCollider mc)
    {
#if UNITY_2019_4_OR_NEWER
        if (mc == null) return;
        MeshColliderCookingOptions allFlags = (MeshColliderCookingOptions)0;
        foreach (MeshColliderCookingOptions v in System.Enum.GetValues(typeof(MeshColliderCookingOptions)))
        {
            allFlags |= v;
        }
        mc.cookingOptions = allFlags;
#endif
    }

    /// <summary>
    /// Adds and configures a MeshCollider using the provided mesh.
    /// </summary>
    private static void AddSharedMeshCollider(GameObject target, Mesh mesh, bool makeConvex = false)
    {
        if (target == null || mesh == null) return;
        var collider = target.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = makeConvex;
        ConfigureMeshColliderCookingAllOptions(collider);
    }

    /// <summary>
    /// Adds a NavMeshModifier with an overridden area, applied to children.
    /// </summary>
    private static NavMeshModifier AddNavMeshModifier(GameObject target, int area)
    {
        if (target == null) return null;
        var modifier = target.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = area;
        modifier.applyToChildren = true;
        return modifier;
    }

   
    /// <summary>
    /// Ensures a tag exists in the project TagManager (editor-only).
    /// </summary>
    private static void EnsureTagExists(string desiredTag)
    {
        if (string.IsNullOrEmpty(desiredTag)) return;
        try
        {
            if (System.Array.IndexOf(InternalEditorUtility.tags, desiredTag) >= 0) return;
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var tagsProp = so.FindProperty("tags");
            if (tagsProp == null) return;
            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                var elem = tagsProp.GetArrayElementAtIndex(i);
                if (elem != null && elem.stringValue == desiredTag) return;
            }
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            var newElem = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
            newElem.stringValue = desiredTag;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"EnsureTagExists failed for tag '{desiredTag}': {ex.Message}");
        }
    }

    /// <summary>
    /// Collects renderers considered part of the playable area (floor and walls).
    /// </summary>
    private static List<Renderer> GetLevelRenderers(Transform root)
    {
        if (root == null) return new List<Renderer>();
        return root.GetComponentsInChildren<Renderer>()
            .Where(r => r.gameObject.name.StartsWith("Wall") || r.gameObject.name == "OuterWall" || r.gameObject.name.StartsWith("Floor"))
            .ToList();
    }

    

    /// <summary>
    /// Repositions children so the root pivot sits above the XZ center without changing world layout.
    /// </summary>
    private static void RecenterPivotXZKeepingWorld(Transform root, List<Renderer> renderers)
    {
        if (root == null || renderers == null || renderers.Count == 0) return;
        var totalBounds = BoundsUtils.CalculateCombinedBounds(renderers);
        Vector3 centerWorld = totalBounds.center;
        Vector3 desiredPivotWorld = new Vector3(centerWorld.x, root.position.y, centerWorld.z);
        Vector3 delta = desiredPivotWorld - root.position;
        Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
        foreach (Transform child in root)
        {
            child.position -= deltaXZ;
        }
        root.position += deltaXZ;
    }

    /// <summary>
    /// Creates an end trigger sphere at the given 2D center with the provided diameter.
    /// </summary>
    private GameObject CreateEndTrigger(Transform parent, Vector2 center, float diameter, Material mat)
    {
        var triggerGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        triggerGO.name = NAME_END_TRIGGER;
        triggerGO.transform.SetParent(parent, false);
        triggerGO.transform.position = new Vector3(center.x, floorHeight + 0.01f, center.y);
        triggerGO.transform.localScale = new Vector3(diameter, diameter, diameter);
        var sphere = triggerGO.GetComponent<SphereCollider>();
        if (sphere != null) sphere.isTrigger = true;
        var rend = triggerGO.GetComponent<Renderer>();
        if (rend != null)
        {
            if (mat != null) rend.sharedMaterial = mat;
            ConfigureRendererProbeDefaults(rend);
        }
        triggerGO.AddComponent<GoalTrigger>();
        try
        {
            EnsureTagExists("GoalTrigger");
            if (System.Array.IndexOf(InternalEditorUtility.tags, "GoalTrigger") >= 0)
            {
                triggerGO.tag = "GoalTrigger";
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Assigning GoalTrigger tag failed: {ex.Message}");
        }
        return triggerGO;
    }
   
    /// <summary>
    /// Extracts the solid fill color of a shape; returns clear if not applicable.
    /// </summary>
    private static Color GetShapeFillColor(Shape shape)
    {
        if (shape == null) return Color.clear;
        if (shape.Fill is SolidFill solid) return solid.Color;
        return Color.clear;
    }

   
    private List<List<Vector2>> SampleAllContours(Shape shape, float targetDistance)
    {
        return SVGShapeSampler.SampleAllContours(shape, targetDistance);
    }

   
   
    private List<List<Vector2>> PrepareContoursForFloorExtrusion(List<List<Vector2>> allContours)
    {
        return SVGShapeSampler.PrepareContoursForFloorExtrusion(allContours, simplifyFloorContours, floorSimplifyTolerance);
    }

   
    

    // --- Geometry helpers to detect the outer wall by area ---
    /// <summary>
    /// Signed polygon area (positive for CCW) using the shoelace formula.
    /// </summary>
    private static float PolygonSignedArea(List<Vector2> points)
    {
        if (points == null || points.Count < 3) return 0f;
        double sum = 0d;
        for (int i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += (double)a.x * b.y - (double)b.x * a.y;
        }
        return (float)(0.5d * sum);
    }

    /// <summary>
    /// Sums absolute areas across contours.
    /// </summary>
    private static float ComputeTotalAbsArea(List<List<Vector2>> allContours)
    {
        if (allContours == null || allContours.Count == 0) return 0f;
        float area = 0f;
        for (int i = 0; i < allContours.Count; i++)
        {
            area += Mathf.Abs(PolygonSignedArea(allContours[i]));
        }
        return area;
    }

   
    /// <summary>
    /// Assigns the GameObject to the 'Wall' layer when appropriate.
    /// </summary>
    private static void EnsureWallLayer(GameObject go, bool isFloor)
    {
        if (go == null || isFloor) return;
        int wallLayerIndex = LayerMask.NameToLayer("Wall");
        if (wallLayerIndex != -1) go.layer = wallLayerIndex;
    }

   
    /// <summary>
    /// Creates small debug spheres for each inner hole contour.
    /// </summary>
    private void CreateMeshHelpersForHoles(GameObject rootGO, List<List<Vector2>> allContours, float baseY, ref GameObject meshHelpersRoot, ref int meshHelperIndex)
    {
        if (allContours == null || allContours.Count <= 1) return;
        if (meshHelpersRoot == null)
        {
            meshHelpersRoot = new GameObject(NAME_MESH_HELPER);
            meshHelpersRoot.transform.parent = rootGO.transform;
            // Centralize NavMesh area control on the group root for consistency
            var holesGroupModifier = AddNavMeshModifier(meshHelpersRoot, AREA_NOT_WALKABLE);
        }

        for (int j = 1; j < allContours.Count; j++)
        {
            var contour = allContours[j];
            if (contour == null || contour.Count == 0) continue;

            float minX = contour[0].x, maxX = contour[0].x;
            float minZ = contour[0].y, maxZ = contour[0].y;
            for (int k = 1; k < contour.Count; k++)
            {
                var p = contour[k];
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minZ) minZ = p.y;
                if (p.y > maxZ) maxZ = p.y;
            }

            Vector3 center = new Vector3((minX + maxX) * 0.5f, baseY + 0f, (minZ + maxZ) * 0.5f);
            if (applyPreRotation)
            {
                var rot = Quaternion.Euler(0f, preRotationYDegrees, 0f);
                center = rot * center;
            }
            float width = Mathf.Max(0.0001f, maxX - minX);
            float depth = Mathf.Max(0.0001f, maxZ - minZ);
            float diameter = Mathf.Max(0.0001f, Mathf.Min(width, depth));

            var helper = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Undo.RegisterCreatedObjectUndo(helper, "Create Mesh Helper");
            helper.name = $"{LABEL_HOLE_PREFIX}{++meshHelperIndex}";
            helper.transform.SetParent(meshHelpersRoot.transform, false);
            helper.transform.position = center;
            helper.transform.localScale = Vector3.one * diameter;
            helper.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            EnsureTagExists(TAG_HOLE);
            helper.tag = TAG_HOLE;

            var helperRenderer = helper.GetComponent<MeshRenderer>();
            if (helperRenderer != null && meshHelperMaterial != null)
            {
                helperRenderer.sharedMaterial = meshHelperMaterial;
            }
            ConfigureRendererProbeDefaults(helperRenderer);

            var sphereCol = helper.GetComponent<SphereCollider>();
            if (sphereCol == null) sphereCol = helper.AddComponent<SphereCollider>();
            sphereCol.isTrigger = false;
            sphereCol.enabled = false;

            // NavMeshModifier is applied on the group root with applyToChildren=true
        }
    }

   
   
    /// <summary>
    /// Places start/end markers, marble, and optional end trigger based on SVG colors.
    /// </summary>
    private void PlaceMarkers(Unity.VectorGraphics.Scene vgScene, GameObject rootGO, out GameObject marbleGO, out GameObject endMarkerGO)
    {
        marbleGO = null;
        endMarkerGO = null;
        var rot = applyPreRotation ? Quaternion.Euler(0f, preRotationYDegrees, 0f) : Quaternion.identity;
        foreach (var node in vgScene.Root.Children)
        {
            if (node.Shapes == null || node.Shapes.Count == 0) continue;
            var shape = node.Shapes[0];
            if (shape.Contours == null || shape.Contours.Count() == 0 || shape.Contours[0].Segments == null)
            {
                continue;
            }
            var color = GetShapeFillColor(shape);
            var markerType = GetMarkerType(color);
            if (markerType == MarkerType.None) continue;

            var bounds = VectorUtils.Bounds(shape.Contours[0].Segments);
            Vector2 center2 = bounds.center;
            float width2 = bounds.size.x;
            float height2 = bounds.size.y;
            Vector3 worldPos;
            GameObject marker = null;

            switch (markerType)
            {
                case MarkerType.Marble:
                {
                    // Interpret marbleStartHeight as a distance above the actual floor top in world space
                    // Fallback uses bounds.min.y + floorHeight in case renderer lookup fails
                    float floorTopY = bounds.min.y + floorHeight;
                    var floorTrLookup = rootGO.transform.Find(LABEL_FLOOR);
                    if (floorTrLookup != null)
                    {
                        var floorRenderer = floorTrLookup.GetComponent<Renderer>();
                        if (floorRenderer != null)
                        {
                            floorTopY = floorRenderer.bounds.max.y;
                        }
                    }
                    // Add the marble radius so a value of 0 sits the marble on the floor
                    worldPos = new Vector3(center2.x, floorTopY + marbleStartHeight + (0.5f * width2), center2.y);
                    if (applyPreRotation) worldPos = rot * worldPos;
                    marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    marker.transform.localScale = new Vector3(width2, width2, height2);
                    var marbleRenderer = marker.GetComponent<Renderer>();
                    if (marbleRenderer != null && marbleMarkerMaterial != null) marbleRenderer.sharedMaterial = marbleMarkerMaterial;

                    var rb = marker.AddComponent<Rigidbody>();
                    rb.mass = marbleMass;
                    rb.drag = marbleDrag;
                    rb.angularDrag = marbleAngularDrag;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    // Re-enable interpolation for smoother visual response of the marble
                    rb.interpolation = RigidbodyInterpolation.Interpolate;

                    var col = marker.GetComponent<Collider>();
                    if (col != null) col.material = marblePhysMaterial;

                    marbleGO = marker;
                    break;
                }
                case MarkerType.EndTrigger:
                {
                    float triggerDiameter = Mathf.Min(width2, height2);
                    var matToUse = endTriggerMaterial != null ? endTriggerMaterial : endMarkerMaterial;
                    if (applyPreRotation)
                    {
                        var rotatedCenter3 = rot * new Vector3(center2.x, 0f, center2.y);
                        var rotatedCenter2 = new Vector2(rotatedCenter3.x, rotatedCenter3.z);
                        CreateEndTrigger(rootGO.transform, rotatedCenter2, triggerDiameter, matToUse);
                    }
                    else
                    {
                        CreateEndTrigger(rootGO.transform, center2, triggerDiameter, matToUse);
                    }
                    continue;
                }
                case MarkerType.Start:
                case MarkerType.End:
                {
                    worldPos = new Vector3(center2.x, floorHeight + 0.01f, center2.y);
                    if (applyPreRotation) worldPos = rot * worldPos;
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    marker.transform.localScale = new Vector3(width2, 0.01f, height2);
                    var renderer = marker.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        if (markerType == MarkerType.Start && startMarkerMaterial != null)
                            renderer.sharedMaterial = startMarkerMaterial;
                        else if (markerType == MarkerType.End && endMarkerMaterial != null)
                            renderer.sharedMaterial = endMarkerMaterial;
                    }

                    var markerCollider = marker.GetComponent<Collider>();
#if UNITY_EDITOR
                    if (markerCollider != null) DestroyImmediate(markerCollider);
#else
                    if (markerCollider != null) Destroy(markerCollider);
#endif

                    if (markerType == MarkerType.End)
                    {
                        bool hasExplicitEndTrigger = vgScene.Root.Children.Any(n =>
                        {
                            if (n.Shapes == null || n.Shapes.Count == 0) return false;
                            var s = n.Shapes[0];
                            Color c = GetShapeFillColor(s);
                            return GetMarkerType(c) == MarkerType.EndTrigger;
                        });
                        if (!hasExplicitEndTrigger)
                        {
                            float triggerDiameter = Mathf.Min(width2, height2) * 0.6f;
                            var matToUse = endTriggerMaterial != null ? endTriggerMaterial : endMarkerMaterial;
                            CreateEndTrigger(rootGO.transform, center2, triggerDiameter, matToUse);
                        }
                    }
                    break;
                }
                default:
                    continue;
            }

            string markerName = markerType switch
            {
                MarkerType.End => NAME_END_MARKER,
                MarkerType.Start => NAME_START_MARKER,
                MarkerType.Marble => NAME_MARBLE_MARKER,
                _ => NAME_MARBLE_MARKER
            };
            marker.name = markerName;
            marker.transform.position = worldPos;
            marker.transform.parent = rootGO.transform;
            if (markerType == MarkerType.End) endMarkerGO = marker;
        }
    }

   
    /// <summary>
    /// Applies mirroring options to the transform scale (pre-rotation is baked elsewhere).
    /// </summary>
    private void ApplyTransformOptions(Transform t)
    {
        if (t == null) return;
        // Pre-rotation is baked into meshes/positions to keep Inspector rotation at 0°.
        Vector3 s = t.localScale;
        s.x = mirrorX ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
        s.z = mirrorZ ? -Mathf.Abs(s.z) : Mathf.Abs(s.z);
        t.localScale = s;
    }

   
    /// <summary>
    /// Optionally scales the level to a target width and recenters its pivot in XZ.
    /// </summary>
    private void FitToTargetSizeAndRecenter(Transform root)
    {
        var renderersToConsider = GetLevelRenderers(root);
        if (renderersToConsider.Count > 0)
        {
            var totalBounds = BoundsUtils.CalculateCombinedBounds(renderersToConsider);
            if (fitToTargetSize)
            {
                float currentWidth = totalBounds.size.x;
                if (currentWidth > 0.0001f)
                {
                    float uniformScale = targetWorldWidth / currentWidth;
                   
                    root.localScale = root.localScale * uniformScale;
                }
            }
        }

        renderersToConsider = GetLevelRenderers(root);
        if (renderersToConsider.Count > 0)
        {
            RecenterPivotXZKeepingWorld(root, renderersToConsider);
        }
    }

   
    /// <summary>
    /// Creates a catch trigger box under the floor sized to the board footprint.
    /// </summary>
    private void CreateHoleTriggerBelowFloor(Transform finalRoot)
    {
        var renderers = GetLevelRenderers(finalRoot);
        if (renderers.Count == 0) return;

        var levelBounds = BoundsUtils.CalculateCombinedBounds(renderers);

        var holeTrigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(holeTrigger, "Create Hole Trigger");
        holeTrigger.name = NAME_HOLE_TRIGGER;
        holeTrigger.transform.SetParent(finalRoot, false);

        // Convert board-unit height to world space using the level's current uniform scale
        float scaleY = Mathf.Abs(finalRoot.lossyScale.y);
        float worldY = Mathf.Max(0.001f, holeTriggerHeight * (Mathf.Approximately(scaleY, 0f) ? 1f : scaleY));

        var floorRenderer = renderers.FirstOrDefault(r => r.gameObject.name == LABEL_FLOOR);
        var xzBounds = floorRenderer != null ? floorRenderer.bounds : levelBounds;

        float inset = 0.002f * Mathf.Max(xzBounds.size.x, xzBounds.size.z);

        var parentScale = finalRoot.lossyScale;
        float worldX = Mathf.Max(0.001f, xzBounds.size.x - inset);
        float worldZ = Mathf.Max(0.001f, xzBounds.size.z - inset);
        float denomX = Mathf.Approximately(parentScale.x, 0f) ? 1f : Mathf.Abs(parentScale.x);
        float denomY = Mathf.Approximately(parentScale.y, 0f) ? 1f : Mathf.Abs(parentScale.y);
        float denomZ = Mathf.Approximately(parentScale.z, 0f) ? 1f : Mathf.Abs(parentScale.z);
        float signX = Mathf.Sign(parentScale.x);
        float signY = Mathf.Sign(parentScale.y);
        float signZ = Mathf.Sign(parentScale.z);
        holeTrigger.transform.localScale = new Vector3(
            (worldX / denomX) * signX,
            (worldY / denomY) * signY,
            (worldZ / denomZ) * signZ
        );
        float localYOffset = Mathf.Approximately(parentScale.y, 0f)
            ? (-worldY * 0.5f - 0.001f)
            : (-worldY * 0.5f - 0.001f) / parentScale.y;
        holeTrigger.transform.localPosition = new Vector3(0f, localYOffset, 0f);

        var boxCollider = holeTrigger.GetComponent<BoxCollider>();
        if (boxCollider != null) boxCollider.isTrigger = true;
        holeTrigger.AddComponent<HoleTrigger>();

        var rend = holeTrigger.GetComponent<Renderer>();
        if (rend != null && holeTriggerMaterial != null)
        {
            rend.sharedMaterial = holeTriggerMaterial;
        }
    }

   
    /// <summary>
    /// Adds a <see cref="NavMeshSurface"/> configured for the board.
    /// </summary>
    private void CreateNavMeshSurface(Transform finalRoot)
    {
        var navSurfaceGO = new GameObject(NAME_NAVMESH_SURFACE);
        navSurfaceGO.transform.SetParent(finalRoot, false);
        var surface = navSurfaceGO.AddComponent<NavMeshSurface>();

        try
        {
            int selectedId = surface.agentTypeID;
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                var settings = NavMesh.GetSettingsByIndex(i);
                var name = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
                if (!string.IsNullOrEmpty(name) && name == "Marble")
                {
                    selectedId = settings.agentTypeID;
                    break;
                }
            }
            surface.agentTypeID = selectedId;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"NavMesh agent type selection failed: {ex.Message}");
        }

        surface.defaultArea = AREA_WALKABLE;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
#if UNITY_2022_2_OR_NEWER
        surface.collectObjects = CollectObjects.MarkedWithModifier;
#else
        surface.collectObjects = CollectObjects.All;
#endif
        surface.layerMask = ~0;
        surface.ignoreNavMeshAgent = true;
        surface.ignoreNavMeshObstacle = true;
        surface.overrideTileSize = false;
        // Let Unity derive voxel size from the agent settings (do not override)
        surface.overrideVoxelSize = false;
        surface.minRegionArea = 2.0f;
        surface.buildHeightMesh = false;

        surface.BuildNavMesh();
    }

   
    /// <summary>
    /// Adds and configures <see cref="BoardAgent"/>, ML-Agents components, and DecisionRequester.
    /// </summary>
    private void ConfigureAgentAndML(GameObject finalRoot, GameObject marbleGO, GameObject endMarkerGO)
    {
        BoardAgent agent = null;
        if (enableAgent)
        {
            agent = finalRoot.GetComponent<BoardAgent>();
            if (agent == null) agent = finalRoot.AddComponent<BoardAgent>();

            if (marbleGO == null)
            {
                var marbleTransform = finalRoot.transform.Find(NAME_MARBLE_MARKER);
                if (marbleTransform) marbleGO = marbleTransform.gameObject;
            }
            if (marbleGO != null)
            {
                agent.marble = marbleGO.transform;
            }

            if (endMarkerGO == null)
            {
                var endMarkerTransform = finalRoot.transform.Find(NAME_END_MARKER);
                if (endMarkerTransform) endMarkerGO = endMarkerTransform.gameObject;
            }
            if (endMarkerGO != null) agent.goal = endMarkerGO.transform;

            // Assign floor renderer so the agent can derive board bounds
            try
            {
                var floorTr = finalRoot.transform.Find(LABEL_FLOOR);
                if (floorTr != null)
                {
                    var fr = floorTr.GetComponent<MeshRenderer>();
                    if (fr != null)
                    {
                        var soA = new UnityEditor.SerializedObject(agent);
                        var floorProp = soA.FindProperty("floorRenderer");
                        if (floorProp != null)
                        {
                            floorProp.objectReferenceValue = fr;
                            soA.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Assigning floorRenderer to BoardAgent failed: {ex.Message}");
            }

            // Basic parameters
            agent.MaxStep = agentMaxStep;
            agent.tiltSpeed = agentTiltSpeed;
            agent.maxTilt = agentMaxTilt;
            agent.kNearestHoles = agentKNearestHoles;
            // Newly exposed runtime-settable fields
            agent.rayCount = agentRayCount;
            agent.maxDistance = agentMaxDistance;

            // Serialized tuning
            var so = new UnityEditor.SerializedObject(agent);
            var rayHeightProp = so.FindProperty("rayHeight");
            var obstacleMaskProp = so.FindProperty("obstacleMask");
            var clearanceFactorProp = so.FindProperty("clearanceFactor");
            var milestoneBinsProp = so.FindProperty("milestoneBins");
            var milestoneBonusProp = so.FindProperty("milestoneBonus");
            if (rayHeightProp != null) rayHeightProp.floatValue = agentRayHeight;
            int wallLayerIdx = LayerMask.NameToLayer("Wall");
#if UNITY_EDITOR
            if (wallLayerIdx == -1)
            {
                wallLayerIdx = TrackingSetupService.EnsureLayerExistsEditor("Wall");
            }
#endif
            if (obstacleMaskProp != null)
            {
                if (wallLayerIdx != -1)
                {
                    obstacleMaskProp.intValue = 1 << wallLayerIdx;
                }
                else
                {
                    Debug.LogWarning("BoardAgent obstacleMask could not be set: Layer 'Wall' not found.");
                }
            }
            if (clearanceFactorProp != null) clearanceFactorProp.floatValue = agentClearanceFactor;
            if (milestoneBinsProp != null) milestoneBinsProp.intValue = agentMilestoneBins;
            if (milestoneBonusProp != null) milestoneBonusProp.floatValue = agentMilestoneBonus;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.EditorUtility.SetDirty(agent);

            // Ensure TiltController (rig) is enabled so Z-tilt is applied via the gimbal
            try
            {
                var tc = UnityEngine.Object.FindObjectOfType<TiltController>();
                if (tc != null) tc.enabled = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Enabling TiltController failed: {ex.Message}");
            }
        }

        if (enableAgent)
        {
            var bp = finalRoot.GetComponent<BehaviorParameters>();
            if (bp == null) bp = finalRoot.AddComponent<BehaviorParameters>();
            bp.BehaviorName = behaviorName;
            bp.TeamId = behaviorTeamId;
            bp.BehaviorType = behaviorType;
            bp.UseChildSensors = useChildSensors;
            bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(continuousActionSize);
            try { bp.Model = behaviorModel; }
            catch (System.Exception ex) { Debug.LogWarning($"Assigning NN model failed: {ex.Message}"); }
            try { bp.InferenceDevice = inferenceDevice; }
            catch (System.Exception ex) { Debug.LogWarning($"Setting inference device failed: {ex.Message}"); }

            // Compute observation size for BoardAgent to match its CollectObservations
            try
            {
                int obsSize = 0;
                // marble(2), goal(2), tilt(2), holes(2*k), guidance(3), pathProgress(1), rays(rayCount)
                int k = agent != null ? agent.kNearestHoles : agentKNearestHoles;
                int rays = agent != null ? agent.rayCount : 8;
                obsSize = 2 + 2 + 2 + (2 * k) + 3 + 1 + rays;
                bp.BrainParameters.VectorObservationSize = obsSize;
            }
            catch
            {
                bp.BrainParameters.VectorObservationSize = vectorObservationSize;
            }

            // Set stacked vectors
            try { bp.BrainParameters.NumStackedVectorObservations = stackedVectors; }
            catch { }

            var dr = finalRoot.GetComponent<DecisionRequester>();
            if (dr == null) dr = finalRoot.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = decisionPeriod;
            dr.TakeActionsBetweenDecisions = takeActionsBetweenDecisions;
        }
    }

    /// <summary>
    /// Builds the visual gimbal frames and cardanic pivots around the board and wires the controller.
    /// </summary>
    private Transform BuildTiltRigAndFrames(Transform levelRoot)
    {
        if (levelRoot == null) return null;

        var renderers = GetLevelRenderers(levelRoot);
        if (renderers == null || renderers.Count == 0) return null;
        var bounds = BoundsUtils.CalculateCombinedBounds(renderers);

        // Create rig with dedicated rotation pivots to avoid mesh/scale side-effects:
        // TiltRig -> OuterPivot(Z) -> OuterFrame(beams)
        //         -> InnerPivot(X) -> InnerFrame(beams) -> Level
        var rigGo = new GameObject("TiltRig");
        var outerPivot = new GameObject("OuterPivot");
        var outerFrameGroup = new GameObject("OuterFrame");
        var innerPivot = new GameObject("InnerPivot");
        var innerFrameGroup = new GameObject("InnerFrame");
        rigGo.transform.position = bounds.center;
        outerPivot.transform.SetParent(rigGo.transform, false);
        // Cardanic joint: InnerPivot is child of OuterPivot (inherits Z, provides X)
        innerPivot.transform.SetParent(outerPivot.transform, false);
        outerFrameGroup.transform.SetParent(outerPivot.transform, false);
        innerFrameGroup.transform.SetParent(innerPivot.transform, false);

        // Cardanic: Board under InnerPivot so it inherits both rotations via the joint
        levelRoot.SetParent(innerPivot.transform, true);

        // Build two rectangular rings (outer, inner) as four thin boxes each
        float t = Mathf.Max(0.0005f, frameThickness);
        float h = Mathf.Max(0.001f, frameMatchWallHeight ? wallHeight : frameHeight);
        float g = Mathf.Max(0f, frameGap);
        float w = bounds.size.x;
        float d = bounds.size.z;
        // Put frames exactly on the floor top so they are not hidden inside the board
        float floorTopY = bounds.min.y + floorHeight; // fallback in case renderer lookup fails
        var floorTr = levelRoot.Find(LABEL_FLOOR);
        if (floorTr != null)
        {
            var r = floorTr.GetComponent<Renderer>();
            if (r != null) floorTopY = r.bounds.max.y;
        }
        float yBase = floorTopY + 0.0005f; // tiny offset to avoid Z-fighting

        // If matching wall height, derive the wall height in WORLD space from actual wall renderers
        if (frameMatchWallHeight)
        {
            Transform wallsT = levelRoot.Find("Walls");
            if (wallsT != null)
            {
                var wallRenderers = wallsT.GetComponentsInChildren<Renderer>();
                if (wallRenderers != null && wallRenderers.Length > 0)
                {
                    float maxWallTop = float.MinValue;
                    for (int i = 0; i < wallRenderers.Length; i++)
                    {
                        if (wallRenderers[i] == null) continue;
                        var b = wallRenderers[i].bounds;
                        if (b.max.y > maxWallTop) maxWallTop = b.max.y;
                    }
                    if (maxWallTop > float.MinValue)
                    {
                        h = Mathf.Max(0.001f, maxWallTop - floorTopY);
                    }
                }
            }
        }

        System.Action<Transform, string, Vector3, Vector3> makeBeam = (parent, name, localPos, localScale) =>
        {
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(beam, "Create Frame Beam");
            beam.name = name;
            beam.transform.SetParent(parent, false);
            // local position relative to rig center
            beam.transform.localPosition = new Vector3(localPos.x, (yBase - bounds.center.y) + h * 0.5f, localPos.z);
            beam.transform.localScale = new Vector3(localScale.x, h, localScale.z);
            var col = beam.GetComponent<BoxCollider>();
            // Remove frame colliders entirely; frames are visuals only
            if (col != null)
            {
#if UNITY_EDITOR
                UnityEngine.Object.DestroyImmediate(col);
#else
                UnityEngine.Object.Destroy(col);
#endif
            }
            var mr = beam.GetComponent<MeshRenderer>();
            if (mr != null && frameMaterial != null) mr.sharedMaterial = frameMaterial;
            ConfigureRendererProbeDefaults(mr);
            EnsureWallLayer(beam, false);
        };

        // Outer ring: construct like a thick outer wall with thickness t around the board
        float halfW = w * 0.5f;
        float halfD = d * 0.5f;
        // Build outward from the game area edge: [board edge] --clearance/outset--> [inner ring] --gap--> [outer ring]
        float gap = Mathf.Max(0f, frameGap);
        float clearance = Mathf.Max(0f, frameBoardClearance);

        // Inner ring centers: start from board edge, move outward by (clearance + gap + half thickness)
        float xi0 = (-halfW) - (clearance + gap + t * 0.5f);
        float xi1 = ( halfW) + (clearance + gap + t * 0.5f);
        float zi0 = (-halfD) - (clearance + gap + t * 0.5f);
        float zi1 = ( halfD) + (clearance + gap + t * 0.5f);
        float innerLenX = Mathf.Max(0.001f, (xi1 - xi0) + t);
        float innerLenZ = Mathf.Max(0.001f, (zi1 - zi0) + t);

        // Outer ring centers: start from board edge, move outward by (clearance + 2*gap + 1.5*thickness)
        float xo0 = (-halfW) - (clearance + 2f * gap + 1.5f * t);
        float xo1 = ( halfW) + (clearance + 2f * gap + 1.5f * t);
        float zo0 = (-halfD) - (clearance + 2f * gap + 1.5f * t);
        float zo1 = ( halfD) + (clearance + 2f * gap + 1.5f * t);
        float outerLenX = Mathf.Max(0.001f, (xo1 - xo0) + t);
        float outerLenZ = Mathf.Max(0.001f, (zo1 - zo0) + t);
        makeBeam(outerFrameGroup.transform, "Left",  new Vector3(xo0, 0f, 0f), new Vector3(t, h, outerLenZ));
        makeBeam(outerFrameGroup.transform, "Right", new Vector3(xo1, 0f, 0f), new Vector3(t, h, outerLenZ));
        makeBeam(outerFrameGroup.transform, "Top",   new Vector3(0f, 0f, zo1), new Vector3(outerLenX, h, t));
        makeBeam(outerFrameGroup.transform, "Bottom",new Vector3(0f, 0f, zo0), new Vector3(outerLenX, h, t));

        // Inner ring: enforce exact visible gap 'g' between the outer ring's inner face and the inner ring's outer face.
        // Also move the inner ring slightly inward by 'frameBoardClearance' so it doesn't touch the maze walls.
        // Inner ring based on board edge + gap + clearance (computed above)
        makeBeam(innerFrameGroup.transform, "Left",  new Vector3(xi0, 0f, 0f), new Vector3(t, h, innerLenZ));
        makeBeam(innerFrameGroup.transform, "Right", new Vector3(xi1, 0f, 0f), new Vector3(t, h, innerLenZ));
        makeBeam(innerFrameGroup.transform, "Top",   new Vector3(0f, 0f, zi1), new Vector3(innerLenX, h, t));
        makeBeam(innerFrameGroup.transform, "Bottom",new Vector3(0f, 0f, zi0), new Vector3(innerLenX, h, t));

        // Attach/update Gimbal rig + controller (inner ring = X, outer ring = Z)
        var gimbal = rigGo.GetComponent<GimbalRig>();
        if (gimbal == null) gimbal = rigGo.AddComponent<GimbalRig>();
        gimbal.outerZ = outerPivot.transform; // rotate pivot, not the mesh group
        gimbal.innerX = innerPivot.transform; // rotate pivot, not the mesh group
        // Keep outer frame static; Z tilt is applied on the board itself via TiltController
        gimbal.freezeOuter = true;

        // Enforce single-axis rotation on pivots
        // Outermost frame static: remove lock and do not drive Z on outer pivot via controller
        // Keep a lock in case anything writes to Y; but Z remains at 0 by controller design
        var outerLock = outerPivot.AddComponent<AxisLock>();
        outerLock.freezeAll = true; // outermost ring static
        var innerLock = innerPivot.AddComponent<AxisLock>();
        innerLock.axis = AxisLock.Axis.X;
        // Use default gimbal axis mapping and signs from GimbalRig

        var controller = rigGo.GetComponent<TiltController>();
        if (controller == null) controller = rigGo.AddComponent<TiltController>();
        controller.rig = gimbal;
        controller.outerFrame = outerPivot.transform;
        controller.innerFrame = innerPivot.transform;
        controller.boardRoot = innerPivot.transform.Find(levelRoot.name);
        // Apply importer tilt limits and speed for both manual and agent-controlled modes
        controller.maxTilt = agentMaxTilt;
        controller.tiltSpeed = agentTiltSpeed;
        // Allow manual keyboard control if no agent is enabled; otherwise give ML exclusive control
        controller.keyboardInput = !enableAgent;
        controller.syncWithBoardRotation = false;

        // Visual hinge cylinders placed inside the ring gaps (do not cross the play area)
        {
            // Size the hinge bars to sit in the gap area only (four small bars)
            // Determine faces first
            float xLeftInnerFaceOuter = xo0 + t * 0.5f;  // inner face of outer-left beam
            float xLeftOuterFaceInner = xi0 - t * 0.5f;  // outer face of inner-left beam
            float xRightInnerFaceOuter = xo1 - t * 0.5f; // inner face of outer-right beam
            float xRightOuterFaceInner = xi1 + t * 0.5f; // outer face of inner-right beam
            // For top/bottom X-axis hinges we now place them BETWEEN the inner ring and the game area.
            // That gap is bounded by the board edge (±halfD) and the INNER face of the inner ring.
            float zTopInnerFaceInnerRing = zi1 - t * 0.5f;     // inner ring face closest to the board (top)
            float zBottomInnerFaceInnerRing = zi0 + t * 0.5f;  // inner ring face closest to the board (bottom)

            // Compute gaps and half-lengths exactly between faces
            float gapLeft = Mathf.Max(0.001f, xLeftOuterFaceInner - xLeftInnerFaceOuter);
            float gapRight = Mathf.Max(0.001f, xRightInnerFaceOuter - xRightOuterFaceInner);
            float axisLenZLeftHalf = gapLeft * 0.5f;
            float axisLenZRightHalf = gapRight * 0.5f;
            float gapBottom = Mathf.Max(0.001f, (-halfD) - zBottomInnerFaceInnerRing);
            float gapTop = Mathf.Max(0.001f, zTopInnerFaceInnerRing - halfD);
            float axisLenXBottomHalf = gapBottom * 0.5f;
            float axisLenXTopHalf = gapTop * 0.5f;
            
            const float lrRadiusScale = 1.5f;   // side bars thickness
            const float tbRadiusScale = 1.2f;   // top/bottom bars thickness

            // Exact radial midpoints between ring outer/inner faces so bars sit strictly between frames
            float xMidLeft = (xLeftInnerFaceOuter + xLeftOuterFaceInner) * 0.5f;
            float xMidRight = (xRightInnerFaceOuter + xRightOuterFaceInner) * 0.5f;
            float zMidTop = (zTopInnerFaceInnerRing + halfD) * 0.5f;
            float zMidBottom = (zBottomInnerFaceInnerRing + (-halfD)) * 0.5f;

            // Two Z-axis hinges (left/right), cylinder Y aligned to Z by RotX(90)
            System.Action<string, float, float> makeZCap = (name, xPos, halfLen) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(go, "Create Hinge");
                go.name = name;
                go.transform.SetParent(outerPivot.transform, false);
                // rotate cylinder Y->Z and twist by 90° around Y so caps face the frames
                go.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);
                go.transform.localPosition = new Vector3(xPos, (yBase - bounds.center.y), 0f);
                go.transform.localScale = new Vector3(hingeRadius * 2f * lrRadiusScale, halfLen, hingeRadius * 2f * lrRadiusScale);
                var r = go.GetComponent<MeshRenderer>(); if (r && hingeMaterial) r.sharedMaterial = hingeMaterial;
                var c = go.GetComponent<Collider>();
                if (c)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(c);
#else
                    UnityEngine.Object.Destroy(c);
#endif
                }
            };
            makeZCap("HingeZLeft", xMidLeft, axisLenZLeftHalf);
            makeZCap("HingeZRight", xMidRight, axisLenZRightHalf);

            // Two X-axis hinges (top/bottom), cylinder Y aligned to X by RotZ(90)
            System.Action<string, float, float> makeXCap = (name, zPos, halfLen) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Undo.RegisterCreatedObjectUndo(go, "Create Hinge");
                go.name = name;
                go.transform.SetParent(innerPivot.transform, false);
                go.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
                go.transform.localPosition = new Vector3(0f, (yBase - bounds.center.y), zPos);
                go.transform.localScale = new Vector3(hingeRadius * 2f * tbRadiusScale, halfLen, hingeRadius * 2f * tbRadiusScale);
                var r = go.GetComponent<MeshRenderer>(); if (r && hingeMaterial) r.sharedMaterial = hingeMaterial;
                var c = go.GetComponent<Collider>();
                if (c)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(c);
#else
                    UnityEngine.Object.Destroy(c);
#endif
                }
            };
            makeXCap("HingeXBottom", zMidBottom, axisLenXBottomHalf);
            makeXCap("HingeXTop", zMidTop, axisLenXTopHalf);
        }

        return rigGo.transform;
    }

    /// <summary>
    /// Renders the importer UI and handles the Import action.
    /// </summary>
    void OnGUI()
    {
        selectedTab = DrawTabsToolbar(selectedTab);
        if (!IsTabEnabled(selectedTab)) selectedTab = GetFirstEnabledTab();
        EditorGUILayout.Space();
        switch (selectedTab)
        {
            case Tab.Import: DrawImportTab(); break;
            case Tab.Geometry: DrawGeometryTab(); break;
            case Tab.Materials: DrawMaterialsTab(); break;
            case Tab.Marble: DrawMarbleTab(); break;
            case Tab.Agent:
                if (enableAgent) DrawAgentTab();
                else EditorGUILayout.HelpBox("Agent is disabled in Features.", MessageType.Info);
                break;
            case Tab.Tracking:
                if (enableTracking) DrawTrackingTab();
                else EditorGUILayout.HelpBox("Tracking is disabled in Features.", MessageType.Info);
                break;
        }
        EditorGUILayout.Space();
		if (selectedTab == Tab.Import)
        {
			if (GUILayout.Button(new GUIContent("Import SVG", "Import the level with current settings.")))
            {
                if (!TryGetSvgTextAndName(svgAsset, out var svgText, out var levelName)) return;
                ImportSvg(svgText, floorHeight, wallHeight, levelName);
            }
        }
    }

    private Tab DrawTabsToolbar(Tab current)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        foreach (Tab tab in System.Enum.GetValues(typeof(Tab)))
        {
            bool enabled = IsTabEnabled(tab);
            using (new EditorGUI.DisabledScope(!enabled))
            {
                bool isSelected = current == tab;
                bool pressed = GUILayout.Toggle(isSelected, GetTabLabel(tab), EditorStyles.toolbarButton);
                if (pressed && enabled && !isSelected) current = tab;
            }
        }
        EditorGUILayout.EndHorizontal();
        return current;
    }

    private bool IsTabEnabled(Tab tab)
    {
        switch (tab)
        {
            case Tab.Agent: return enableAgent;
            case Tab.Tracking: return enableTracking;
            default: return true;
        }
    }

    private Tab GetFirstEnabledTab()
    {
        if (IsTabEnabled(Tab.Import)) return Tab.Import;
        if (IsTabEnabled(Tab.Geometry)) return Tab.Geometry;
        if (IsTabEnabled(Tab.Materials)) return Tab.Materials;
        if (IsTabEnabled(Tab.Marble)) return Tab.Marble;
        if (IsTabEnabled(Tab.Agent)) return Tab.Agent;
        if (IsTabEnabled(Tab.Tracking)) return Tab.Tracking;
        return Tab.Import;
    }

    

    /// <summary>
    /// Extracts SVG text and a level name from a TextAsset or .svg asset.
    /// </summary>
    private bool TryGetSvgTextAndName(UnityEngine.Object asset, out string svgText, out string levelName)
    {
        svgText = null;
        levelName = "SVG";
        if (asset == null)
        {
            Debug.LogError("No SVG file selected.");
            return false;
        }
        if (asset is TextAsset textAsset)
        {
            svgText = textAsset.text;
            levelName = textAsset.name;
            return true;
        }
        string assetPath = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(assetPath) || !assetPath.EndsWith(".svg", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("Please select a .svg file or a TextAsset containing SVG content.");
            return false;
        }
        string fullPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"SVG file not found at path: {fullPath}");
            return false;
        }
        svgText = File.ReadAllText(fullPath);
        levelName = Path.GetFileNameWithoutExtension(assetPath);
        return true;
    }

    /// <summary>
    /// End-to-end import of the SVG into a playable level hierarchy according to current settings.
    /// </summary>
    private void ImportSvg(string svgText, float floorHeight, float wallHeight, string levelName)
    {
        int undoGroup = Undo.GetCurrentGroup();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Import SVG Level: {levelName}");
        // Ensure required layers exist before we start assigning them
        try
        {
            TrackingSetupService.EnsureLayerExistsEditor("Wall");
        }
        catch { }
        var ps = new ProgressScope("SVG Import", "Parsing SVG...", 0.05f);
        var sceneInfo = SVGParser.ImportSVG(new StringReader(svgText));

        // Removed unused tessellation options to keep import faster
        var rootGO = new GameObject("Level_" + ToPascalCase(levelName));
        Undo.RegisterCreatedObjectUndo(rootGO, "Create Level Root");
        GameObject marbleGO = null;
        GameObject endMarkerGO = null;
        GameObject finalRoot = rootGO;
       
        GameObject meshHelpersRoot = null;
        int meshHelperIndex = 0;
        GameObject wallsRoot = null;
        
        var shapesToProcess = new List<Shape>();
        System.Action<Unity.VectorGraphics.SceneNode> collect = null;
        collect = (n) =>
        {
            if (n == null) return;
            if (n.Shapes != null && n.Shapes.Count > 0)
            {
                for (int si = 0; si < n.Shapes.Count; si++)
                {
                    var s = n.Shapes[si];
                    if (s != null) shapesToProcess.Add(s);
                }
            }
            if (n.Children != null && n.Children.Count > 0)
            {
                for (int ci = 0; ci < n.Children.Count; ci++) collect(n.Children[ci]);
            }
        };
        collect(sceneInfo.Scene.Root);

        int wallIndex = 0;
        ps.Update("SVG Import", "Building meshes...", 0.35f);

        // Determine largest wall shape by area to treat as the outer wall
        int outerWallIndex = -1;
        float maxWallArea = 0f;
        for (int i = 0; i < shapesToProcess.Count; i++)
        {
            var shape = shapesToProcess[i];
            Color color = GetShapeFillColor(shape);
            if (GetMarkerType(color) != MarkerType.None) continue;
            if (IsFloorColor(color)) continue;
            var tmpContours = SampleAllContours(shape, curveSampleDistance);
            float area = ComputeTotalAbsArea(tmpContours);
            if (area > maxWallArea)
            {
                maxWallArea = area;
                outerWallIndex = i;
            }
        }

        for (int i = 0; i < shapesToProcess.Count; i++)
        {
            var shape = shapesToProcess[i];
            Color color = GetShapeFillColor(shape);
            
            if (GetMarkerType(color) != MarkerType.None)
                continue;
            
            var allContours = SampleAllContours(shape, curveSampleDistance);
            
            bool isFloor = IsFloorColor(color);
            bool isOuterWall = !isFloor && (i == outerWallIndex);
            float height = isFloor ? floorHeight : (isOuterWall ? wallHeight : innerWallHeight);
            float baseY = isFloor ? 0 : floorHeight;
            
            var contoursForExtrusion = isFloor ? PrepareContoursForFloorExtrusion(allContours) : allContours;
            
            var mesh = MeshExtruder.Extrude(height, baseY, contoursForExtrusion, isFloor);
            if (applyPreRotation && mesh != null && mesh.vertexCount > 0)
            {
                MeshExtruder.RotateMeshInPlace(mesh, Quaternion.Euler(0f, preRotationYDegrees, 0f));
            }
            
            string label = isFloor ? LABEL_FLOOR : (isOuterWall ? "OuterWall" : $"{LABEL_WALL_PREFIX}{++wallIndex}");
            var go = new GameObject(label);
            Undo.RegisterCreatedObjectUndo(go, "Create Mesh Object");
            if (label == LABEL_FLOOR)
            {
                go.transform.parent = rootGO.transform;
            }
            else
            {
                if (wallsRoot == null)
                {
                    wallsRoot = new GameObject("Walls");
                    Undo.RegisterCreatedObjectUndo(wallsRoot, "Create Walls Root");
                    wallsRoot.transform.parent = rootGO.transform;
                    AddNavMeshModifier(wallsRoot, AREA_NOT_WALKABLE);
                    // Set layer on walls root as well if available
                    EnsureWallLayer(wallsRoot, false);
                }
                go.transform.parent = wallsRoot.transform;
            }
            
           
            EnsureWallLayer(go, isFloor);
            
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            if (label == LABEL_FLOOR)
            {
                mr.sharedMaterial = floorMaterial;
            }
            else if (isOuterWall && frameMaterial != null)
            {
                mr.sharedMaterial = frameMaterial;
            }
            else
            {
                mr.sharedMaterial = wallMaterial;
            }
            ConfigureRendererProbeDefaults(mr);

           
            AddSharedMeshCollider(go, mesh, false);
            
            
            if (label == LABEL_FLOOR)
            {
                var mc = go.GetComponent<MeshCollider>();
                if (mc != null && floorPhysMaterial != null) mc.sharedMaterial = floorPhysMaterial;
                AddNavMeshModifier(go, AREA_WALKABLE);
                if (createMeshHelpers)
                {
                    CreateMeshHelpersForHoles(rootGO, allContours, baseY, ref meshHelpersRoot, ref meshHelperIndex);
                }
            }
            else
            {
                var mc = go.GetComponent<MeshCollider>();
                if (mc != null && wallPhysMaterial != null) mc.sharedMaterial = wallPhysMaterial;
            }
        }
               
        ps.Update("SVG Import", "Placing markers...", 0.6f);
        PlaceMarkers(sceneInfo.Scene, rootGO, out marbleGO, out endMarkerGO);
       
        if (mirrorX || mirrorZ)
        {
            ApplyTransformOptions(finalRoot.transform);
        }

        FitToTargetSizeAndRecenter(finalRoot.transform);
        finalRoot.transform.position = Vector3.zero;
       
        if (createHoleTrigger)
        {
            ps.Update("SVG Import", "Creating hole trigger...", 0.7f);
            CreateHoleTriggerBelowFloor(finalRoot.transform);
        }
        
        // Ensure all created transforms show 0 rotation in Inspector
        ZeroLocalRotationsRecursively(finalRoot.transform);

        // Derive marble radius in world units from the placed marker (after scaling)
        computedMarbleRadiusWorld = -1f;
        if (marbleGO != null)
        {
            var rend = marbleGO.GetComponent<Renderer>();
            if (rend != null)
            {
                float dx = rend.bounds.size.x;
                float dz = rend.bounds.size.z;
                float diameter = (dx + dz) * 0.5f;
                computedMarbleRadiusWorld = Mathf.Max(0.0001f, diameter * 0.5f);
            }
            else
            {
                // Fallback to transform scale (Sphere primitive diameter is 1 at scale=1)
                computedMarbleRadiusWorld = Mathf.Max(0.0001f, marbleGO.transform.lossyScale.x * 0.5f);
            }
        }

        // Optionally create gimbal frames and parent the level under them (keep agent on level root)
        if (createTiltRig)
        {
            ps.Update("SVG Import", "Building tilt rig...", 0.8f);
            BuildTiltRigAndFrames(finalRoot.transform);
        }
        else
        {
            // No rig: attach a TiltController to the level root for manual control
            var controller = finalRoot.GetComponent<TiltController>();
            if (controller == null) controller = finalRoot.AddComponent<TiltController>();
            controller.maxTilt = agentMaxTilt;
            controller.tiltSpeed = agentTiltSpeed;
            controller.keyboardInput = !enableAgent;
            controller.syncWithBoardRotation = false;
        }

        ps.Update("SVG Import", "Building NavMesh...", 0.85f);
        CreateNavMeshSurface(finalRoot.transform);
        ps.Update("SVG Import", "Configuring Agent & ML...", 0.9f);
        if (enableAgent)
        {
            ConfigureAgentAndML(finalRoot, marbleGO, endMarkerGO);
        }
        // Integrate visual tracking (tracking marker + HUD + tracking camera) and/or Real HUD
        if (enableTracking || addRealHud)
        {
            try
            {
                ps.Update("SVG Import", "Setting up tracking...", 0.95f);
                var trkOptions = new TrackingSetupService.TrackingOptions
                {
                    // Tracking-only features are gated by enableTracking
                    showTrackingFeedback = enableTracking && showTrackingFeedback,
                    showHud = enableTracking && trkAddHud,
                    showRealHud = addRealHud,
                    showDisplay = enableTracking && trkShowDisplay,
                    trackingMarkerScaleMultiplier = trackingMarkerScaleMultiplier,
                    trackingCamHeight = trackingCamHeight,
                    trackingCamPadding = trackingCamPadding,
                    trkSampleWidth = trkSampleWidth,
                    trkSampleHeight = trkSampleHeight,
                    trkSampleEveryNth = trkSampleEveryNth,
                    trkTargetTrackingFps = trkTargetTrackingFps,
                    trkMinPixelCount = trkMinPixelCount,
                    trkSmoothing = trkSmoothing,
                    trkSearchAroundLast = trkSearchAroundLast,
                    trkRoiRadiusViewport = trkRoiRadiusViewport,
                    trkEnablePrediction = trkEnablePrediction,
                    trkMaxMissedFrames = trkMaxMissedFrames,
                    trkVelocityDamping = trkVelocityDamping,
                    trkMissesBeforeFullScan = trkMissesBeforeFullScan,
                    trkRoiExpandFactorOnMiss = trkRoiExpandFactorOnMiss,
                    trkFloorViewportPadding = trkFloorViewportPadding,
                    trkTrackingMarkerHeightOffset = trkTrackingMarkerHeightOffset,
                    trkPlaceTrackingMarkerOnFloor = trkPlaceTrackingMarkerOnFloor,
                    trkRestrictToFloorViewport = trkRestrictToFloorViewport,
                    trkClampToFloorBounds = trkClampToFloorBounds,
                    trkTargetColor = trkTargetColor,
                    trkColorTolerance = trkColorTolerance,
                    trackingMarkerMaterial = trackingMarkerMaterial,
                    useShaderMask = (trackingMode == SvgImportSettings.TrackingMode.Shader)
                };
                TrackingSetupService.Setup(finalRoot.transform, marbleGO, trkOptions);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Tracking setup failed: {ex.Message}");
            }
            finally
            {
                ps.Clear();
                Undo.CollapseUndoOperations(undoGroup);
                if (rootGO.scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGO.scene);
                }
            }
        }
        else
        {
            ps.Clear();
            Undo.CollapseUndoOperations(undoGroup);
            if (rootGO.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rootGO.scene);
            }
        }
    }

   
    // Sampling now fully delegated to SVGShapeSampler; extrusion delegated to MeshExtruder

    private void ZeroLocalRotationsRecursively(Transform t)
    {
        if (t == null) return;
        t.localRotation = Quaternion.identity;
        foreach (Transform child in t)
        {
            ZeroLocalRotationsRecursively(child);
        }
    }

    

    // Removed internal triangulation/extrusion helpers in favor of MeshExtruder utilities
}