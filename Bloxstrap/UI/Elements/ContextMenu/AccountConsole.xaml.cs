using System;
using System.Windows;
using System.Windows.Input;
using Claudestrap.UI.ViewModels;
using Claudestrap.Utility;

namespace Claudestrap.UI.Elements.ContextMenu
{
    public partial class AccountManagerWindow
    {
        public AccountManagerWindow()
        {
            InitializeComponent();
            DataContext = new AccountManagerViewModel();
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            WindowDrag.Begin(this);
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = System.Windows.WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
