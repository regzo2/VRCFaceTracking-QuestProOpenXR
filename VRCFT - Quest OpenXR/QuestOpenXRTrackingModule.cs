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
using System.Reflection.Metadata.Ecma335;

namespace Meta_OpenXR
{
    public class QuestProTrackingModule : ExtTrackingModule
    {
        private const int expressionsSize = 63;
        private (bool, bool) trackingSupported = (false, false);
        private FaceWeightsFB expressions;
        private EyeGazesFB gazes;

        List<Stream> _images = new List<Stream>();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool SetDllDirectory(string lpPathName);

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            ModuleInformation.Name = "Quest Pro";

            var stream = GetType().Assembly.GetManifestResourceStream("Meta_OpenXR.Assets.quest-pro.png");
            ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

            var currentDllDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            SetDllDirectory(currentDllDirectory + "\\ModuleLibs");

            qxrResult result = QXR.InitializeSession();
            switch (result)
            {
                case qxrResult.RUNTIME_FEATURE_UNAVAILABLE:
                    Logger.LogError("Runtime does not have required features. " +
                                    "Ensure that the Oculus / Meta runtime is set as the default OpenXR runtime.");
                    return (false, false);
                case qxrResult.SPACE_CREATE_FAILURE:
                    Logger.LogError("Runtime failed to create a reference space. " +
                                    "Ensure that the Oculus / Meta runtime is set as the default OpenXR runtime.");
                    return (false, false);
                case qxrResult.SYSTEM_GET_FAILURE:
                    Logger.LogError("Runtime failed to provide system information." +
                                    "Ensure that the Oculus / Meta runtime is set as the default OpenXR runtime.");
                    return (false, false);
                case qxrResult.GRAPHICS_BIND_FAILURE:
                    Logger.LogError("Runtime failed to initialize graphics. " +
                                    "Ensure that you have a DX11 compatible system");
                    return (false, false);
                case qxrResult.RUNTIME_FAILURE:
                    Logger.LogError("Runtime failed to initialize session. " +
                                    "Ensure that the Oculus / Meta runtime is set as the default OpenXR runtime.");
                    return (false, false);
                case qxrResult.RUNTIME_MISSING:
                    Logger.LogError("Runtime does not exist. " +
                                    "Ensure that the Oculus / Meta runtime is set as the default OpenXR runtime.");
                    return (false, false);
                case qxrResult.SUCCESS:
                    Logger.LogInformation("Initialized session successfully.");
                    break;
                default:
                    Logger.LogError($"Session unable to be created for unknown reason {result}. Module will not be loaded.");
                    return (false, false);
            }

            trackingSupported = (QXR.CreateEyeTracker(), QXR.CreateFaceTracker());

            if (!trackingSupported.Item1)
                Logger.LogError("Eye tracking is unavailable for this session.");
            if (!trackingSupported.Item2)
                Logger.LogError("Face expression tracking is unavailable for this session.");

            return trackingSupported;
        }

        public override void Update()
        {
            Thread.Sleep(10);
            UpdateTracking();
        }

        public void UpdateTracking()
        {
            expressions.time = 10000000; // 10 ms, VRCFT update rate.
            if (QXR.GetFaceData(ref expressions))
            {
                UpdateEyeExpressions(ref UnifiedTracking.Data.Shapes, ref expressions.weights);
                UpdateMouthExpressions(ref UnifiedTracking.Data.Shapes, ref expressions.weights);
            }

            gazes.time = 10000000;
            if (QXR.GetEyeData(ref gazes))
                UpdateEyeData(ref UnifiedTracking.Data.Eye);
        }

        private Vector2 NormalizedGaze(XrQuaternion q)
        {
            float magnitude = (float)Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            q.x /= magnitude;
            q.y /= magnitude;
            q.z /= magnitude;
            q.w /= magnitude;

            float pitch = (float)Math.Asin(2*(q.x*q.z - q.w*q.y));
            float yaw = (float)Math.Atan2(2.0*(q.y*q.z + q.w*q.x), q.w*q.w - q.x*q.x - q.y*q.y + q.z*q.z);

            return new Vector2(pitch, yaw);
        }

