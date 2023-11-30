using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

public static class OpenXR
{
    [StructLayout(LayoutKind.Sequential)]
    public class XrQuat
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    public static extern int InitOpenXRRuntime();

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    public static extern int UpdateOpenXRFaceTracker();

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    public static extern float GetCheekPuff(int cheekIndex);

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    public static extern void GetEyeOrientation(int eyeIndex, XrQuat outOrientation);

    [DllImport("QuestFaceTrackingOpenXR.dll")]
    public static extern void GetFaceWeights([MarshalAs(UnmanagedType.LPArray, SizeConst = 63)] float[] faceExpressionFB);
}
