using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Claudestrap.Integrations
{
    /// <summary>
    /// Liest und schreibt AMD-Grafiktreiber-Einstellungen über die Windows-Registry.
    /// Schlüssel: HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\XXXX\UMD
    /// </summary>
    public static class AmdProfileHelpers
    {
        private const string GpuClassGuid = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        // Bekannte Registry-Schlüsselnamen für AMD UMD-Einstellungen
        public const string AntiAliasingMode   = "AntiAlias_Mode";
        public const string AntiAliasingLevel  = "AntiAlias_Level";
        public const string AnisotropicMode    = "Anisotropic_Mode";
        public const string AnisotropicLevel   = "Anisotropic_Level";
        public const string TextureQuality     = "TFQ";
        public const string VSyncControl       = "Wait_VBlank";
        public const string TessellationOption = "Tessellation_OPTION";
        public const string TessellationMax    = "Tessellation_MAX_LEVEL";
        public const string MorphologicalAA    = "MCE";
        public const string ShaderCache        = "ShaderCache";
        public const string FrameRateTarget    = "FRTC_MODE";
        public const string FrameRateValue     = "FRTC_TARGET";
        public const string SurfaceFormat      = "SurfaceFormatOptimization";
        public const string ColorDepth         = "ColorDepth";

        /// <summary>Sucht den UMD-Unterschlüssel aller AMD-Grafikadapter.</summary>
        public static List<RegistryKey> FindAmdUmdKeys()
        {
            var result = new List<RegistryKey>();

            try
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(GpuClassGuid, writable: false);
                if (classKey == null) return result;

                foreach (var sub in classKey.GetSubKeyNames())
                {
                    try
                    {
                        var adapterKey = classKey.OpenSubKey(sub, writable: false);
                        if (adapterKey == null) continue;

                        var providerName = adapterKey.GetValue("ProviderName")?.ToString() ?? "";
                        var driverDesc  = adapterKey.GetValue("DriverDesc")?.ToString() ?? "";

                        bool isAmd = providerName.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || providerName.Contains("ATI", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("RX ", StringComparison.OrdinalIgnoreCase);

                        if (!isAmd) { adapterKey.Dispose(); continue; }

                        var umdKey = adapterKey.OpenSubKey("UMD", writable: true);
                        if (umdKey != null) result.Add(umdKey);

                        adapterKey.Dispose();
                    }
                    catch { /* Zugriffsfehler überspringen */ }
                }
            }
            catch { }

            return result;
        }

        /// <summary>Öffnet den ersten AMD-UMD-Schlüssel mit Schreibzugriff.</summary>
        public static RegistryKey? OpenFirstAmdUmdWritable()
        {
            try
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(GpuClassGuid, writable: false);
                if (classKey == null) return null;

                foreach (var sub in classKey.GetSubKeyNames())
                {
                    try
                    {
                        var adapterKey = classKey.OpenSubKey(sub, writable: false);
                        if (adapterKey == null) continue;

                        var providerName = adapterKey.GetValue("ProviderName")?.ToString() ?? "";
                        var driverDesc   = adapterKey.GetValue("DriverDesc")?.ToString() ?? "";

                        bool isAmd = providerName.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || providerName.Contains("ATI", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("RX ", StringComparison.OrdinalIgnoreCase);

                        adapterKey.Dispose();

                        if (!isAmd) continue;

                        // Jetzt mit Schreibzugriff öffnen
                        var writableAdapter = Registry.LocalMachine.OpenSubKey(
                            $@"{GpuClassGuid}\{sub}\UMD", writable: true);

                        if (writableAdapter != null) return writableAdapter;
                    }
                    catch { }
                }
            }
            catch { }

            return null;
        }

        /// <summary>Gibt zurück ob überhaupt ein AMD-Adapter gefunden wurde.</summary>
        public static bool HasAmdAdapter()
        {
            try
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(GpuClassGuid, writable: false);
                if (classKey == null) return false;

                return classKey.GetSubKeyNames().Any(sub =>
                {
                    try
                    {
                        using var adapterKey = classKey.OpenSubKey(sub, writable: false);
                        if (adapterKey == null) return false;
                        var providerName = adapterKey.GetValue("ProviderName")?.ToString() ?? "";
                        var driverDesc   = adapterKey.GetValue("DriverDesc")?.ToString() ?? "";
                        return providerName.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                            || providerName.Contains("ATI", StringComparison.OrdinalIgnoreCase)
                            || driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                            || driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase);
                    }
                    catch { return false; }
                });
            }
            catch { return false; }
        }

        /// <summary>Liest einen Dword-Wert aus dem ersten AMD-UMD-Schlüssel.</summary>
        public static int? ReadDword(string valueName)
        {
            var keys = FindAmdUmdKeys();
            foreach (var key in keys)
            {
                try
                {
                    var val = key.GetValue(valueName);
                    key.Dispose();
                    if (val is int i) return i;
                }
                catch { key.Dispose(); }
            }
            return null;
        }

        /// <summary>Schreibt einen Dword-Wert in alle AMD-UMD-Schlüssel.</summary>
        public static void WriteDword(string valueName, int value)
        {
            try
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(GpuClassGuid, writable: false);
                if (classKey == null) return;

                foreach (var sub in classKey.GetSubKeyNames())
                {
                    try
                    {
                        using var adapterKey = classKey.OpenSubKey(sub, writable: false);
                        if (adapterKey == null) continue;

                        var providerName = adapterKey.GetValue("ProviderName")?.ToString() ?? "";
                        var driverDesc   = adapterKey.GetValue("DriverDesc")?.ToString() ?? "";

                        bool isAmd = providerName.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || providerName.Contains("ATI", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("RX ", StringComparison.OrdinalIgnoreCase);

                        if (!isAmd) continue;

                        using var umdWritable = Registry.LocalMachine.OpenSubKey(
                            $@"{GpuClassGuid}\{sub}\UMD", writable: true);

                        umdWritable?.SetValue(valueName, value, RegistryValueKind.DWord);
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>Löscht einen Wert aus allen AMD-UMD-Schlüsseln (= Treiber-Standard).</summary>
        public static void DeleteValue(string valueName)
        {
            try
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(GpuClassGuid, writable: false);
                if (classKey == null) return;

                foreach (var sub in classKey.GetSubKeyNames())
                {
                    try
                    {
                        using var adapterKey = classKey.OpenSubKey(sub, writable: false);
                        if (adapterKey == null) continue;

                        var providerName = adapterKey.GetValue("ProviderName")?.ToString() ?? "";
                        var driverDesc   = adapterKey.GetValue("DriverDesc")?.ToString() ?? "";

                        bool isAmd = providerName.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || providerName.Contains("ATI", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("RX ", StringComparison.OrdinalIgnoreCase);

                        if (!isAmd) continue;

                        using var umdWritable = Registry.LocalMachine.OpenSubKey(
                            $@"{GpuClassGuid}\{sub}\UMD", writable: true);

                        try { umdWritable?.DeleteValue(valueName, throwOnMissingValue: false); }
                        catch { }
                    }
                    catch { }
                }
            }
            catch { }
        }

        /// <summary>Gibt den Anzeigenamen der erkannten AMD-GPU zurück.</summary>
        public static string GetAdapterName()
        {
            try
            {
                using var classKey = Registry.LocalMachine.OpenSubKey(GpuClassGuid, writable: false);
                if (classKey == null) return "Unknown";

                foreach (var sub in classKey.GetSubKeyNames())
                {
                    try
                    {
                        using var adapterKey = classKey.OpenSubKey(sub, writable: false);
                        if (adapterKey == null) continue;

                        var providerName = adapterKey.GetValue("ProviderName")?.ToString() ?? "";
                        var driverDesc   = adapterKey.GetValue("DriverDesc")?.ToString() ?? "";

                        bool isAmd = providerName.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || providerName.Contains("ATI", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase)
                                  || driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase);

                        if (isAmd && !string.IsNullOrEmpty(driverDesc)) return driverDesc;
                    }
                    catch { }
                }
            }
            catch { }

            return "Unknown AMD GPU";
        }
    }
}