        private void UpdateEyeData(ref UnifiedEyeData eyes)
        {
            #region Eye Openness parsing

            eyes.Left.Openness = 
                1.0f - (float)Math.Max(0, Math.Min(1, expressions.weights[(int)ExpressionFB.EYES_CLOSED_L]
                + expressions.weights[(int)ExpressionFB.CHEEK_RAISER_L] * expressions.weights[(int)ExpressionFB.LID_TIGHTENER_L]));

            eyes.Right.Openness = 
                1.0f - (float)Math.Max(0, Math.Min(1, expressions.weights[(int)ExpressionFB.EYES_CLOSED_R] 
                + expressions.weights[(int)ExpressionFB.CHEEK_RAISER_R] * expressions.weights[(int)ExpressionFB.LID_TIGHTENER_R]));

            #endregion

            #region Eye Data to UnifiedEye

            eyes.Right.Gaze = NormalizedGaze(gazes.gaze[(int)EyePositionFB.EYE_POSITION_RIGHT].orientation);
            eyes.Left.Gaze = NormalizedGaze(gazes.gaze[(int)EyePositionFB.EYE_POSITION_LEFT].orientation);

            // Eye dilation code, automated process maybe?
            eyes.Left.PupilDiameter_MM = 5f;
            eyes.Right.PupilDiameter_MM = 5f;

            // Force the normalization values of Dilation to fit avg. pupil values.
            eyes._minDilation = 0;
            eyes._maxDilation = 10;

            #endregion
        }

