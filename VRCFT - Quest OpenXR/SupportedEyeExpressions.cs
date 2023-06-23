using VRCFaceTracking.Core.Params.Expressions;

namespace VRCFTQuestOpenXRModule;

public partial class QuestProTrackingModule
{
    /// <summary>
    /// A read-only collection of supported eye expressions that don't require any additional processing.
    /// </summary>
    public static readonly TwoKeyDictionary<UnifiedExpressions, FBExpression, float> SupportedEyeExpressions = new()
    {
        { UnifiedExpressions.EyeWideLeft, FBExpression.Upper_Lid_Raiser_L, 0f },
        { UnifiedExpressions.EyeWideRight, FBExpression.Upper_Lid_Raiser_R, 0f },
        { UnifiedExpressions.EyeSquintLeft, FBExpression.Lid_Tightener_L, 0f },
        { UnifiedExpressions.EyeSquintRight, FBExpression.Lid_Tightener_R, 0f },
        { UnifiedExpressions.BrowInnerUpLeft, FBExpression.Inner_Brow_Raiser_L, 0f },
        { UnifiedExpressions.BrowInnerUpRight, FBExpression.Inner_Brow_Raiser_R, 0f },
        { UnifiedExpressions.BrowOuterUpLeft, FBExpression.Outer_Brow_Raiser_L, 0f },
        { UnifiedExpressions.BrowOuterUpRight, FBExpression.Outer_Brow_Raiser_R, 0f },
        { UnifiedExpressions.BrowPinchLeft, FBExpression.Brow_Lowerer_L, 0f },
        { UnifiedExpressions.BrowLowererLeft, FBExpression.Brow_Lowerer_L, 0f },
        { UnifiedExpressions.BrowPinchRight, FBExpression.Brow_Lowerer_R, 0f },
        { UnifiedExpressions.BrowLowererRight, FBExpression.Brow_Lowerer_R, 0f }
    };
}
