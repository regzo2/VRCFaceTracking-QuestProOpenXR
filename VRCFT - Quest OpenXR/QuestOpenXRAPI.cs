using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Meta_OpenXR;

public static class QXR
{
    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern qxrResult InitializeSession();

    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern bool CloseSession();

    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern bool CreateFaceTracker();

    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern bool DestroyFaceTracker();

    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern bool GetFaceData(ref FaceWeightsFB expressions);

    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern bool CreateEyeTracker();

    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern bool DestroyEyeTracker();

    [DllImport("Meta-OpenXR-Bridge.dll")]
    public static extern bool GetEyeData(ref EyeGazesFB gazes);
}
