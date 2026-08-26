using System.Windows;
using Claudestrap.UI.ViewModels;
using Claudestrap.UI.ViewModels.ContextMenu;

namespace Claudestrap.UI.Elements.ContextMenu
{
    public partial class RPCWindow
    {
        public RPCWindow()
        {
            InitializeComponent();
            DataContext = new RPCCustomizerViewModel();
        }

        private void SaveRpcSettings_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.Save();
            MessageBox.Show("RPC-Einstellungen gespeichert! Starte Roblox neu damit sie wirksam werden.",
                "Gespeichert", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
