using System.Windows.Controls;
using Claudestrap.UI.ViewModels.Pages;

namespace Claudestrap.UI.Elements.Settings.Pages
{
    public partial class HistoryPage : Page
    {
        private readonly HistoryPageViewModel _viewModel;
        public HistoryPage()
        {
            InitializeComponent();
            _viewModel = new HistoryPageViewModel();
            DataContext = _viewModel;
        }
    }
}