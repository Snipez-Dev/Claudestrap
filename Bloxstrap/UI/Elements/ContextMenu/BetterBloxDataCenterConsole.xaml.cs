using Claudestrap.Integrations;
using Claudestrap.UI.Elements.Base;
using Claudestrap.UI.ViewModels.ContextMenu;

namespace Claudestrap.UI.Elements.ContextMenu
{
    public partial class BetterBloxDataCenterConsole
    {
        public BetterBloxDataCenterConsole()
        {
            InitializeComponent();
            var vm = new BetterBloxDataCenterConsoleViewModel();
            DataContext = vm;
        }
    }
}
