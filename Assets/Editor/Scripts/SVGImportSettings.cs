using UnityEngine;
using Unity.Barracuda;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

/// <summary>
/// ScriptableObject preset for SVG level importer settings so users can save/load configurations.
/// Grouped into Features, Color Mapping, Geometry, Frames/Rig, Materials, Physics, Agent, Behavior, and Tracking.
/// </summary>
[CreateAssetMenu(fileName = "Svg Import Settings", menuName = "SVG Import Settings", order = 1000)]
public class SvgImportSettings : ScriptableObject
{
    [Header("Features")]
    [Tooltip("Create and configure the BoardAgent component.")]
    public bool enableAgent = SVGImporterDefaults.Values.EnableAgent;
    // ML-Agents setup is implied by Enable Agent; no separate toggle
    [Tooltip("Create tracking objects and enable tracking setup.")]
    public bool enableTracking = SVGImporterDefaults.Values.EnableTracking;
    [InspectorName("Show HUD")]
    [Tooltip("Create on-screen HUD for the real ball")]
    [UnityEngine.Serialization.FormerlySerializedAs("trkAddRealHud")]
    public bool addRealHud = SVGImporterDefaults.Values.AddRealHud;

    // Color Mapping
    [Header("Color Mapping")]
    [Tooltip("Fill color that marks floor shapes in the SVG.")]
    [InspectorName("Floor Color")] public Color colorFloor = SVGImporterDefaults.Colors.Floor; // #cccccc
    [Tooltip("Fill color that marks wall shapes in the SVG.")]
    [InspectorName("Wall Color")] public Color colorWall = SVGImporterDefaults.Colors.Wall; // #333333
    [Tooltip("Fill color that marks the start marker in the SVG.")]
    [InspectorName("Start Marker Color")] public Color colorStart = SVGImporterDefaults.Colors.Start; // #ff0000
    [Tooltip("Fill color that marks the goal marker in the SVG.")]
    [InspectorName("Goal Marker Color")] public Color colorEnd = SVGImporterDefaults.Colors.End; // #00ff00
    [Tooltip("Fill color that marks the marble spawn in the SVG.")]
    [InspectorName("Marble Color")] public Color colorMarble = SVGImporterDefaults.Colors.Marble; // #0000ff
    [Tooltip("Fill color that explicitly marks a goal trigger in the SVG.")]
    [InspectorName("Goal Trigger Color")] public Color colorEndTrigger = SVGImporterDefaults.Colors.EndTrigger; // #ff00ff

    [Header("Geometry")]
    [Tooltip("Vertical thickness of the playable floor (world units).")]
    [Range(0.01f, 200f)] public float floorHeight = SVGImporterDefaults.Values.FloorHeight;
    [Tooltip("Height of the outer boundary walls and frames (world units).")]
    [Range(0.01f, 300f)] public float wallHeight = SVGImporterDefaults.Values.WallHeight; // UI: labeled as Frame Height
    [Tooltip("Height for interior walls inside the maze (world units).")]
    [Range(0.01f, 300f)] public float innerWallHeight = SVGImporterDefaults.Values.InnerWallHeight; // inner walls slightly shorter than outer/frame by default
    [UnityEngine.Serialization.FormerlySerializedAs("sphereStartHeight")]
    [InspectorName("Marble Gap")]
    [Tooltip("Vertical distance of the marble above the floor top (world units).")]
    [Range(0f, 5f)] public float marbleStartHeight = SVGImporterDefaults.Values.MarbleStartHeight;
    // In SVG/board units
    [Tooltip("Distance between samples along SVG curves (board units). Smaller values increase fidelity.")]
    [Range(0.02f, 2f)] public float curveSampleDistance = SVGImporterDefaults.Values.CurveSampleDistance;
    [InspectorName("Simplify Floor Top")]
    [Tooltip("Reduces vertex count of the floor top outline using RDP simplification.")]
    public bool simplifyFloorContours = SVGImporterDefaults.Values.SimplifyFloorContours;
    // In SVG/board units
    [Tooltip("Tolerance for floor simplification in board (SVG) units. Larger values remove more detail.")]
    [Range(0.01f, 2f)] public float floorSimplifyTolerance = SVGImporterDefaults.Values.FloorSimplifyTolerance;
    [InspectorName("Fit Width")]
    [Tooltip("Uniformly scale the level so its world width matches Target World Width.")]
    public bool fitToTargetSize = SVGImporterDefaults.Values.FitToTargetSize;
    [Tooltip("Desired world-space width of the imported board (X extent).")]
    [Range(0.01f, 200f)] public float targetWorldWidth = SVGImporterDefaults.Values.TargetWorldWidth;
    [InspectorName("Apply Rotation")]
    [Tooltip("Bake a Y-axis rotation directly into meshes and positions (Inspector shows 0° after import).")]
    public bool applyPreRotation = SVGImporterDefaults.Values.ApplyPreRotation;
    [InspectorName("Y Rotation (deg)")]
    [Tooltip("Degrees to rotate around world Y before placing objects.")]
    [Range(-360f, 360f)] public float preRotationYDegrees = SVGImporterDefaults.Values.PreRotationYDegrees;
    [Tooltip("Flip the level along the X axis after import.")]
    public bool mirrorX = SVGImporterDefaults.Values.MirrorX;
    [Tooltip("Flip the level along the Z axis after import.")]
    public bool mirrorZ = SVGImporterDefaults.Values.MirrorZ;

