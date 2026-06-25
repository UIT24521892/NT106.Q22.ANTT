using System;
using System.IO;
using UnityEngine;

[Serializable]
public class ServerConnectionConfigData
{
    public string Host = "127.0.0.1";
    public int Port = 8080;
    public int ConnectTimeoutSeconds = 8;
}

public static class ServerConnectionConfig
{
    private const string ConfigFileName = "server-config.json";
    private static ServerConnectionConfigData cached;

    public static ServerConnectionConfigData Current
    {
        get
        {
            if (cached == null)
                cached = Load();

            return cached;
        }
    }

    public static void Reload()
    {
        cached = Load();
    }

    private static ServerConnectionConfigData Load()
    {
        ServerConnectionConfigData config = new ServerConnectionConfigData();

        try
        {
            string path = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                ServerConnectionConfigData loaded = JsonUtility.FromJson<ServerConnectionConfigData>(json);
                if (loaded != null)
                    config = loaded;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ServerConnectionConfig] Cannot read config: {ex.Message}");
        }

        string hostOverride = GetArgumentValue("--server-host");
        string portOverride = GetArgumentValue("--server-port");

        if (!string.IsNullOrWhiteSpace(hostOverride))
            config.Host = hostOverride.Trim();
        if (int.TryParse(portOverride, out int parsedPort))
            config.Port = parsedPort;

        if (string.IsNullOrWhiteSpace(config.Host))
            config.Host = "127.0.0.1";
        config.Port = Mathf.Clamp(config.Port, 1, 65535);
        config.ConnectTimeoutSeconds = Mathf.Clamp(config.ConnectTimeoutSeconds, 2, 30);
        return config;
    }

    private static string GetArgumentValue(string key)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return "";
    }
}
