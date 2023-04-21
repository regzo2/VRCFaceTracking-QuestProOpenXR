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

namespace VRCFT_Module___QuestOpenXR
{
    public enum FBExpression
    {
        Brow_Lowerer_L = 0,
        Brow_Lowerer_R = 1,
        Cheek_Puff_L = 2,
        Cheek_Puff_R = 3,
        Cheek_Raiser_L = 4,
        Cheek_Raiser_R = 5,
        Cheek_Suck_L = 6,
        Cheek_Suck_R = 7,
        Chin_Raiser_B = 8,
        Chin_Raiser_T = 9,
        Dimpler_L = 10,
        Dimpler_R = 11,
        Eyes_Closed_L = 12,
        Eyes_Closed_R = 13,
        Eyes_Look_Down_L = 14,
        Eyes_Look_Down_R = 15,
        Eyes_Look_Left_L = 16,
        Eyes_Look_Left_R = 17,
        Eyes_Look_Right_L = 18,
        Eyes_Look_Right_R = 19,
        Eyes_Look_Up_L = 20,
        Eyes_Look_Up_R = 21,
        Inner_Brow_Raiser_L = 22,
        Inner_Brow_Raiser_R = 23,
        Jaw_Drop = 24,
        Jaw_Sideways_Left = 25,
        Jaw_Sideways_Right = 26,
        Jaw_Thrust = 27,
        Lid_Tightener_L = 28,
        Lid_Tightener_R = 29,
        Lip_Corner_Depressor_L = 30,
        Lip_Corner_Depressor_R = 31,
        Lip_Corner_Puller_L = 32,
        Lip_Corner_Puller_R = 33,
        Lip_Funneler_LB = 34,
        Lip_Funneler_LT = 35,
        Lip_Funneler_RB = 36,
        Lip_Funneler_RT = 37,
        Lip_Pressor_L = 38,
        Lip_Pressor_R = 39,
        Lip_Pucker_L = 40,
        Lip_Pucker_R = 41,
        Lip_Stretcher_L = 42,
        Lip_Stretcher_R = 43,
        Lip_Suck_LB = 44,
        Lip_Suck_LT = 45,
        Lip_Suck_RB = 46,
        Lip_Suck_RT = 47,
        Lip_Tightener_L = 48,
        Lip_Tightener_R = 49,
        Lips_Toward = 50,
        Lower_Lip_Depressor_L = 51,
        Lower_Lip_Depressor_R = 52,
        Mouth_Left = 53,
        Mouth_Right = 54,
        Nose_Wrinkler_L = 55,
        Nose_Wrinkler_R = 56,
        Outer_Brow_Raiser_L = 57,
        Outer_Brow_Raiser_R = 58,
        Upper_Lid_Raiser_L = 59,
        Upper_Lid_Raiser_R = 60,
        Upper_Lip_Raiser_L = 61,
        Upper_Lip_Raiser_R = 62,
        Max = 63
    }

    public class QuestProTrackingModule : ExtTrackingModule
    {
        [StructLayout(LayoutKind.Sequential)]
        class XrQuat
        {
            public float x;
            public float y;
            public float z;
            public float w;
        }

        [DllImport("QuestFaceTrackingOpenXR.dll")]
        static extern int InitOpenXRRuntime();

        [DllImport("QuestFaceTrackingOpenXR.dll")]
        static extern int UpdateOpenXRFaceTracker();

        [DllImport("QuestFaceTrackingOpenXR.dll")]
        static extern float GetCheekPuff(int cheekIndex);

        [DllImport("QuestFaceTrackingOpenXR.dll")]
        static extern void GetEyeOrientation(int eyeIndex, XrQuat outOrientation);

        [DllImport("QuestFaceTrackingOpenXR.dll")]
        static extern void GetFaceWeights([MarshalAs(UnmanagedType.LPArray, SizeConst = 63)] float[] faceExpressionFB);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);


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

            SetDllDirectory(Utils.CustomLibsDirectory + "\\ModuleLibs");

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

