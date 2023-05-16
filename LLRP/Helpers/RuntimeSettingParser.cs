namespace LLRP.Helpers;

internal static class RuntimeSettingParser
{
    public static bool QuerEnvironmentVariableSwitch(string environmentVariableSettingName, bool defaultValue)
    {
        string? envVar = Environment.GetEnvironmentVariable(environmentVariableSettingName);

        if (!string.IsNullOrEmpty(envVar))
        {
            if (bool.TryParse(envVar, out bool value))
            {
                return value;
            }

            if (uint.TryParse(envVar, out uint intVal))
            {
                return intVal != 0;
            }
        }

        return defaultValue;
    }
}
