using Microsoft.Extensions.Logging;
using VRCFaceTracking;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using VRCFaceTracking.Core.Types;

namespace VRCFTQuestOpenXRModule;

public partial class QuestProTrackingModule : ExtTrackingModule
{
    private const int ExpressionsSize = 63;
    private (bool eyeCurrentlySupported, bool faceCurrentlySupported) trackingSupported = (false, false);
    private float[] expressions = new float[ExpressionsSize + (8 * 2)];
    private float[] faceExpressionFB = new float[ExpressionsSize];

    public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

    /// <summary>
    /// Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
    /// </summary>
    public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
    {
        ModuleInformation.Name = "Quest Pro";

        var stream = GetType().Assembly.GetManifestResourceStream("VRCFT___Quest_OpenXR.Assets.quest-pro.png");
        ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

        var currentDllDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        SetDllDirectory(currentDllDirectory + "\\ModuleLibs");

        int RuntimeInitResult = InitOpenXRRuntime();
        switch (RuntimeInitResult)
        {
            case -2:
                Logger.LogInformation("[QuestOpenXR] Facial Tracking not supported.");
                trackingSupported.Item2 = false;
                break;
            case -1:
                Logger.LogInformation("[QuestOpenXR] Eye Tracking not supported.");
                trackingSupported.Item1 = false;
                break;
            case 0:
                Logger.LogInformation("[QuestOpenXR] OpenXR Runtime init success.");
                trackingSupported = (true, true);
                break;
            case 2:
                Logger.LogError("[QuestOpenXR] Failed to create XrInstance.");
                Logger.LogError("[QuestOpenXR] Please make sure that the Oculus application is running, or this module did not properly uninitialize.");
                trackingSupported = (false, false);
                break;
            case 3:
                Logger.LogError("[QuestOpenXR] Failed to get XrSystemID.");
                trackingSupported = (false, false);
                break;
            case 4:
                Logger.LogError("[QuestOpenXR] Failed to get XrViewConfigurationType.");
                trackingSupported = (false, false);
                break;
            case 5:
                Logger.LogError("[QuestOpenXR] Failed to GetD3D11GraphicsRequirements.");
                trackingSupported = (false, false);
                break;
            case 6:
                Logger.LogError("[QuestOpenXR] Failed to create session.");
                Logger.LogError("[QuestOpenXR] Please ensure that Oculus app is the active OpenXR runtime.");
                trackingSupported = (false, false);
                break;
            case 7:
                Logger.LogError("[QuestOpenXR] OpenXR Failed to create XrSpace.");
                trackingSupported = (false, false);
                break;
            case 8:
                Logger.LogError("[QuestOpenXR] Failed to begin session.");
                trackingSupported = (false, false);
                break;
            case 9:
                Logger.LogError("[QuestOpenXR] Failed to create Face Tracker.");
                trackingSupported.Item2 = false;
                break;
            case 10:
                Logger.LogError("[QuestOpenXR] Failed to create Eye Tracker.");
                trackingSupported.Item1 = false;
                break;
            default:
                Logger.LogError("[QuestOpenXR] OpenXR Runtime undefined error. Please restart this module.");
                trackingSupported = (false, false);
                break;
        }

        return trackingSupported;
    }

    /// <summary>
    /// This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
    /// </summary>
    public override void Update()
    {
        UpdateTracking();
        Thread.Sleep(10);
    }

    /// <summary>
    /// The update function needs to be defined separately in case the user is running with the --vrcft-nothread launch parameter
    /// </summary>
    public void UpdateTracking()
    {
        UpdateOpenXRFaceTracker();
        GetFaceWeights(faceExpressionFB);

        // TODO: Is there any reason why we need a duplicate array here?
        for (int i = 0; i < ExpressionsSize; ++i)
        {
            expressions[i] = faceExpressionFB[i];
        }

        if (trackingSupported.eyeCurrentlySupported)
        {
            UpdateEyeData(ref UnifiedTracking.Data.Eye, ref expressions);
            UpdateEyeExpressions(ref UnifiedTracking.Data.Shapes, ref expressions);
        }

        if (trackingSupported.faceCurrentlySupported)
        {
            UpdateMouthExpressions(ref UnifiedTracking.Data.Shapes, ref expressions);
        }
    }

