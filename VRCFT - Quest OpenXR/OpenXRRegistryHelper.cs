using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;

public class OpenXRRegistryHelper : Process
{
    private static string originalActiveRuntime;

    public static bool SetActiveRuntime(string desiredRuntime, ref ILogger logger)
    {
        string availableRuntimesKeyPath = @"SOFTWARE\Khronos\OpenXR\1\AvailableRuntimes";

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
                    using (RegistryKey activeRuntimeKey = Registry.LocalMachine.OpenSubKey(activeRuntimeKeyPath, true))
                    {
                        if (activeRuntimeKey != null)
                        {
                            originalActiveRuntime = (string)activeRuntimeKey.GetValue("ActiveRuntime");
                            logger.LogInformation($"Stored original runtime: {originalActiveRuntime}");

                            activeRuntimeKey.SetValue("ActiveRuntime", desiredRuntimePath);
                            logger.LogInformation($"Set active runtime to: {desiredRuntimePath}");
                            return true;
                        }
                        else logger.LogInformation($"activeRuntimeKey null");
                    }
                }
            }
            else logger.LogInformation($"key null");
        }

        logger.LogInformation("cry");

        return false;
    }

    public static void RestoreOriginalActiveRuntime(ref ILogger logger)
    {
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