using System;
using System.Windows;

namespace Claudestrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Progress window for the self-updater. The update download is a couple of hundred
    /// megabytes, so without this the app just sat there looking frozen between "yes,
    /// update" and the restart. Every method marshals to the UI thread itself, so the
    /// updater can call them from whichever thread it happens to be running on.
    /// </summary>
    public partial class UpdateProgressDialog
    {
        public UpdateProgressDialog(string version)
        {
            InitializeComponent();

            VersionText.Text = $"Version {version.TrimStart('v', 'V')}";
        }

        /// <summary>Sets the status line, and switches the bar to a marquee for the
        /// steps that have no measurable progress (installing, restarting).</summary>
        public void SetStatus(string status, bool indeterminate = false)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;
                Progress.IsIndeterminate = indeterminate;

                if (indeterminate)
                {
                    SizeText.Text = "";
                    PercentText.Text = "";
                }
            });
        }

        /// <summary>
        /// Reports download progress. <paramref name="total"/> is null when the server
        /// didn't send a content length -- the bar then just shows movement instead of
        /// a made-up percentage.
        /// </summary>
        public void SetProgress(long received, long? total)
        {
            Dispatcher.Invoke(() =>
            {
                if (total is > 0)
                {
                    double percent = Math.Clamp(received * 100.0 / total.Value, 0, 100);

                    Progress.IsIndeterminate = false;
                    Progress.Value = percent;
                    PercentText.Text = $"{percent:0}%";
                    SizeText.Text = $"{FormatSize(received)} of {FormatSize(total.Value)}";
                }
                else
                {
                    Progress.IsIndeterminate = true;
                    PercentText.Text = "";
                    SizeText.Text = FormatSize(received);
                }
            });
        }

        public void CloseDialog() => Dispatcher.Invoke(Close);

        private static string FormatSize(long bytes) => $"{bytes / 1024d / 1024d:0.0} MB";
    }
}
