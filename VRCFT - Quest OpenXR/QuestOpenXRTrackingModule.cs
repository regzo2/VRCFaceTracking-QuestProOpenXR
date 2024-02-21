using System;
using System.Threading;
using System.Runtime.InteropServices;

using System.Diagnostics;

using VRCFaceTracking;
using System.IO;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Types;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;
using System.Drawing.Imaging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using static Quest_OpenXR.OpenXR;

namespace Quest_OpenXR
{
    public class QuestProTrackingModule : ExtTrackingModule
    {
        private const int expressionsSize = 63;
        private byte[] rawExpressions = new byte[expressionsSize * 4 + (8 * 2 * 4)];
        private float[] expressions = new float[expressionsSize + (8 * 2)];
        float[] FaceExpressionFB = new float[expressionsSize];
        private (bool, bool) trackingSupported = (false, false);

        List<Stream> _images = new List<Stream>();

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            ModuleInformation.Name = "Quest Pro";

            var stream = GetType().Assembly.GetManifestResourceStream("VRCFT___Quest_OpenXR.Assets.quest-pro.png");
            ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

            var currentDllDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
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
                    Logger.LogError("[QuestOpenXR] Failed to create Face Treacker.");
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

        // This will be run in the tracking thread. This is exposed so you can control when and if the tracking data is updated down to the lowest level.
        public override void Update()
        {
            Thread.Sleep(10);
            UpdateTracking();
        }

        // The update function needs to be defined separately in case the user is running with the --vrcft-nothread launch parameter
        public void UpdateTracking()
        {
            int updateResult = UpdateOpenXRFaceTracker();
            
            GetFaceWeights(FaceExpressionFB);
            for (int i = 0; i < expressionsSize; ++i)
            {
                expressions[i] = FaceExpressionFB[i];
            }

            if (trackingSupported.Item1)
                UpdateEyeData(ref UnifiedTracking.Data.Eye, ref expressions);
                UpdateEyeExpressions(ref UnifiedTracking.Data.Shapes, ref expressions);
            if (trackingSupported.Item2)
                UpdateMouthExpressions(ref UnifiedTracking.Data.Shapes, ref expressions);
        }

        private static Vector2 NormalizedGaze(XrQuat q)
        {
            float magnitude = (float)Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            q.x /= magnitude;
            q.y /= magnitude;
            q.z /= magnitude;
            q.w /= magnitude;

            float pitch = (float)Math.Asin(2f * (q.w * q.y - q.z * q.x));
            float yaw = (float)Math.Atan2(2f * (q.w * q.z + q.x * q.y), 1 - 2f * (q.y * q.y + q.z * q.z));
        
            return new Vector2(pitch, yaw).PolarTo2DCartesian();
        }

