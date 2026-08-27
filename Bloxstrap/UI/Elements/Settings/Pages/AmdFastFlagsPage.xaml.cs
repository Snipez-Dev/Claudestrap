using Claudestrap.UI.ViewModels.Settings;
using System.Windows;

namespace Claudestrap.UI.Elements.Settings.Pages
{
    public partial class AmdFastFlagsPage
    {
        private readonly AmdFastFlagsViewModel _viewModel;

        public AmdFastFlagsPage()
        {
            InitializeComponent();
            _viewModel = new AmdFastFlagsViewModel();
            DataContext = _viewModel;
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Apply();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ResetToDefaults();
        }
    }
}