    // Frames / Rig
    [Header("Frames / Rig")]
    [Tooltip("Build a gimballed frame around the board for physical tilting and visualization.")]
    [InspectorName("Tilt Rig")] public bool createTiltRig = SVGImporterDefaults.Values.CreateTiltRig;
    [Tooltip("Beam thickness for inner/outer frames (visual width).")]
    [Range(0.01f, 5f)] public float frameThickness = SVGImporterDefaults.Values.FrameThickness;
    [Tooltip("Height of the frames when not matching the wall height (world units).")]
    [Range(0.01f, 300f)] public float frameHeight = SVGImporterDefaults.Values.FrameHeight;
    [InspectorName("Gap Between Frames")]
    [Tooltip("Visible gap between the outer and inner frame (world units).")]
    [Range(0f, 0.5f)] public float frameGap = SVGImporterDefaults.Values.FrameGap;
    [InspectorName("Board Clearance")]
    [Tooltip("Gap between the inner frame and the playable board to avoid contact (world units).")]
    [Range(0f, 0.2f)] public float frameBoardClearance = SVGImporterDefaults.Values.FrameBoardClearance;
    [Tooltip("Match the frame height to the tallest wall of the imported board.")]
    public bool frameMatchWallHeight = SVGImporterDefaults.Values.FrameMatchWallHeight;
    [Tooltip("Visual thickness of hinge cylinders (always created with the rig).")]
    [Range(0.005f, 1.0f)] public float hingeRadius = SVGImporterDefaults.Values.HingeRadius; // hinges are always created with the rig

    // Tilt controls
    [InspectorName("Tilt Speed")]
    [UnityEngine.Serialization.FormerlySerializedAs("agentTiltSpeed")]
    [Tooltip("Speed at which the board can tilt (deg/sec).")]
    [Range(1f, 360f)] public float tiltSpeed = SVGImporterDefaults.Values.TiltSpeed;
    [InspectorName("Max Tilt")]
    [UnityEngine.Serialization.FormerlySerializedAs("agentMaxTilt")]
    [Tooltip("Maximum tilt angle allowed (degrees).")]
    [Range(1f, 89f)] public float maxTilt = SVGImporterDefaults.Values.MaxTilt;

    // Helpers / Triggers
    [Header("Helpers / Triggers")]
    [Tooltip("Create small sphere helpers for detected hole contours (debugging/authoring).")]
    [InspectorName("Hole Markers")] public bool createMeshHelpers = true;
    [Tooltip("Create trigger boxes under the board to catch balls falling through holes.")]
    [InspectorName("Hole Triggers")] public bool createHoleTrigger = SVGImporterDefaults.Values.CreateHoleTrigger;
    // Catch trigger height in SVG/board units (scales with import)
    [Tooltip("Catch trigger height in board (SVG) units. Scales with import.")]
    [Range(0.01f, 100f)] public float holeTriggerHeight = SVGImporterDefaults.Values.HoleTriggerHeight;

