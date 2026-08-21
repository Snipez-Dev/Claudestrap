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
    }
}
