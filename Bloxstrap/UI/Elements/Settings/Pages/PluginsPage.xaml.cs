using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui.Hardware;
using Claudestrap.UI.Elements.Dialogs;
using Claudestrap.UI.ViewModels.Settings;
using System.Collections.ObjectModel;

namespace Claudestrap.UI.Elements.Settings.Pages
{
    public partial class PluginsPage
    {
        public PluginsPage()
        {
            InitializeComponent();
            DataContext = new PluginsViewModel();
        }
    }
}

