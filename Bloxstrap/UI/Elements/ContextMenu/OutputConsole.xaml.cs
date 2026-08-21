using Claudestrap.Integrations;
using Claudestrap.UI.ViewModels.ContextMenu;

namespace Claudestrap.UI.Elements.ContextMenu
{
    public partial class OutputConsole
    {
        public OutputConsole(ActivityWatcher watcher)
        {
            var viewModel = new OutputConsoleViewModel(watcher);

            viewModel.RequestCloseEvent += (_, _) => Close();

            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
