using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFTQuestOpenXRModule;

public partial class QuestProTrackingModule
{
    /// <summary>
    /// A read-only collection of supported face expressions that don't require any additional processing.
    /// </summary>
    public static readonly TwoKeyDictionary<UnifiedExpressions, FBExpression, float> SupportedFaceExpressions = new()
    {
        { UnifiedExpressions.JawOpen, FBExpression.Jaw_Drop, 0f },
        { UnifiedExpressions.JawLeft, FBExpression.Jaw_Sideways_Left, 0f },
        { UnifiedExpressions.JawRight, FBExpression.Jaw_Sideways_Right, 0f },
        { UnifiedExpressions.JawForward, FBExpression.Jaw_Thrust, 0f },
        { UnifiedExpressions.MouthClosed, FBExpression.Lips_Toward, 0f },
        { UnifiedExpressions.MouthUpperLeft, FBExpression.Mouth_Left, 0f },
        { UnifiedExpressions.MouthLowerLeft, FBExpression.Mouth_Left, 0f },
        { UnifiedExpressions.MouthUpperRight, FBExpression.Mouth_Right, 0f },
        { UnifiedExpressions.MouthLowerRight, FBExpression.Mouth_Right, 0f },
        { UnifiedExpressions.MouthCornerPullLeft, FBExpression.Lip_Corner_Puller_L, 0f },
        { UnifiedExpressions.MouthCornerSlantLeft, FBExpression.Lip_Corner_Puller_L, 0f },
        { UnifiedExpressions.MouthCornerPullRight, FBExpression.Lip_Corner_Puller_R, 0f },
        { UnifiedExpressions.MouthCornerSlantRight, FBExpression.Lip_Corner_Puller_R, 0f },
        { UnifiedExpressions.MouthFrownLeft, FBExpression.Lip_Corner_Depressor_L, 0f },
        { UnifiedExpressions.MouthFrownRight, FBExpression.Lip_Corner_Depressor_R, 0f },
        { UnifiedExpressions.MouthLowerDownLeft, FBExpression.Lower_Lip_Depressor_L, 0f },
        { UnifiedExpressions.MouthLowerDownRight, FBExpression.Lower_Lip_Depressor_R, 0f },
        { UnifiedExpressions.MouthRaiserUpper, FBExpression.Chin_Raiser_T, 0f },
        { UnifiedExpressions.MouthRaiserLower, FBExpression.Chin_Raiser_B, 0f },
        { UnifiedExpressions.MouthDimpleLeft, FBExpression.Dimpler_L, 0f },
        { UnifiedExpressions.MouthDimpleRight, FBExpression.Dimpler_R, 0f },
        { UnifiedExpressions.MouthTightenerLeft, FBExpression.Lip_Tightener_L, 0f },
        { UnifiedExpressions.MouthTightenerRight, FBExpression.Lip_Tightener_R, 0f },
        { UnifiedExpressions.MouthPressLeft, FBExpression.Lip_Pressor_L, 0f },
        { UnifiedExpressions.MouthPressRight, FBExpression.Lip_Pressor_R, 0f },
        { UnifiedExpressions.MouthStretchLeft, FBExpression.Lip_Stretcher_L, 0f },
        { UnifiedExpressions.MouthStretchRight, FBExpression.Lip_Stretcher_R, 0f },
        { UnifiedExpressions.LipPuckerUpperRight, FBExpression.Lip_Pucker_R, 0f },
        { UnifiedExpressions.LipPuckerLowerRight, FBExpression.Lip_Pucker_R, 0f },
        { UnifiedExpressions.LipPuckerUpperLeft, FBExpression.Lip_Pucker_L, 0f },
        { UnifiedExpressions.LipPuckerLowerLeft, FBExpression.Lip_Pucker_L, 0f },
        { UnifiedExpressions.LipFunnelUpperLeft, FBExpression.Lip_Funneler_LT, 0f },
        { UnifiedExpressions.LipFunnelUpperRight, FBExpression.Lip_Funneler_RT, 0f },
        { UnifiedExpressions.LipFunnelLowerLeft, FBExpression.Lip_Funneler_LB, 0f },
        { UnifiedExpressions.LipFunnelLowerRight, FBExpression.Lip_Funneler_RB, 0f },
        { UnifiedExpressions.LipSuckLowerLeft, FBExpression.Lip_Suck_LB, 0f },
        { UnifiedExpressions.LipSuckLowerRight, FBExpression.Lip_Suck_RB, 0f },
        { UnifiedExpressions.CheekPuffLeft, FBExpression.Cheek_Puff_L, 0f },
        { UnifiedExpressions.CheekPuffRight, FBExpression.Cheek_Puff_R, 0f },
        { UnifiedExpressions.CheekSuckLeft, FBExpression.Cheek_Suck_L, 0f },
        { UnifiedExpressions.CheekSuckRight, FBExpression.Cheek_Suck_R, 0f },
        { UnifiedExpressions.CheekSquintLeft, FBExpression.Cheek_Raiser_L, 0f },
        { UnifiedExpressions.CheekSquintRight, FBExpression.Cheek_Raiser_R, 0f },
        { UnifiedExpressions.NoseSneerLeft, FBExpression.Nose_Wrinkler_L, 0f },
        { UnifiedExpressions.NoseSneerRight, FBExpression.Nose_Wrinkler_R, 0f },
    };
}

