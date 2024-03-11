using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Meta_OpenXR;

using XrTime = Int64;

[StructLayout(LayoutKind.Sequential)]
public struct FaceWeightsFB
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)ExpressionFB.COUNT)]
    public float[] weights;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)ConfidenceFB.COUNT)]
    public float[] confidences;
    public XrTime time;
}

[StructLayout(LayoutKind.Sequential)]
public struct EyeGazesFB
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public XrPose[] gaze;
    public XrTime time;
}

[StructLayout(LayoutKind.Sequential)]
public struct XrPose
{
    public XrQuaternion orientation;
    public XrVector3 position;
}

[StructLayout(LayoutKind.Sequential)]
public struct XrVector3
{
    public float x;
    public float y;
    public float z;
}

[StructLayout(LayoutKind.Sequential)]
public struct XrQuaternion
{
    public float x;
    public float y;
    public float z;
    public float w;
}

public enum qxrResult {
	SUCCESS,
	RUNTIME_MISSING,
	RUNTIME_FAILURE,
    RUNTIME_FEATURE_UNAVAILABLE,
	INSTANCE_CREATE_FAILURE,
	SESSION_CREATE_FAILURE,
	GRAPHICS_BIND_FAILURE,
	SYSTEM_GET_FAILURE,
	PROPERTY_GET_FAILURE,
	SPACE_CREATE_FAILURE,
	STEREO_VIEW_UNSUPPORTED
};

public enum EyePositionFB {
    EYE_POSITION_LEFT = 0,
    EYE_POSITION_RIGHT = 1,
    COUNT = 2,
    MAX = 0x7FFFFFFF
}

public enum ConfidenceFB
{
    LOWER_FACE = 0,
    UPPER_FACE = 1,
    COUNT = 2,
    MAX = 0x7FFFFFF
}

public enum ExpressionFB
{
    BROW_LOWERER_L = 0,
    BROW_LOWERER_R = 1,
    CHEEK_PUFF_L = 2,
    CHEEK_PUFF_R = 3,
    CHEEK_RAISER_L = 4,
    CHEEK_RAISER_R = 5,
    CHEEK_SUCK_L = 6,
    CHEEK_SUCK_R = 7,
    CHIN_RAISER_B = 8,
    CHIN_RAISER_T = 9,
    DIMPLER_L = 10,
    DIMPLER_R = 11,
    EYES_CLOSED_L = 12,
    EYES_CLOSED_R = 13,
    EYES_LOOK_DOWN_L = 14,
    EYES_LOOK_DOWN_R = 15,
    EYES_LOOK_LEFT_L = 16,
    EYES_LOOK_LEFT_R = 17,
    EYES_LOOK_RIGHT_L = 18,
    EYES_LOOK_RIGHT_R = 19,
    EYES_LOOK_UP_L = 20,
    EYES_LOOK_UP_R = 21,
    INNER_BROW_RAISER_L = 22,
    INNER_BROW_RAISER_R = 23,
    JAW_DROP = 24,
    JAW_SIDEWAYS_LEFT = 25,
    JAW_SIDEWAYS_RIGHT = 26,
    JAW_THRUST = 27,
    LID_TIGHTENER_L = 28,
    LID_TIGHTENER_R = 29,
    LIP_CORNER_DEPRESSOR_L = 30,
    LIP_CORNER_DEPRESSOR_R = 31,
    LIP_CORNER_PULLER_L = 32,
    LIP_CORNER_PULLER_R = 33,
    LIP_FUNNELER_LB = 34,
    LIP_FUNNELER_LT = 35,
    LIP_FUNNELER_RB = 36,
    LIP_FUNNELER_RT = 37,
    LIP_PRESSOR_L = 38,
    LIP_PRESSOR_R = 39,
    LIP_PUCKER_L = 40,
    LIP_PUCKER_R = 41,
    LIP_STRETCHER_L = 42,
    LIP_STRETCHER_R = 43,
    LIP_SUCK_LB = 44,
    LIP_SUCK_LT = 45,
    LIP_SUCK_RB = 46,
    LIP_SUCK_RT = 47,
    LIP_TIGHTENER_L = 48,
    LIP_TIGHTENER_R = 49,
    LIPS_TOWARD = 50,
    LOWER_LIP_DEPRESSOR_L = 51,
    LOWER_LIP_DEPRESSOR_R = 52,
    MOUTH_LEFT = 53,
    MOUTH_RIGHT = 54,
    NOSE_WRINKLER_L = 55,
    NOSE_WRINKLER_R = 56,
    OUTER_BROW_RAISER_L = 57,
    OUTER_BROW_RAISER_R = 58,
    UPPER_LID_RAISER_L = 59,
    UPPER_LID_RAISER_R = 60,
    UPPER_LIP_RAISER_L = 61,
    UPPER_LIP_RAISER_R = 62,
    TONGUE_TIP_INTERDENTAL = 63,
    TONGUE_TIP_ALVEOLAR = 64,
    Tongue_FRONT_DORSAL_PALATE = 65,
    TONGUE_MID_DORSAL_PALATE = 66,
    TONGUE_BACK_DORSAL_VELAR = 67,
    TONGUE_OUT = 68,
    TONGUE_RETREAT = 69,
    COUNT = 70,
    MAX = 0x7FFFFFF
}