    // Materials
    [Header("Materials")]
    [Tooltip("Material applied to the floor mesh.")]
    [InspectorName("Floor")] public Material floorMaterial;
    [Tooltip("Material applied to inner walls.")]
    [InspectorName("Wall")] public Material wallMaterial;
    [Tooltip("Material applied to the outer wall/frames.")]
    [InspectorName("Frame")] public Material frameMaterial;
    [Tooltip("Material used for visual hinge cylinders.")]
    [InspectorName("Hinge")] public Material hingeMaterial;
    [Tooltip("Material for the start marker cylinder.")]
    [InspectorName("Start Marker")] public Material startMarkerMaterial;
    [Tooltip("Material for the goal marker cylinder.")]
    [InspectorName("Goal Marker")] public Material endMarkerMaterial;
    [UnityEngine.Serialization.FormerlySerializedAs("sphereMarkerMaterial")]
    [InspectorName("Marble")]
    [Tooltip("Material for the marble (visual only).")]
    public Material marbleMarkerMaterial;
    [InspectorName("Tracking Marker")]
    [UnityEngine.Serialization.FormerlySerializedAs("trackingBallMaterial")]
    [Tooltip("Material for the tracking marker overlay.")]
    public Material trackingMarkerMaterial;
    [Tooltip("Optional material to visualize the catch trigger (debug).")]
    [InspectorName("Hole Trigger")] public Material holeTriggerMaterial;
    [Tooltip("Material for the goal trigger sphere (if present).")]
    [InspectorName("Goal Trigger")] public Material endTriggerMaterial;
    [InspectorName("Hole Markers")]
    [Tooltip("Material for hole helper gizmos.")]
    public Material meshHelperMaterial;

    // Physic materials
    [Header("Physic Materials")]
    [Tooltip("Physics material applied to floor colliders.")]
    [InspectorName("Floor Physics")] public PhysicMaterial floorPhysMaterial;
    [Tooltip("Physics material applied to wall colliders.")]
    [InspectorName("Wall Physics")] public PhysicMaterial wallPhysMaterial;
    [Tooltip("Physics material applied to the marble collider.")]
    [InspectorName("Marble Physics")] public PhysicMaterial marblePhysMaterial;
    [Tooltip("Physics material applied to frame beams (colliders).")]
    [InspectorName("Frame Physics")] public PhysicMaterial framePhysMaterial;

    // Marble physics
    [Header("Marble Physics")]
    [UnityEngine.Serialization.FormerlySerializedAs("sphereMass")]
    [Tooltip("Rigidbody mass of the marble.")]
    [InspectorName("Mass")]
    [Range(0.01f, 50f)] public float marbleMass = SVGImporterDefaults.Values.MarbleMass;
    [UnityEngine.Serialization.FormerlySerializedAs("sphereDrag")]
    [Tooltip("Linear drag applied to the marble.")]
    [InspectorName("Drag")]
    [Range(0f, 10f)] public float marbleDrag = SVGImporterDefaults.Values.MarbleDrag;
    [UnityEngine.Serialization.FormerlySerializedAs("sphereAngularDrag")]
    [Tooltip("Rotational drag applied to the marble.")]
    [InspectorName("Ang. Drag")]
    [Range(0f, 10f)] public float marbleAngularDrag = SVGImporterDefaults.Values.MarbleAngularDrag;

    // Agent
    [Header("Agent")]
    [Tooltip("Maximum steps per episode for the agent.")]
    [InspectorName("Max Step")]
    [Range(1, 200000)] public int agentMaxStep = SVGImporterDefaults.Values.AgentMaxStep;
    [Tooltip("Number of nearby holes considered for observations.")]
    [InspectorName("Nearest Holes")]
    [Range(0, 32)] public int agentKNearestHoles = SVGImporterDefaults.Values.AgentKNearestHoles;
    [Tooltip("Height above the floor from which obstacle rays are cast (world units).")]
    [InspectorName("Ray Height")]
    [Range(0f, 1f)] public float agentRayHeight = SVGImporterDefaults.Values.AgentRayHeight;
    [Tooltip("Safety factor applied around holes/obstacles (0..1 typical).")]
    [InspectorName("Clearance Factor")]
    [Range(0f, 2f)] public float agentClearanceFactor = SVGImporterDefaults.Values.AgentClearanceFactor;

    [Tooltip("Number of milestone bins along the ideal path for bonus rewards.")]
    [InspectorName("Milestone Bins")]
    [Range(0, 64)] public int agentMilestoneBins = SVGImporterDefaults.Values.AgentMilestoneBins;
    [Tooltip("Bonus reward granted per reached milestone.")]
    [InspectorName("Milestone Bonus")]
    [Range(0f, 10f)] public float agentMilestoneBonus = SVGImporterDefaults.Values.AgentMilestoneBonus;
    [Tooltip("Number of radial rays cast for obstacle sensing.")]
    [InspectorName("Ray Count")]
    [Range(0, 64)] public int agentRayCount = SVGImporterDefaults.Values.AgentRayCount;
    [Tooltip("Maximum distance for obstacle raycasts (world units).")]
    [InspectorName("Max Distance")]
    [Range(0.1f, 50f)] public float agentMaxDistance = SVGImporterDefaults.Values.AgentMaxDistance;

