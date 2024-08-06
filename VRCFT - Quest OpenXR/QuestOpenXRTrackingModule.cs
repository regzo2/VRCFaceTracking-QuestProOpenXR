using System.Runtime.InteropServices;
using VRCFaceTracking;
using Microsoft.Extensions.Logging;
using VRCFaceTracking.Core.Types;
using VRCFaceTracking.Core.Params.Data;
using VRCFaceTracking.Core.Params.Expressions;

namespace Meta_OpenXR
{
    public class QuestProTrackingModule : ExtTrackingModule
    {
        private (bool, bool) faceSlots = (false, false);
        private FaceWeightsFB expressions;
        private EyeGazesFB gazes;

        private string questRuntimeJson = "oculus";

        List<Stream> _images = new List<Stream>();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool SetDllDirectory(string lpPathName);

        private void SetRuntimeToQuest()
        {
            if (Utils.HasAdmin && OpenXRRegistryHelper.SetActiveRuntime(questRuntimeJson, ref Logger))
                Logger.LogInformation("Setting active OpenXR runtime to Quest.");
        }
        
        private void ResetRuntime()
        {
            if (Utils.HasAdmin)
            {
                Logger.LogInformation("Resetting OpenXR runtime to original active runtime.");
                OpenXRRegistryHelper.RestoreOriginalActiveRuntime(ref Logger);
            }
        }

        // Synchronous module initialization. Take as much time as you need to initialize any external modules. This runs in the init-thread
        public override (bool SupportsEye, bool SupportsExpression) Supported => (true, true);

        public override (bool eyeSuccess, bool expressionSuccess) Initialize(bool eyeAvailable, bool expressionAvailable)
        {
            ModuleInformation.Name = "Quest Pro";

            var stream = GetType().Assembly.GetManifestResourceStream("Meta_OpenXR.Assets.quest-pro.png");
            ModuleInformation.StaticImages = stream != null ? new List<Stream> { stream } : ModuleInformation.StaticImages;

            var currentDllDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            SetDllDirectory(currentDllDirectory + "\\ModuleLibs");

            if (Utils.HasAdmin && OpenXRRegistryHelper.SetActiveRuntime(questRuntimeJson, ref Logger))
                Logger.LogInformation("Setting active OpenXR runtime to Quest.");

            qxrResult result = QXR.InitializeSession();

            if (result != qxrResult.SUCCESS)
            {
                Logger.LogError(result switch
                {
                    qxrResult.RUNTIME_FEATURE_UNAVAILABLE => "Runtime does not have required features available.",
                    qxrResult.SPACE_CREATE_FAILURE => "Runtime failed to create a reference space.",
                    qxrResult.SYSTEM_GET_FAILURE => "Runtime failed to provide system information.",
                    qxrResult.GRAPHICS_BIND_FAILURE => "Runtime failed to bind to a graphics runtime.",
                    qxrResult.RUNTIME_FAILURE => "Runtime failed to initialize.",
                    qxrResult.RUNTIME_MISSING => "Runtime does not exist.",
                    _ => $"Session unable to be created: {result}. Module will not be loaded. "
                });

                if (!Utils.HasAdmin)
                {
                    Logger.LogInformation("Please ensure that Quest Link running and is set as the active OpenXR runtime");
                    Logger.LogInformation("Or run VRCFaceTracking as administrator to enable automatic runtime switching.");
                }

                goto EndInit;
            }

            var trackingSupported = (QXR.CreateEyeTracker(), QXR.CreateFaceTracker());

            if (!trackingSupported.Item1)
                Logger.LogError("Quest Eye tracking is unavailable for this session.");
            if (!trackingSupported.Item2)
                Logger.LogError("Quest Face expression tracking is unavailable for this session.");

            faceSlots = (eyeAvailable && (trackingSupported.Item1 || trackingSupported.Item2),
                         expressionAvailable && trackingSupported.Item2);

            // set tracking update rate to VRCFT update rate.
            expressions.time = 10000000;
            gazes.time = 10000000;

            EndInit:
            ResetRuntime();

            return faceSlots;
        }

        public override void Update()
        {
            Thread.Sleep(10);
            UpdateTracking();
        }

        public void UpdateTracking()
        {
            if (faceSlots.Item2 && QXR.GetFaceData(ref expressions))
            {
                if (faceSlots.Item1) UpdateEyeExpressions(ref UnifiedTracking.Data.Shapes, ref expressions.weights);
                UpdateMouthExpressions(ref UnifiedTracking.Data.Shapes, ref expressions.weights);
            }
            if (faceSlots.Item1 && QXR.GetEyeData(ref gazes))
            {
                UpdateEyeData(ref UnifiedTracking.Data.Eye, ref expressions.weights, ref gazes);
            }
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

        private void UpdateEyeData(ref UnifiedEyeData eyes, ref float[] weights, ref EyeGazesFB gazes)
        {
            #region Eye Openness parsing

            eyes.Left.Openness = 
                1.0f - (float)Math.Max(0, Math.Min(1, weights[(int)ExpressionFB.EYES_CLOSED_L]
                + weights[(int)ExpressionFB.CHEEK_RAISER_L] * weights[(int)ExpressionFB.LID_TIGHTENER_L]));

            eyes.Right.Openness = 
                1.0f - (float)Math.Max(0, Math.Min(1, weights[(int)ExpressionFB.EYES_CLOSED_R] 
                + weights[(int)ExpressionFB.CHEEK_RAISER_R] * weights[(int)ExpressionFB.LID_TIGHTENER_R]));

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
            unifiedExpressions[(int)UnifiedExpressions.MouthClosed].Weight = Math.Max(0, 
                                                                                Math.Min(weights[(int)ExpressionFB.LIPS_TOWARD], 
                                                                                         weights[(int)ExpressionFB.JAW_DROP]));

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

            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpLeft].Weight = Math.Max(0, expressions[(int)Expressions.UpperLipRaiserL] - expressions[(int)Expressions.NoseWrinklerL]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenLeft].Weight = Math.Max(0, expressions[(int)Expressions.UpperLipRaiserL] - expressions[(int)Expressions.NoseWrinklerL]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperUpRight].Weight = Math.Max(0, expressions[(int)Expressions.UpperLipRaiserR] - expressions[(int)Expressions.NoseWrinklerR]); // Workaround for upper lip up wierd tracking quirk.
            unifiedExpressions[(int)UnifiedExpressions.MouthUpperDeepenRight].Weight = Math.Max(0, expressions[(int)Expressions.UpperLipRaiserR] - expressions[(int)Expressions.NoseWrinklerR]); // Workaround for upper lip up wierd tracking quirk.

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
            SetRuntimeToQuest();
            QXR.DestroyFaceTracker();
            QXR.DestroyEyeTracker();
            QXR.CloseSession();
            ResetRuntime();
        }
    }
}
