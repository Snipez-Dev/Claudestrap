using System;
using System.IO;
using System.Text.Json;
using Claudestrap;

public static class ClaudestrapRobloxSettingsManager // lowk didnt know what tf to name this file
{
    public class ClaudestrapRobloxSettings
    {
        public int MemoryCleanerIntervalSeconds { get; set; }
    }

    private static readonly string FolderPath = Paths.Base;

    private static readonly string FilePath =
        Path.Combine(FolderPath, "ClaudestrapRobloxSaves.json");

    public static ClaudestrapRobloxSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new ClaudestrapRobloxSettings();

            string json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ClaudestrapRobloxSettings>(json)
                   ?? new ClaudestrapRobloxSettings();
        }
        catch
        {
            return new ClaudestrapRobloxSettings();
        }
    }

    public static void Save(ClaudestrapRobloxSettings settings)
    {
        try
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(FilePath, json);
        }
        catch
        {
        }
    }
}