    // BehaviorParameters
    [Header("Behavior Parameters")]
    [Tooltip("Unique name for the ML-Agents behavior.")]
    [InspectorName("Name")] public string behaviorName = SVGImporterDefaults.Values.BehaviorName;
    [Tooltip("Team identifier for multi-agent setups.")]
    [InspectorName("Team ID")] public int behaviorTeamId = SVGImporterDefaults.Values.BehaviorTeamId;
    [Tooltip("Training/Inference mode for this behavior.")]
    [InspectorName("Type")] public Unity.MLAgents.Policies.BehaviorType behaviorType = SVGImporterDefaults.Values.BehaviorType;
    [Tooltip("Include sensors on child objects in observations.")]
    [InspectorName("Child Sensors")] public bool useChildSensors = SVGImporterDefaults.Values.UseChildSensors;
    [Tooltip("Size of the vector observation for the policy.")]
    [InspectorName("Obs Size")]
    [Range(1, 1024)] public int vectorObservationSize = SVGImporterDefaults.Values.VectorObservationSize;
    [Tooltip("Number of stacked vector observations.")]
    [InspectorName("Stacked Vectors")]
    [Range(1, 50)] public int stackedVectors = SVGImporterDefaults.Values.StackedVectors;
    [Tooltip("Number of continuous actions (e.g., X/Z tilt).")]
    [InspectorName("Action Size")]
    [Range(1, 64)] public int continuousActionSize = SVGImporterDefaults.Values.ContinuousActionSize;
    [Tooltip("Number of frames between agent decisions.")]
    [InspectorName("Period")]
    [Range(1, 120)] public int decisionPeriod = SVGImporterDefaults.Values.DecisionPeriod;
    [Tooltip("If enabled, actions are applied every frame between decisions.")]
    [InspectorName("Act Between")] public bool takeActionsBetweenDecisions = SVGImporterDefaults.Values.TakeActionsBetweenDecisions;
    [Tooltip("Optional NNModel for inference.")]
    [InspectorName("Model")] public NNModel behaviorModel;
    [Tooltip("Inference device for the model.")]
    [InspectorName("Device")] public InferenceDevice inferenceDevice = SVGImporterDefaults.Values.InferenceDevice;