        private void UpdateEyeData(ref UnifiedEyeData eye, ref float[] expressions)
        {
            #region Eye Openness parsing

            eye.Left.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_L] + 
                expressions[(int)FBExpression.Eyes_Closed_L] * expressions[(int)FBExpression.Lid_Tightener_L]));

            eye.Right.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_R] +
                expressions[(int)FBExpression.Eyes_Closed_R] * expressions[(int)FBExpression.Lid_Tightener_R]));
            #endregion

            #region Eye Gaze parsing

            XrQuat q_l = new XrQuat();
            GetEyeOrientation(0, q_l);

            XrQuat q_r = new XrQuat();
            GetEyeOrientation(1, q_r);

            #endregion

            #region Eye Data to UnifiedEye

            eye.Right.Gaze = NormalizedGaze(q_r);
            eye.Left.Gaze = NormalizedGaze(q_l);

            // Eye dilation code, automated process maybe?
            eye.Left.PupilDiameter_MM = 5f;
            eye.Right.PupilDiameter_MM = 5f;

            // Force the normalization values of Dilation to fit avg. pupil values.
            eye._minDilation = 0;
            eye._maxDilation = 10;

            #endregion
        }

        private void UpdateEyeExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref float[] expressions)
        {
            #region Eye Expressions Set

            unifiedExpressions[(int)UnifiedExpressions.EyeWideLeft].Weight = expressions[(int)FBExpression.Upper_Lid_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.EyeWideRight].Weight = expressions[(int)FBExpression.Upper_Lid_Raiser_R];

            unifiedExpressions[(int)UnifiedExpressions.EyeSquintLeft].Weight = expressions[(int)FBExpression.Lid_Tightener_L];
            unifiedExpressions[(int)UnifiedExpressions.EyeSquintRight].Weight = expressions[(int)FBExpression.Lid_Tightener_R];

            #endregion

            #region Brow Expressions Set

            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = expressions[(int)FBExpression.Inner_Brow_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpRight].Weight = expressions[(int)FBExpression.Inner_Brow_Raiser_R];
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = expressions[(int)FBExpression.Outer_Brow_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpRight].Weight = expressions[(int)FBExpression.Outer_Brow_Raiser_R];

            unifiedExpressions[(int)UnifiedExpressions.BrowPinchLeft].Weight = expressions[(int)FBExpression.Brow_Lowerer_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererLeft].Weight = expressions[(int)FBExpression.Brow_Lowerer_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowPinchRight].Weight = expressions[(int)FBExpression.Brow_Lowerer_R];
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererRight].Weight = expressions[(int)FBExpression.Brow_Lowerer_R];

            #endregion
        }

        private void UpdateMouthExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref float[] expressions)
        {

            #region Jaw Expression Set                        
            unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = expressions[(int)FBExpression.Jaw_Drop];
            unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = expressions[(int)FBExpression.Jaw_Sideways_Left];
            unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = expressions[(int)FBExpression.Jaw_Sideways_Right];
            unifiedExpressions[(int)UnifiedExpressions.JawForward].Weight = expressions[(int)FBExpression.Jaw_Thrust];
            #endregion

            #region Mouth Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.MouthClosed].Weight = expressions[(int)FBExpression.Lips_Toward];

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = expressions[(int)FBExpression.Mouth_Left];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = expressions[(int)FBExpression.Mouth_Left];
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = expressions[(int)FBExpression.Mouth_Right];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = expressions[(int)FBExpression.Mouth_Right];

            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = expressions[(int)FBExpression.Lip_Corner_Puller_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = expressions[(int)FBExpression.Lip_Corner_Puller_L]; // Slant (Sharp Corner Raiser) is baked into Corner Puller.
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = expressions[(int)FBExpression.Lip_Corner_Puller_R];
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = expressions[(int)FBExpression.Lip_Corner_Puller_R]; // Slant (Sharp Corner Raiser) is baked into Corner Puller.
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownLeft].Weight = expressions[(int)FBExpression.Lip_Corner_Depressor_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownRight].Weight = expressions[(int)FBExpression.Lip_Corner_Depressor_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = expressions[(int)FBExpression.Lower_Lip_Depressor_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownRight].Weight = expressions[(int)FBExpression.Lower_Lip_Depressor_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = Math.Max(0, expressions[(int)FBExpression.Upper_Lip_Raiser_L] - expressions[(int)FBExpression.Nose_Wrinkler_L]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = Math.Max(0, expressions[(int)FBExpression.Upper_Lip_Raiser_L] - expressions[(int)FBExpression.Nose_Wrinkler_L]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = Math.Max(0, expressions[(int)FBExpression.Upper_Lip_Raiser_R] - expressions[(int)FBExpression.Nose_Wrinkler_R]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = Math.Max(0, expressions[(int)FBExpression.Upper_Lip_Raiser_R] - expressions[(int)FBExpression.Nose_Wrinkler_R]); // Workaround for upper lip up wierd tracking quirk.

            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserUpper].Weight = expressions[(int)FBExpression.Chin_Raiser_T];
            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserLower].Weight = expressions[(int)FBExpression.Chin_Raiser_B];

            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleLeft].Weight = expressions[(int)FBExpression.Dimpler_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleRight].Weight = expressions[(int)FBExpression.Dimpler_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerLeft].Weight = expressions[(int)FBExpression.Lip_Tightener_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerRight].Weight = expressions[(int)FBExpression.Lip_Tightener_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthPressLeft].Weight = expressions[(int)FBExpression.Lip_Pressor_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthPressRight].Weight = expressions[(int)FBExpression.Lip_Pressor_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthStretchLeft].Weight = expressions[(int)FBExpression.Lip_Stretcher_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthStretchRight].Weight = expressions[(int)FBExpression.Lip_Stretcher_R];
            #endregion

            #region Lip Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = expressions[(int)FBExpression.Lip_Pucker_R];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = expressions[(int)FBExpression.Lip_Pucker_R];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = expressions[(int)FBExpression.Lip_Pucker_L];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = expressions[(int)FBExpression.Lip_Pucker_L];

            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = expressions[(int)FBExpression.Lip_Funneler_LT];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = expressions[(int)FBExpression.Lip_Funneler_RT];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = expressions[(int)FBExpression.Lip_Funneler_LB];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = expressions[(int)FBExpression.Lip_Funneler_RB];

            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Min(1f - (float)Math.Pow(expressions[(int)FBExpression.Upper_Lip_Raiser_L], 1f/6f), expressions[(int)FBExpression.Lip_Suck_LT]);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Min(1f - (float)Math.Pow(expressions[(int)FBExpression.Upper_Lip_Raiser_R], 1f/6f), expressions[(int)FBExpression.Lip_Suck_RT]);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = expressions[(int)FBExpression.Lip_Suck_LB];
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = expressions[(int)FBExpression.Lip_Suck_RB];
            #endregion

            #region Cheek Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = expressions[(int)FBExpression.Cheek_Puff_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = expressions[(int)FBExpression.Cheek_Puff_R];
            unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight = expressions[(int)FBExpression.Cheek_Suck_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = expressions[(int)FBExpression.Cheek_Suck_R];
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintLeft].Weight = expressions[(int)FBExpression.Cheek_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintRight].Weight = expressions[(int)FBExpression.Cheek_Raiser_R];
            #endregion

            #region Nose Expression Set             
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerLeft].Weight = expressions[(int)FBExpression.Nose_Wrinkler_L];
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerRight].Weight = expressions[(int)FBExpression.Nose_Wrinkler_R];
            #endregion

            #region Tongue Expression Set   
            //Future placeholder
            unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = 0f;
            #endregion
        }

        // A chance to de-initialize everything. This runs synchronously inside main game thread. Do not touch any Unity objects here.
        public override void Teardown()
        {
        }
    }
}