        private void UpdateEyeData(ref UnifiedEyeData eye, ref float[] expressions)
        {
            #region Eye Openness parsing

            //eye.Left.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_L] + Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_L] - expressions[(int)FBExpression.Eyes_Closed_R])));
            eye.Left.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_L] + 
                //expressions[(int)FBExpression.Eyes_Closed_L] * (-Math.Pow(expressions[(int)FBExpression.Lid_Tightener_L] - 0.5f, 2) + 0.5f)));
                expressions[(int)FBExpression.Eyes_Closed_L] * (2f * expressions[(int)FBExpression.Lid_Tightener_L] / Math.Pow(2f, 2f * expressions[(int)FBExpression.Lid_Tightener_L]))));

            eye.Right.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_R] +
                //expressions[(int)FBExpression.Eyes_Closed_R] * (-Math.Pow(expressions[(int)FBExpression.Lid_Tightener_R] - 0.5f, 2) + 0.5f)));
                expressions[(int)FBExpression.Eyes_Closed_R] * (2f * expressions[(int)FBExpression.Lid_Tightener_R] / Math.Pow(2f, 2f * expressions[(int)FBExpression.Lid_Tightener_R]))));
            //eye.Right.Openness = 1.0f - (float)Math.Max(0, Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_R] + Math.Min(1, expressions[(int)FBExpression.Eyes_Closed_R] - expressions[(int)FBExpression.Eyes_Closed_L])));

            #endregion

            #region Eye Gaze parsing

            XrQuat orientation_L = new XrQuat();
            GetEyeOrientation(0, orientation_L);
            double q_x = (float)orientation_L.x;
            double q_y = (float)orientation_L.y;
            double q_z = (float)orientation_L.z;
            double q_w = (float)orientation_L.w;

            double yaw = Math.Atan2(2.0 * (q_y * q_z + q_w * q_x), q_w * q_w - q_x * q_x - q_y * q_y + q_z * q_z);
            double pitch = Math.Asin(-2.0 * (q_x * q_z - q_w * q_y));

            double pitch_L = (180.0 / Math.PI) * pitch; // from radians
            double yaw_L = (180.0 / Math.PI) * yaw;

            XrQuat orientation_R = new XrQuat();
            GetEyeOrientation(1, orientation_R);

            q_x = (float)orientation_L.x;
            q_y = (float)orientation_L.y;
            q_z = (float)orientation_L.z;
            q_w = (float)orientation_L.w;
            yaw = Math.Atan2(2.0 * (q_y * q_z + q_w * q_x), q_w * q_w - q_x * q_x - q_y * q_y + q_z * q_z);
            pitch = Math.Asin(-2.0 * (q_x * q_z - q_w * q_y));

            double pitch_R = (180.0 / Math.PI) * pitch; // from radians
            double yaw_R = (180.0 / Math.PI) * yaw;

            #endregion

            #region Eye Data to UnifiedEye

            var radianConst = 0.0174533f;

            var pitch_R_mod = (float)(Math.Abs(pitch_R) + 4f * Math.Pow(Math.Abs(pitch_R) / 30f, 30f)); // curves the tail end to better accomodate actual eye pos.
            var pitch_L_mod = (float)(Math.Abs(pitch_L) + 4f * Math.Pow(Math.Abs(pitch_L) / 30f, 30f));
            var yaw_R_mod = (float)(Math.Abs(yaw_R) + 6f * Math.Pow(Math.Abs(yaw_R) / 27f, 18f)); // curves the tail end to better accomodate actual eye pos.
            var yaw_L_mod = (float)(Math.Abs(yaw_L) + 6f * Math.Pow(Math.Abs(yaw_L) / 27f, 18f));

            eye.Right.Gaze = new Vector2(
                pitch_R < 0 ? pitch_R_mod * radianConst : -1 * pitch_R_mod * radianConst,
                yaw_R < 0 ? -1 * yaw_R_mod * radianConst : (float)yaw_R * radianConst);
            eye.Left.Gaze = new Vector2(
                pitch_L < 0 ? pitch_L_mod * radianConst : -1 * pitch_L_mod * radianConst,
                yaw_L < 0 ? -1 * yaw_L_mod * radianConst : (float)yaw_L * radianConst);

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

            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperLeft].Weight = expressions[(int)FBExpression.Lip_Suck_LT];
            unifiedExpressions[(int)UnifiedExpressions.LipSuckUpperRight].Weight = expressions[(int)FBExpression.Lip_Suck_RT];
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