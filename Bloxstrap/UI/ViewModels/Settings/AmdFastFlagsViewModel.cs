using Claudestrap.Integrations;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Claudestrap.UI.ViewModels.Settings
{
    public sealed class AmdFastFlagsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Erkannte Hardware ──────────────────────────────────────────────
        private string _adapterName = "Kein AMD-Adapter gefunden";
        public string AdapterName
        {
            get => _adapterName;
            private set => Set(ref _adapterName, value);
        }

        private bool _hasAdapter;
        public bool HasAdapter
        {
            get => _hasAdapter;
            private set => Set(ref _hasAdapter, value);
        }

        // ── Anti-Aliasing ──────────────────────────────────────────────────
        public ObservableCollection<string> AntiAliasingModes { get; } =
            new() { "Disabled (Application Controlled)", "Enhanced", "Override" };

        public ObservableCollection<string> AntiAliasingLevels { get; } =
            new() { "None", "2x", "4x", "8x" };

        private string _aaMode = "Disabled (Application Controlled)";
        public string SelectedAntiAliasingMode
        {
            get => _aaMode;
            set => Set(ref _aaMode, value);
        }

        private string _aaLevel = "None";
        public string SelectedAntiAliasingLevel
        {
            get => _aaLevel;
            set => Set(ref _aaLevel, value);
        }

        // ── Anisotropic Filtering ──────────────────────────────────────────
        public ObservableCollection<string> AnisotropicModes { get; } =
            new() { "Disabled", "Enabled" };

        public ObservableCollection<string> AnisotropicLevels { get; } =
            new() { "2x", "4x", "8x", "16x" };

        private string _afMode = "Disabled";
        public string SelectedAnisotropicMode
        {
            get => _afMode;
            set => Set(ref _afMode, value);
        }

        private string _afLevel = "16x";
        public string SelectedAnisotropicLevel
        {
            get => _afLevel;
            set => Set(ref _afLevel, value);
        }

        // ── Texture Filtering Quality ──────────────────────────────────────
        public ObservableCollection<string> TextureQualityModes { get; } =
            new() { "Performance", "Balanced", "Quality", "High Quality" };

        private string _texQuality = "Quality";
        public string SelectedTextureQuality
        {
            get => _texQuality;
            set => Set(ref _texQuality, value);
        }

        // ── VSync ──────────────────────────────────────────────────────────
        public ObservableCollection<string> VSyncModes { get; } =
            new() { "Off", "On", "Always On" };

        private string _vsync = "Off";
        public string SelectedVSync
        {
            get => _vsync;
            set => Set(ref _vsync, value);
        }

        // ── Tessellation ───────────────────────────────────────────────────
        public ObservableCollection<string> TessellationModes { get; } =
            new() { "Application Controlled", "Disable", "Override (Max)" };

        private string _tessMode = "Application Controlled";
        public string SelectedTessellationMode
        {
            get => _tessMode;
            set => Set(ref _tessMode, value);
        }

        // ── Morphological AA ───────────────────────────────────────────────
        private bool _morphologicalAA;
        public bool EnableMorphologicalAA
        {
            get => _morphologicalAA;
            set => Set(ref _morphologicalAA, value);
        }

        // ── Surface Format Optimization ────────────────────────────────────
        private bool _surfaceFormat = true;
        public bool EnableSurfaceFormatOptimization
        {
            get => _surfaceFormat;
            set => Set(ref _surfaceFormat, value);
        }

        // ── Frame Rate Target Control (FRTC) ────────────────────────────────
        private bool _frtcEnabled;
        public bool EnableFRTC
        {
            get => _frtcEnabled;
            set
            {
                Set(ref _frtcEnabled, value);
                OnPropertyChanged(nameof(FrtcLabel));
            }
        }

        private int _frtcTarget = 60;
        public int FRTCTarget
        {
            get => _frtcTarget;
            set
            {
                Set(ref _frtcTarget, Math.Clamp(value, 10, 300));
                OnPropertyChanged(nameof(FrtcLabel));
            }
        }

        public string FrtcLabel => EnableFRTC
            ? $"Frame Rate Target: {FRTCTarget} FPS"
            : "Frame Rate Target: Disabled";

        // ──────────────────────────────────────────────────────────────────
        public AmdFastFlagsViewModel()
        {
            HasAdapter = AmdProfileHelpers.HasAmdAdapter();
            AdapterName = HasAdapter
                ? AmdProfileHelpers.GetAdapterName()
                : "Kein AMD/Radeon-Adapter gefunden";

            if (HasAdapter) Load();
        }

        // ── Load ──────────────────────────────────────────────────────────
        private void Load()
        {
            // Anti-Aliasing Mode (0=App, 1=Enhance, 2=Override)
            var aaMode = AmdProfileHelpers.ReadDword(AmdProfileHelpers.AntiAliasingMode);
            _aaMode = aaMode switch { 1 => "Enhanced", 2 => "Override", _ => "Disabled (Application Controlled)" };

            // Anti-Aliasing Level (0=off, 1=2x, 2=4x, 3=8x)
            var aaLevel = AmdProfileHelpers.ReadDword(AmdProfileHelpers.AntiAliasingLevel);
            _aaLevel = aaLevel switch { 1 => "2x", 2 => "4x", 3 => "8x", _ => "None" };

            // Anisotropic Mode (0=off, 1=on)
            var afMode = AmdProfileHelpers.ReadDword(AmdProfileHelpers.AnisotropicMode);
            _afMode = afMode == 1 ? "Enabled" : "Disabled";

            // Anisotropic Level (2=2x, 4=4x, 8=8x, 16=16x)
            var afLevel = AmdProfileHelpers.ReadDword(AmdProfileHelpers.AnisotropicLevel);
            _afLevel = afLevel switch { 2 => "2x", 4 => "4x", 8 => "8x", _ => "16x" };

            // Texture Quality (0=Perf, 1=Balanced, 2=Quality, 3=HQ)
            var tfq = AmdProfileHelpers.ReadDword(AmdProfileHelpers.TextureQuality);
            _texQuality = tfq switch { 0 => "Performance", 1 => "Balanced", 3 => "High Quality", _ => "Quality" };

            // VSync (0=off, 1=on, 2=always)
            var vsync = AmdProfileHelpers.ReadDword(AmdProfileHelpers.VSyncControl);
            _vsync = vsync switch { 1 => "On", 2 => "Always On", _ => "Off" };

            // Tessellation (0=app, 1=disable, 2=override)
            var tess = AmdProfileHelpers.ReadDword(AmdProfileHelpers.TessellationOption);
            _tessMode = tess switch { 1 => "Disable", 2 => "Override (Max)", _ => "Application Controlled" };

            // Morphological AA (0=off, 1=on)
            var mce = AmdProfileHelpers.ReadDword(AmdProfileHelpers.MorphologicalAA);
            _morphologicalAA = mce == 1;

            // Surface Format Optimization
            var sfo = AmdProfileHelpers.ReadDword(AmdProfileHelpers.SurfaceFormat);
            _surfaceFormat = sfo != 0;

            // FRTC
            var frtcMode = AmdProfileHelpers.ReadDword(AmdProfileHelpers.FrameRateTarget);
            _frtcEnabled = frtcMode == 1;
            var frtcVal = AmdProfileHelpers.ReadDword(AmdProfileHelpers.FrameRateValue);
            if (frtcVal.HasValue && frtcVal.Value > 0) _frtcTarget = frtcVal.Value;
        }

        // ── Apply ─────────────────────────────────────────────────────────
        public void Apply()
        {
            if (!HasAdapter)
            {
                MessageBox.Show(
                    "Es wurde kein AMD/Radeon-Adapter gefunden. Einstellungen können nicht gespeichert werden.",
                    "Kein AMD-Adapter", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Anti-Aliasing Mode
            int aaModeVal = SelectedAntiAliasingMode switch
            {
                "Enhanced" => 1,
                "Override" => 2,
                _ => 0
            };
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.AntiAliasingMode, aaModeVal);

            // Anti-Aliasing Level
            int aaLevelVal = SelectedAntiAliasingLevel switch
            {
                "2x" => 1, "4x" => 2, "8x" => 3, _ => 0
            };
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.AntiAliasingLevel, aaLevelVal);

            // Anisotropic Mode
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.AnisotropicMode,
                SelectedAnisotropicMode == "Enabled" ? 1 : 0);

            // Anisotropic Level
            int afLevelVal = SelectedAnisotropicLevel switch
            {
                "2x" => 2, "4x" => 4, "8x" => 8, _ => 16
            };
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.AnisotropicLevel, afLevelVal);

            // Texture Quality
            int tfqVal = SelectedTextureQuality switch
            {
                "Performance" => 0, "Balanced" => 1, "High Quality" => 3, _ => 2
            };
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.TextureQuality, tfqVal);

            // VSync
            int vsyncVal = SelectedVSync switch
            {
                "On" => 1, "Always On" => 2, _ => 0
            };
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.VSyncControl, vsyncVal);

            // Tessellation
            int tessVal = SelectedTessellationMode switch
            {
                "Disable" => 1, "Override (Max)" => 2, _ => 0
            };
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.TessellationOption, tessVal);

            // Morphological AA
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.MorphologicalAA,
                EnableMorphologicalAA ? 1 : 0);

            // Surface Format
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.SurfaceFormat,
                EnableSurfaceFormatOptimization ? 1 : 0);

            // FRTC
            AmdProfileHelpers.WriteDword(AmdProfileHelpers.FrameRateTarget,
                EnableFRTC ? 1 : 0);
            if (EnableFRTC)
                AmdProfileHelpers.WriteDword(AmdProfileHelpers.FrameRateValue, FRTCTarget);

            MessageBox.Show(
                "AMD-Einstellungen wurden gespeichert. Starte Roblox neu damit sie wirksam werden.",
                "Gespeichert", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── Reset ────────────────────────────────────────────────────────
        public void ResetToDefaults()
        {
            if (!HasAdapter) return;

            var confirm = MessageBox.Show(
                "Alle AMD-Einstellungen auf Treiber-Standard zurücksetzen?",
                "Zurücksetzen", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.AntiAliasingMode);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.AntiAliasingLevel);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.AnisotropicMode);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.AnisotropicLevel);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.TextureQuality);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.VSyncControl);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.TessellationOption);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.TessellationMax);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.MorphologicalAA);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.SurfaceFormat);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.FrameRateTarget);
            AmdProfileHelpers.DeleteValue(AmdProfileHelpers.FrameRateValue);

            Load();
            OnPropertyChanged(string.Empty);

            MessageBox.Show("AMD-Einstellungen zurückgesetzt.", "Zurückgesetzt",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────
        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return;
            field = value;
            OnPropertyChanged(name);
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
