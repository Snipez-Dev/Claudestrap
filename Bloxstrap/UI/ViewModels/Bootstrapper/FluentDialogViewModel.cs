using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Claudestrap.UI.ViewModels.Bootstrapper
{
    public class FluentDialogViewModel : BootstrapperDialogViewModel
    {
        public SolidColorBrush BackgroundColourBrush { get; set; } = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

        // Header text: launcher name shown top-left of the WEAO-style dialog.
        public string LauncherName => "Claudestrap Launcher";

        // Version pill on the bottom-left.
        public string AppVersionPill => "v" + App.Version;

        // Roblox version-hash pill next to the version pill.
        public string RobloxVersionPill
        {
            get
            {
                var guid = App.State?.Prop?.Player?.VersionGuid;
                if (string.IsNullOrWhiteSpace(guid))
                    guid = App.State?.Prop?.Studio?.VersionGuid;
                return string.IsNullOrWhiteSpace(guid) ? "no build installed" : guid;
            }
        }

        // "Official Roblox" or "RDD (executor: X)" — surfaces the source line in the dialog.
        public string SourceLabel
        {
            get
            {
                if (App.Settings?.Prop?.ExecutorSyncEnabled == true &&
                    !string.IsNullOrWhiteSpace(App.Settings.Prop.SelectedExecutor))
                    return $"Executor Sync · {App.Settings.Prop.SelectedExecutor}";
                return "Official Roblox";
            }
        }

        [Obsolete("Do not use this! This is for the designer only.", true)]
        public FluentDialogViewModel() : base()
        { }

        public FluentDialogViewModel(IBootstrapperDialog dialog, bool aero) : base(dialog)
        {
            const int alpha = 128;

            if (aero)
            {
                BackgroundColourBrush = App.Settings.Prop.Theme2.GetFinal() == Enums.Theme.Light ?
                    new SolidColorBrush(Color.FromArgb(alpha, 225, 225, 225)) :
                    new SolidColorBrush(Color.FromArgb(alpha, 30, 30, 30));
            }
        }
    }
}
