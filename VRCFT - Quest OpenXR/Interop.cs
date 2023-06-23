using System.Runtime.InteropServices;

namespace VRCFTQuestOpenXRModule;

public partial class QuestProTrackingModule
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
    private static extern int InitOpenXRRuntime();

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    private static extern int UpdateOpenXRFaceTracker();

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    private static extern float GetCheekPuff(int cheekIndex);

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    private static extern void GetEyeOrientation(int eyeIndex, XrQuat outOrientation);

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    private static extern void GetFaceWeights([MarshalAs(UnmanagedType.LPArray, SizeConst = 63)] float[] faceExpressionFB);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string lpPathName);
}