        private void UpdateEyeExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref float[] weights)
        {
            #region Eye Expressions Set

            unifiedExpressions[(int)UnifiedExpressions.EyeWideLeft].Weight = weights[(int)ExpressionFB.UPPER_LID_RAISER_L];
            unifiedExpressions[(int)UnifiedExpressions.EyeWideRight].Weight = weights[(int)ExpressionFB.UPPER_LID_RAISER_R];

            unifiedExpressions[(int)UnifiedExpressions.EyeSquintLeft].Weight = weights[(int)ExpressionFB.LID_TIGHTENER_L];
            unifiedExpressions[(int)UnifiedExpressions.EyeSquintRight].Weight = weights[(int)ExpressionFB.LID_TIGHTENER_R];

            #endregion

            #region Brow Expressions Set

            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = weights[(int)ExpressionFB.INNER_BROW_RAISER_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpRight].Weight = weights[(int)ExpressionFB.INNER_BROW_RAISER_R];
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = weights[(int)ExpressionFB.OUTER_BROW_RAISER_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpRight].Weight = weights[(int)ExpressionFB.OUTER_BROW_RAISER_R];

            unifiedExpressions[(int)UnifiedExpressions.BrowPinchLeft].Weight = weights[(int)ExpressionFB.BROW_LOWERER_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererLeft].Weight = weights[(int)ExpressionFB.BROW_LOWERER_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowPinchRight].Weight = weights[(int)ExpressionFB.BROW_LOWERER_R];
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererRight].Weight = weights[(int)ExpressionFB.BROW_LOWERER_R];

            #endregion
        }

        private void UpdateMouthExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref float[] weights)
        {
            #region Jaw Expression Set                        
            unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = weights[(int)ExpressionFB.JAW_DROP];
            unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = weights[(int)ExpressionFB.JAW_SIDEWAYS_LEFT];
            unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = weights[(int)ExpressionFB.JAW_SIDEWAYS_RIGHT];
            unifiedExpressions[(int)UnifiedExpressions.JawForward].Weight = weights[(int)ExpressionFB.JAW_THRUST];
            #endregion

            #region Mouth Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.MouthClosed].Weight = weights[(int)ExpressionFB.LIPS_TOWARD];

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = weights[(int)ExpressionFB.MOUTH_LEFT];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = weights[(int)ExpressionFB.MOUTH_LEFT];
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = weights[(int)ExpressionFB.MOUTH_RIGHT];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = weights[(int)ExpressionFB.MOUTH_RIGHT];

            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = weights[(int)ExpressionFB.LIP_CORNER_PULLER_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = weights[(int)ExpressionFB.LIP_CORNER_PULLER_L]; // Slant (Sharp Corner Raiser) is baked into Corner Puller.
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = weights[(int)ExpressionFB.LIP_CORNER_PULLER_R];
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = weights[(int)ExpressionFB.LIP_CORNER_PULLER_R]; // Slant (Sharp Corner Raiser) is baked into Corner Puller.
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownLeft].Weight = weights[(int)ExpressionFB.LIP_CORNER_DEPRESSOR_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownRight].Weight = weights[(int)ExpressionFB.LIP_CORNER_DEPRESSOR_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = weights[(int)ExpressionFB.LOWER_LIP_DEPRESSOR_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownRight].Weight = weights[(int)ExpressionFB.LOWER_LIP_DEPRESSOR_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = Math.Max(weights[(int)ExpressionFB.UPPER_LIP_RAISER_L], 
                                                                                           weights[(int)ExpressionFB.NOSE_WRINKLER_L]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = Math.Max(weights[(int)ExpressionFB.UPPER_LIP_RAISER_L], 
                                                                                               weights[(int)ExpressionFB.NOSE_WRINKLER_L]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = Math.Max(weights[(int)ExpressionFB.UPPER_LIP_RAISER_R], 
                                                                                            weights[(int)ExpressionFB.NOSE_WRINKLER_R]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = Math.Max(weights[(int)ExpressionFB.UPPER_LIP_RAISER_R], 
                                                                                                weights[(int)ExpressionFB.NOSE_WRINKLER_R]); // Workaround for upper lip up wierd tracking quirk.

            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserUpper].Weight = weights[(int)ExpressionFB.CHIN_RAISER_T];
            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserLower].Weight = weights[(int)ExpressionFB.CHIN_RAISER_B];

            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleLeft].Weight = weights[(int)ExpressionFB.DIMPLER_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleRight].Weight = weights[(int)ExpressionFB.DIMPLER_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerLeft].Weight = weights[(int)ExpressionFB.LIP_TIGHTENER_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerRight].Weight = weights[(int)ExpressionFB.LIP_TIGHTENER_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthPressLeft].Weight = weights[(int)ExpressionFB.LIP_PRESSOR_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthPressRight].Weight = weights[(int)ExpressionFB.LIP_PRESSOR_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthStretchLeft].Weight = weights[(int)ExpressionFB.LIP_STRETCHER_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthStretchRight].Weight = weights[(int)ExpressionFB.LIP_STRETCHER_R];
            #endregion

            #region Lip Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = weights[(int)ExpressionFB.LIP_PUCKER_R];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = weights[(int)ExpressionFB.LIP_PUCKER_R];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = weights[(int)ExpressionFB.LIP_PUCKER_L];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = weights[(int)ExpressionFB.LIP_PUCKER_L];

            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = weights[(int)ExpressionFB.LIP_FUNNELER_LT];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = weights[(int)ExpressionFB.LIP_FUNNELER_RT];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = weights[(int)ExpressionFB.LIP_FUNNELER_LB];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = weights[(int)ExpressionFB.LIP_FUNNELER_RB];

            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Min(1f - (float)Math.Pow(weights[(int)ExpressionFB.UPPER_LIP_RAISER_L], 1f/6f), weights[(int)ExpressionFB.LIP_SUCK_LT]);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Min(1f - (float)Math.Pow(weights[(int)ExpressionFB.UPPER_LIP_RAISER_R], 1f/6f), weights[(int)ExpressionFB.LIP_SUCK_RT]);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = weights[(int)ExpressionFB.LIP_SUCK_LB];
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = weights[(int)ExpressionFB.LIP_SUCK_RB];
            #endregion

            #region Cheek Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = weights[(int)ExpressionFB.CHEEK_PUFF_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = weights[(int)ExpressionFB.CHEEK_PUFF_R];
            unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight = weights[(int)ExpressionFB.CHEEK_SUCK_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = weights[(int)ExpressionFB.CHEEK_SUCK_R];
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintLeft].Weight = weights[(int)ExpressionFB.CHEEK_RAISER_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintRight].Weight = weights[(int)ExpressionFB.CHEEK_RAISER_R];
            #endregion

            #region Nose Expression Set             
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerLeft].Weight = weights[(int)ExpressionFB.NOSE_WRINKLER_L];
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerRight].Weight = weights[(int)ExpressionFB.NOSE_WRINKLER_R];
            #endregion

            #region Tongue Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = weights[(int)ExpressionFB.TONGUE_OUT];
            #endregion
        }

        public override void Teardown()
        {
            QXR.DestroyFaceTracker();
            QXR.DestroyEyeTracker();
            QXR.CloseSession();
        }
    }
}