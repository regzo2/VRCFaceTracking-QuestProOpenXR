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
                1.0f - (float)Math.Max(0, Math.Min(1, expressions.weights[(int)FBExpression.Eyes_Closed_L]
                + expressions.weights[(int)FBExpression.Cheek_Raiser_L] * expressions.weights[(int)FBExpression.Lid_Tightener_L]));

            eyes.Right.Openness = 
                1.0f - (float)Math.Max(0, Math.Min(1, expressions.weights[(int)FBExpression.Eyes_Closed_R] 
                + expressions.weights[(int)FBExpression.Eyes_Closed_R] * expressions.weights[(int)FBExpression.Lid_Tightener_R]));

            #endregion

            #region Eye Data to UnifiedEye

            eyes.Right.Gaze = NormalizedGaze(gazes.gaze[1].orientation);
            eyes.Left.Gaze = NormalizedGaze(gazes.gaze[0].orientation);

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

            unifiedExpressions[(int)UnifiedExpressions.EyeWideLeft].Weight = weights[(int)FBExpression.Upper_Lid_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.EyeWideRight].Weight = weights[(int)FBExpression.Upper_Lid_Raiser_R];

            unifiedExpressions[(int)UnifiedExpressions.EyeSquintLeft].Weight = weights[(int)FBExpression.Lid_Tightener_L];
            unifiedExpressions[(int)UnifiedExpressions.EyeSquintRight].Weight = weights[(int)FBExpression.Lid_Tightener_R];

            #endregion

            #region Brow Expressions Set

            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpLeft].Weight = weights[(int)FBExpression.Inner_Brow_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowInnerUpRight].Weight = weights[(int)FBExpression.Inner_Brow_Raiser_R];
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpLeft].Weight = weights[(int)FBExpression.Outer_Brow_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowOuterUpRight].Weight = weights[(int)FBExpression.Outer_Brow_Raiser_R];

            unifiedExpressions[(int)UnifiedExpressions.BrowPinchLeft].Weight = weights[(int)FBExpression.Brow_Lowerer_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererLeft].Weight = weights[(int)FBExpression.Brow_Lowerer_L];
            unifiedExpressions[(int)UnifiedExpressions.BrowPinchRight].Weight = weights[(int)FBExpression.Brow_Lowerer_R];
            unifiedExpressions[(int)UnifiedExpressions.BrowLowererRight].Weight = weights[(int)FBExpression.Brow_Lowerer_R];

            #endregion
        }

        private void UpdateMouthExpressions(ref UnifiedExpressionShape[] unifiedExpressions, ref float[] weights)
        {

            #region Jaw Expression Set                        
            unifiedExpressions[(int)UnifiedExpressions.JawOpen].Weight = weights[(int)FBExpression.Jaw_Drop];
            unifiedExpressions[(int)UnifiedExpressions.JawLeft].Weight = weights[(int)FBExpression.Jaw_Sideways_Left];
            unifiedExpressions[(int)UnifiedExpressions.JawRight].Weight = weights[(int)FBExpression.Jaw_Sideways_Right];
            unifiedExpressions[(int)UnifiedExpressions.JawForward].Weight = weights[(int)FBExpression.Jaw_Thrust];
            #endregion

            #region Mouth Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.MouthClosed].Weight = weights[(int)FBExpression.Lips_Toward];

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperLeft].Weight = weights[(int)FBExpression.Mouth_Left];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerLeft].Weight = weights[(int)FBExpression.Mouth_Left];
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperRight].Weight = weights[(int)FBExpression.Mouth_Right];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerRight].Weight = weights[(int)FBExpression.Mouth_Right];

            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullLeft].Weight = weights[(int)FBExpression.Lip_Corner_Puller_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantLeft].Weight = weights[(int)FBExpression.Lip_Corner_Puller_L]; // Slant (Sharp Corner Raiser) is baked into Corner Puller.
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerPullRight].Weight = weights[(int)FBExpression.Lip_Corner_Puller_R];
            unifiedExpressions[(int)UnifiedExpressions.MouthCornerSlantRight].Weight = weights[(int)FBExpression.Lip_Corner_Puller_R]; // Slant (Sharp Corner Raiser) is baked into Corner Puller.
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownLeft].Weight = weights[(int)FBExpression.Lip_Corner_Depressor_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthFrownRight].Weight = weights[(int)FBExpression.Lip_Corner_Depressor_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownLeft].Weight = weights[(int)FBExpression.Lower_Lip_Depressor_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthLowerDownRight].Weight = weights[(int)FBExpression.Lower_Lip_Depressor_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = Math.Max(weights[(int)FBExpression.Upper_Lip_Raiser_L], 
                                                                                           weights[(int)FBExpression.Nose_Wrinkler_L]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = Math.Max(weights[(int)FBExpression.Upper_Lip_Raiser_L], 
                                                                                               weights[(int)FBExpression.Nose_Wrinkler_L]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = Math.Max(weights[(int)FBExpression.Upper_Lip_Raiser_R], 
                                                                                            weights[(int)FBExpression.Nose_Wrinkler_R]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = Math.Max(weights[(int)FBExpression.Upper_Lip_Raiser_R], 
                                                                                                weights[(int)FBExpression.Nose_Wrinkler_R]); // Workaround for upper lip up wierd tracking quirk.

            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserUpper].Weight = weights[(int)FBExpression.Chin_Raiser_T];
            unifiedExpressions[(int)UnifiedExpressions.MouthRaiserLower].Weight = weights[(int)FBExpression.Chin_Raiser_B];

            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleLeft].Weight = weights[(int)FBExpression.Dimpler_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthDimpleRight].Weight = weights[(int)FBExpression.Dimpler_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerLeft].Weight = weights[(int)FBExpression.Lip_Tightener_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthTightenerRight].Weight = weights[(int)FBExpression.Lip_Tightener_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthPressLeft].Weight = weights[(int)FBExpression.Lip_Pressor_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthPressRight].Weight = weights[(int)FBExpression.Lip_Pressor_R];

            unifiedExpressions[(int)UnifiedExpressions.MouthStretchLeft].Weight = weights[(int)FBExpression.Lip_Stretcher_L];
            unifiedExpressions[(int)UnifiedExpressions.MouthStretchRight].Weight = weights[(int)FBExpression.Lip_Stretcher_R];
            #endregion

            #region Lip Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperRight].Weight = weights[(int)FBExpression.Lip_Pucker_R];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerRight].Weight = weights[(int)FBExpression.Lip_Pucker_R];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerUpperLeft].Weight = weights[(int)FBExpression.Lip_Pucker_L];
            unifiedExpressions[(int)UnifiedExpressions.LipPuckerLowerLeft].Weight = weights[(int)FBExpression.Lip_Pucker_L];

            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperLeft].Weight = weights[(int)FBExpression.Lip_Funneler_LT];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelUpperRight].Weight = weights[(int)FBExpression.Lip_Funneler_RT];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerLeft].Weight = weights[(int)FBExpression.Lip_Funneler_LB];
            unifiedExpressions[(int)UnifiedExpressions.LipFunnelLowerRight].Weight = weights[(int)FBExpression.Lip_Funneler_RB];

            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = Math.Min(1f - (float)Math.Pow(weights[(int)FBExpression.Upper_Lip_Raiser_L], 1f/6f), weights[(int)FBExpression.Lip_Suck_LT]);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = Math.Min(1f - (float)Math.Pow(weights[(int)FBExpression.Upper_Lip_Raiser_R], 1f/6f), weights[(int)FBExpression.Lip_Suck_RT]);
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerLeft].Weight = weights[(int)FBExpression.Lip_Suck_LB];
            unifiedExpressions[(int)UnifiedExpressions.LipSuckLowerRight].Weight = weights[(int)FBExpression.Lip_Suck_RB];
            #endregion

            #region Cheek Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffLeft].Weight = weights[(int)FBExpression.Cheek_Puff_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekPuffRight].Weight = weights[(int)FBExpression.Cheek_Puff_R];
            unifiedExpressions[(int)UnifiedExpressions.CheekSuckLeft].Weight = weights[(int)FBExpression.Cheek_Suck_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekSuckRight].Weight = weights[(int)FBExpression.Cheek_Suck_R];
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintLeft].Weight = weights[(int)FBExpression.Cheek_Raiser_L];
            unifiedExpressions[(int)UnifiedExpressions.CheekSquintRight].Weight = weights[(int)FBExpression.Cheek_Raiser_R];
            #endregion

            #region Nose Expression Set             
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerLeft].Weight = weights[(int)FBExpression.Nose_Wrinkler_L];
            unifiedExpressions[(int)UnifiedExpressions.NoseSneerRight].Weight = weights[(int)FBExpression.Nose_Wrinkler_R];
            #endregion

            #region Tongue Expression Set   
            unifiedExpressions[(int)UnifiedExpressions.TongueOut].Weight = weights[(int)FBExpression.Tongue_Out];
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