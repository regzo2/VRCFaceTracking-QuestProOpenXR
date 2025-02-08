using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;
using System.Security;
using VRCFaceTracking;

public class OpenXRRegistryHelper : Process
{
    private static string originalActiveRuntime;

    public static bool SetActiveRuntime(string desiredRuntime, ref ILogger logger)
    {
        string availableRuntimesKeyPath = @"SOFTWARE\Khronos\OpenXR\1\AvailableRuntimes";

        try
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey(availableRuntimesKeyPath))
            {
                if (key != null)
                {
                    logger.LogInformation($"{key.Name}");
                    string[] runtimeValues = key.GetValueNames();

                    string desiredRuntimePath = null;
                    foreach (string value in runtimeValues)
                    {
                        if (value.Contains(desiredRuntime))
                        {
                            desiredRuntimePath = value;
                            break;
                        }
                    }

                    if (desiredRuntimePath != null)
                    {
                        string activeRuntimeKeyPath = @"SOFTWARE\Khronos\OpenXR\1";
                        using (RegistryKey activeRuntimeKey = Registry.LocalMachine.OpenSubKey(activeRuntimeKeyPath, Utils.HasAdmin))
                        {
                            if (activeRuntimeKey != null)
                            {
                                originalActiveRuntime = (string)activeRuntimeKey.GetValue("ActiveRuntime");
                                logger.LogInformation($"Stored original runtime: {originalActiveRuntime}");

                                if (originalActiveRuntime.Contains(desiredRuntime))
                                    return true;
                                else if (!Utils.HasAdmin)
                                    return false;

                                activeRuntimeKey.SetValue("ActiveRuntime", desiredRuntimePath);
                                logger.LogInformation($"Set active runtime to: {desiredRuntimePath}");
                                return true;
                            }
                            else logger.LogInformation($"activeRuntimeKey null");
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
        }
        
        return false;
    }

    public static void RestoreOriginalActiveRuntime(ref ILogger logger)
    {
        if (Utils.HasAdmin)
            if (originalActiveRuntime != null)
            {
                string activeRuntimeKeyPath = @"SOFTWARE\Khronos\OpenXR\1";
                using (RegistryKey activeRuntimeKey = Registry.LocalMachine.OpenSubKey(activeRuntimeKeyPath, true))
                {
                    if (activeRuntimeKey != null)
                    {
                        // Restore the original ActiveRuntime
                        activeRuntimeKey.SetValue("ActiveRuntime", originalActiveRuntime);
                        logger.LogInformation($"Restored {originalActiveRuntime}");
                    }
                }
            }
    }
}