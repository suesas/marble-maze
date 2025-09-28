using UnityEngine;
using Unity.MLAgents.Policies;
using Unity.Barracuda;
using Unity.MLAgents;

/// <summary>
/// Centralized default values for the SVG level importer, grouped by topic.
/// Keeps UI defaults, color names, and behavior parameters in one discoverable place.
/// </summary>
public static class SVGImporterDefaults
{
    public static class Colors
    {
		// Direct Color defaults to avoid parsing at call sites
		public static readonly Color Floor = new Color(0.8f, 0.8f, 0.8f, 1f);
		public static readonly Color Wall = new Color(0.2f, 0.2f, 0.2f, 1f);
		public static readonly Color Start = new Color(1f, 0f, 0f, 1f);
		public static readonly Color End = new Color(0f, 1f, 0f, 1f);
		public static readonly Color Marble = new Color(0f, 0f, 1f, 1f);
		public static readonly Color EndTrigger = new Color(1f, 0f, 1f, 1f);
    }

	public static class Names
	{
		public static readonly string[] Floor = { "Floor", "Mat_Floor", "White", "Wood" };
		public static readonly string[] Wall = { "Walls", "Mat_Wall" };
		public static readonly string[] Frame = { "Frame", "Walls" };
		public static readonly string[] Hinge = { "Frame" };
		public static readonly string[] Start = { "Start" };
		public static readonly string[] Goal = { "Goal" };
		public static readonly string[] Marble = { "Marble" };
		public static readonly string[] TrackingMarker = { "Tracking_Marker", "RGB_Tracking_Marble" };
		public static readonly string[] Hole = { "Hole", "Mat_Hole" };
        // Default PhysicMaterial asset names to probe for
        public static readonly string[] PhysFloor = { "Floor", "Phys_Floor", "PM_Floor" };
        public static readonly string[] PhysWall = { "Wall", "Walls", "Phys_Wall" };
        public static readonly string[] PhysMarbleArray = { "Marble", "metal" };
        public static readonly string[] PhysFrame = { "Frame", "Wall", "Phys_Frame" };
		public const string PhysMarble = "metal";
	}

    public static class Values
	{
		// Features (Import tab)
		public const bool EnableAgent = true;
		public const bool EnableTracking = false;
		public const bool AddRealHud = true;
		public const SvgImportSettings.TrackingMode DefaultTrackingMode = SvgImportSettings.TrackingMode.RGB;
		// Geometry
		public const float FloorHeight = 10f;
		public const float WallHeight = 25f;
		public const float InnerWallHeight = 22f;
		public const float MarbleStartHeight = 0.5f;
		public const float CurveSampleDistance = 0.4f;
		public const bool SimplifyFloorContours = true;
		public const float FloorSimplifyTolerance = 0.1f;
		public const bool FitToTargetSize = true;
		public const float TargetWorldWidth =20.16f;
		public const bool ApplyPreRotation = true;
		public const float PreRotationYDegrees = 90f;
		public const bool MirrorX = true;
		public const bool MirrorZ = true;
		public const bool CreateMeshHelpers = true;
		public const bool CreateHoleTrigger = true;
		public const bool CreateTiltRig = true;
		public const float FrameThickness = 0.5f;
		public const float FrameHeight = 25f;
		public const float FrameGap = 0.25f;
		public const float FrameBoardClearance = 0.02f;
		public const bool FrameMatchWallHeight = true;
		public const float HingeRadius = 0.25f;
		public const float HoleTriggerHeight = 10f;
		// Tilt controls
		public const float TiltSpeed = 90f;
		public const float MaxTilt = 10f;

		// Marble
		public const float MarbleMass = 5f;
		public const float MarbleDrag = 0.01f;
		public const float MarbleAngularDrag = 0.05f;

		// Agent
		public const int AgentMaxStep = 50000;

		public const int AgentKNearestHoles = 5;
		public const float AgentRayHeight = 0.2f;
		public const float AgentClearanceFactor = 0.8f;
		public const int AgentMilestoneBins = 30;
		public const float AgentMilestoneBonus = 0.5f;
		public const int AgentRayCount = 8;
		public const float AgentMaxDistance = 4f;

		// Behavior (ML-Agents)
		public const string BehaviorName = "TestBehaviour";
		public const int BehaviorTeamId = 0;
		public static readonly BehaviorType BehaviorType = BehaviorType.Default;
		public const bool UseChildSensors = true;
		public const int VectorObservationSize = 28;
		public const int ContinuousActionSize = 2;
		public const int DecisionPeriod = 3;
		public const int StackedVectors = 10;
		public const bool TakeActionsBetweenDecisions = true;
        public const Unity.MLAgents.Policies.InferenceDevice InferenceDevice = Unity.MLAgents.Policies.InferenceDevice.Default;

		// Tracking
		public const float TrackingMarkerScaleMultiplier = 1.5f;
		public const float TrackingCamHeight = 10f;
		public const float TrackingCamPadding = 1.0f;
		public const bool ShowTrackingFeedback = true;
		public const bool TrkAddHud = true;
		public const bool TrkShowDisplay = true;
		public const bool TrkPlaceOnFloor = true;
		public const bool TrkRestrictToFloorViewport = false;
		public const bool TrkClampToFloor = true;
		public const int TrkSampleWidth = 512;
		public const int TrkSampleHeight = 512;
		public const int TrkSampleEveryNth = 1;
		public const float TrkTargetTrackingFps = 0f;
		public const int TrkMinPixelCount = 10;
		public const float TrkSmoothing = 0.05f;
		public const bool TrkSearchAroundLast = true;
		public const float TrkRoiRadiusViewport = 0.35f;
		public const bool TrkEnablePrediction = true;
		public const int TrkMaxMissedFrames = 12;
		public const float TrkVelocityDamping = 0.85f;
		public const int TrkMissesBeforeFullScan = 3;
		public const float TrkRoiExpandFactorOnMiss = 1.75f;
		public const float TrkFloorViewportPadding = 0.02f;
		public const float TrkTrackingMarkerHeightOffset = 0.5f;
		public static readonly Color TrkTargetColor = Color.blue;
		public const float TrkColorTolerance = 0.15f;
	}
}