    // Tracking
    public enum TrackingMode { RGB, Shader }
    [Header("Tracking")]
    [InspectorName("Mode")]
    [Tooltip("RGB: color thresholds; Shader: BallMask shader writes a binary mask.")]
    public TrackingMode trackingMode = SVGImporterDefaults.Values.DefaultTrackingMode;
    [InspectorName("Show Tracking Marker")]
    [Tooltip("Enable the tracking marker and HUD.")]
    public bool showTrackingFeedback = SVGImporterDefaults.Values.ShowTrackingFeedback;
    [InspectorName("Show HUD")]
    [Tooltip("Create on-screen HUD (text and tracking camera preview).")]
    public bool trkAddHud = SVGImporterDefaults.Values.TrkAddHud;
    [InspectorName("Show Display")]
    [Tooltip("Show the tracking camera preview display (RawImage) on the HUD.")]
    public bool trkShowDisplay = SVGImporterDefaults.Values.TrkShowDisplay;
    [UnityEngine.Serialization.FormerlySerializedAs("trackingScaleMultiplier")]
    [InspectorName("Scale Multiplier")]
    [Tooltip("Visual size multiplier for the tracking marker relative to the marble.")]
    [Range(0.5f, 3f)] public float trackingMarkerScaleMultiplier = SVGImporterDefaults.Values.TrackingMarkerScaleMultiplier;
    [UnityEngine.Serialization.FormerlySerializedAs("trackingCamHeight")]
    [InspectorName("Height")]
    [Tooltip("Distance of the tracking camera above the board (world units).")]
    [Range(2f, 30f)] public float trackingCamHeight = SVGImporterDefaults.Values.TrackingCamHeight;
    [UnityEngine.Serialization.FormerlySerializedAs("trackingCamPadding")]
    [InspectorName("Padding")]
    [Tooltip("Extra margin around the board in the tracking view (world units).")]
    [Range(0f, 5f)] public float trackingCamPadding = SVGImporterDefaults.Values.TrackingCamPadding;
    [UnityEngine.Serialization.FormerlySerializedAs("trkPlaceTrackingOnFloor")]
    [InspectorName("Marker On Floor")]
    [Tooltip("Project the tracking marker onto the floor plane instead of placing it in 3D.")]
    public bool trkPlaceTrackingMarkerOnFloor = SVGImporterDefaults.Values.TrkPlaceOnFloor;
    [InspectorName("Floor Viewport Only")]
    [Tooltip("Only search within the viewport area that covers the floor.")]
    public bool trkRestrictToFloorViewport = SVGImporterDefaults.Values.TrkRestrictToFloorViewport;
    [InspectorName("Clamp To Floor")]
    [Tooltip("Clamp tracking marker position to the floor bounds.")]
    public bool trkClampToFloorBounds = SVGImporterDefaults.Values.TrkClampToFloor;
    [Tooltip("Downscaled texture width for CPU sampling.")]
    [InspectorName("Sample Width")]
    [Range(64, 2048)] public int trkSampleWidth = SVGImporterDefaults.Values.TrkSampleWidth;
    [Tooltip("Downscaled texture height for CPU sampling.")]
    [InspectorName("Sample Height")]
    [Range(64, 2048)] public int trkSampleHeight = SVGImporterDefaults.Values.TrkSampleHeight;
    [Tooltip("Process every Nth frame to reduce cost.")]
    [InspectorName("Sample Stride")]
    [Range(1, 10)] public int trkSampleEveryNth = SVGImporterDefaults.Values.TrkSampleEveryNth;
    [Tooltip("0 = off; if > 0, throttles tracking by time instead of frame stride (FPS).")]
    [InspectorName("Target Tracking FPS")]
    [Range(0f, 240f)] public float trkTargetTrackingFps = SVGImporterDefaults.Values.TrkTargetTrackingFps;
    [Tooltip("Minimum number of matching pixels to accept a detection.")]
    [InspectorName("Min Pixels")]
    [Range(0, 10000)] public int trkMinPixelCount = SVGImporterDefaults.Values.TrkMinPixelCount;
    [Tooltip("Temporal smoothing of the detected centroid (0 = none).")]
    [InspectorName("Smoothing")]
    [Range(0f, 1f)] public float trkSmoothing = SVGImporterDefaults.Values.TrkSmoothing;
    [Tooltip("Search in a small ROI around the previous detection first.")]
    [InspectorName("Search Around Last")] public bool trkSearchAroundLast = SVGImporterDefaults.Values.TrkSearchAroundLast;
    [Tooltip("Base radius of the search ROI in viewport units (0..1).")]
    [InspectorName("ROI Radius (viewport)")]
    [Range(0.01f, 0.9f)] public float trkRoiRadiusViewport = SVGImporterDefaults.Values.TrkRoiRadiusViewport;
    [Tooltip("Predict motion briefly when detection is lost.")]
    [InspectorName("Enable Prediction")] public bool trkEnablePrediction = SVGImporterDefaults.Values.TrkEnablePrediction;
    [Tooltip("Maximum frames to continue predicting when lost.")]
    [InspectorName("Max Missed Frames")]
    [Range(0, 600)] public int trkMaxMissedFrames = SVGImporterDefaults.Values.TrkMaxMissedFrames;
    [Tooltip("Damping of the predicted viewport velocity (0..1).")]
    [InspectorName("Velocity Damping")]
    [Range(0f, 1f)] public float trkVelocityDamping = SVGImporterDefaults.Values.TrkVelocityDamping;
    [Tooltip("Number of consecutive misses before scanning the full frame.")]
    [InspectorName("Misses Before Scan")]
    [Range(0, 100)] public int trkMissesBeforeFullScan = SVGImporterDefaults.Values.TrkMissesBeforeFullScan;
    [Tooltip("ROI growth factor applied per miss while searching.")]
    [InspectorName("ROI Growth")]
    [Range(1f, 10f)] public float trkRoiExpandFactorOnMiss = SVGImporterDefaults.Values.TrkRoiExpandFactorOnMiss;
    [Tooltip("Padding added around the computed floor viewport when restricting (0..0.2 typical).")]
    [InspectorName("Floor Viewport Padding")]
    [Range(0f, 0.2f)] public float trkFloorViewportPadding = SVGImporterDefaults.Values.TrkFloorViewportPadding;
    [UnityEngine.Serialization.FormerlySerializedAs("trkTrackingHeightOffset")]
    [InspectorName("Marker Height")]
    [Tooltip("Offset above the floor when placing the tracking marker (world units).")]
    [Range(0f, 2f)] public float trkTrackingMarkerHeightOffset = SVGImporterDefaults.Values.TrkTrackingMarkerHeightOffset;
    [Tooltip("RGB color to detect when in RGB mode.")]
    [InspectorName("Target")] public Color trkTargetColor = SVGImporterDefaults.Values.TrkTargetColor;
    [Tooltip("RGB distance tolerance (0..1) when in RGB mode.")]
    [InspectorName("Tolerance")]
    [Range(0f, 1f)] public float trkColorTolerance = SVGImporterDefaults.Values.TrkColorTolerance;
    
    
}

