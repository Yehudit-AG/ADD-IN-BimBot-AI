using System.Windows;
using BimJsonRevitImporter.Domain.Messaging;
using BimJsonRevitImporters.UI.ViewModels;

namespace BimJsonRevitImporters.UI.Views
{
    public partial class MainImportWindow : Window
    {
        public MainImportWindow(IRevitEventDispatcher dispatcher)
        {
            InitializeComponent();
            DataContext = new MainImportViewModel(dispatcher);
        }
    }
}
