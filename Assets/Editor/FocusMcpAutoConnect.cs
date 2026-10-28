using System;
using System.Reflection;
using UnityEditor;

[InitializeOnLoad]
internal static class FocusMcpAutoConnect
{
    static FocusMcpAutoConnect()
    {
        EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
        EditorApplication.delayCall += Connect;
    }

    private static void Connect()
    {
        try
        {
            Type locatorType = FindType("MCPForUnity.Editor.Services.MCPServiceLocator");
            Type modeType = FindType("MCPForUnity.Editor.Services.Transport.TransportMode");
            if (locatorType == null || modeType == null)
            {
                return;
            }

            object transport = locatorType.GetProperty("TransportManager", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (transport == null)
            {
                return;
            }

            object httpMode = Enum.Parse(modeType, "Http");
            MethodInfo isRunning = transport.GetType().GetMethod("IsRunning", new[] { modeType });
            if (isRunning != null && (bool)isRunning.Invoke(transport, new[] { httpMode }))
            {
                return;
            }

            MethodInfo startAsync = transport.GetType().GetMethod("StartAsync", new[] { modeType });
            startAsync?.Invoke(transport, new[] { httpMode });
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning("Focus MCP auto-connect failed: " + ex.Message);
        }
    }

    private static Type FindType(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type found = assemblies[i].GetType(typeName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
