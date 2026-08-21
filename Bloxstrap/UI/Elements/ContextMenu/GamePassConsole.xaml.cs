using Claudestrap.Integrations;
using Claudestrap.UI.Elements.Base;
using Claudestrap.UI.ViewModels.ContextMenu;

namespace Claudestrap.UI.Elements.ContextMenu
{
    public partial class GamePassConsole
    {
        public GamePassConsole(long userId)
        {
            InitializeComponent();
            var vm = new GamePassConsoleViewModel();
            DataContext = vm;
            vm.LoadGamePassesCommand.Execute(userId);
        }
    }
}