    /// <summary>
    /// A twofold method whose purpose is to poll the latest eye information, and convert to VRCFT's standard
    /// </summary>
    /// <param name="eye"></param>
    /// <param name="expressions"></param>
    private void UpdateEyeData(ref UnifiedEyeData eye, ref float[] expressions)
    {
        eye.Left.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_L] + 
            expressions[(int)FBExpression.Eyes_Closed_L] * expressions[(int)FBExpression.Lid_Tightener_L]));

        eye.Right.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_R] +
            expressions[(int)FBExpression.Eyes_Closed_R] * expressions[(int)FBExpression.Lid_Tightener_R]));

        XrQuat orientation_L = new XrQuat();
        GetEyeOrientation(0, orientation_L);
        double q_x = orientation_L.x;
        double q_y = orientation_L.y;
        double q_z = orientation_L.z;
        double q_w = orientation_L.w;

        double yaw = Math.Atan2(2.0 * (q_y * q_z + q_w * q_x), q_w * q_w - q_x * q_x - q_y * q_y + q_z * q_z);
        double pitch = Math.Asin(-2.0 * (q_x * q_z - q_w * q_y));

        double pitch_L = (180.0 / Math.PI) * pitch; // From radians
        double yaw_L = (180.0 / Math.PI) * yaw;

        XrQuat orientation_R = new XrQuat();
        GetEyeOrientation(1, orientation_R);

        q_x = orientation_L.x;
        q_y = orientation_L.y;
        q_z = orientation_L.z;
        q_w = orientation_L.w;
        yaw = Math.Atan2(2.0 * (q_y * q_z + q_w * q_x), q_w * q_w - q_x * q_x - q_y * q_y + q_z * q_z);
        pitch = Math.Asin(-2.0 * (q_x * q_z - q_w * q_y));

        double pitch_R = (180.0 / Math.PI) * pitch; // From radians
        double yaw_R = (180.0 / Math.PI) * yaw;

        var radianConst = 0.0174533f;

        var pitch_R_mod = (float)(Math.Abs(pitch_R) + 4f * Math.Pow(Math.Abs(pitch_R) / 30f, 30f)); // Curves the tail end to better accomodate actual eye pos.
        var pitch_L_mod = (float)(Math.Abs(pitch_L) + 4f * Math.Pow(Math.Abs(pitch_L) / 30f, 30f));
        var yaw_R_mod = (float)(Math.Abs(yaw_R) + 6f * Math.Pow(Math.Abs(yaw_R) / 27f, 18f)); // Curves the tail end to better accomodate actual eye pos.
        var yaw_L_mod = (float)(Math.Abs(yaw_L) + 6f * Math.Pow(Math.Abs(yaw_L) / 27f, 18f));

        eye.Right.Gaze = new Vector2(
            pitch_R < 0 ? pitch_R_mod * radianConst : -1 * pitch_R_mod * radianConst,
            yaw_R < 0 ? -1 * yaw_R_mod * radianConst : (float)yaw_R * radianConst);
        eye.Left.Gaze = new Vector2(
            pitch_L < 0 ? pitch_L_mod * radianConst : -1 * pitch_L_mod * radianConst,
            yaw_L < 0 ? -1 * yaw_L_mod * radianConst : (float)yaw_L * radianConst);

        eye.Left.PupilDiameter_MM = 5f;
        eye.Right.PupilDiameter_MM = 5f;

        // Force the normalization values of Dilation to fit avg. pupil values.
        eye._minDilation = 0;
        eye._maxDilation = 10;
    }

    private void UpdateEyeExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref float[] expressions)
    {
        foreach (var shape in SupportedEyeExpressions.OuterKeys)
        {
            unifiedExpressions[(int)shape].Weight = expressions[(int)SupportedEyeExpressions.GetInnerValue(shape)];
        }
    }

    private void UpdateMouthExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref float[] expressions)
    {
        // Workaround for weird upper lip up tracking quirk
        var upperMaxLeft = Math.Max(0, expressions[(int)FBExpression.Upper_Lip_Raiser_L] - expressions[(int)FBExpression.Nose_Wrinkler_L]);
        var upperMaxRight = Math.Max(0, expressions[(int)FBExpression.Upper_Lip_Raiser_R] - expressions[(int)FBExpression.Nose_Wrinkler_R]);
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = upperMaxLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = upperMaxLeft;
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = upperMaxRight;
        unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = upperMaxRight;

        // Workaround for weird lip suck tracking quirk
        unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Min(1f - (float)Math.Pow(expressions[(int)FBExpression.Upper_Lip_Raiser_L], 1f / 6f), expressions[(int)FBExpression.Lip_Suck_LT]);
        unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Min(1f - (float)Math.Pow(expressions[(int)FBExpression.Upper_Lip_Raiser_R], 1f / 6f), expressions[(int)FBExpression.Lip_Suck_RT]);

        foreach (var shape in SupportedFaceExpressions.OuterKeys)
        {
            UnifiedTracking.Data.Shapes[(int)shape].Weight = expressions[(int)SupportedFaceExpressions.GetInnerValue(shape)];
        }
    }

    public override void Teardown() { }
}