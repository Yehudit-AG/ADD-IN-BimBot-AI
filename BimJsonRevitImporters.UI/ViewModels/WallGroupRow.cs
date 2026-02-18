using System.ComponentModel;
using System.Runtime.CompilerServices;
using BimJsonRevitImporter.Domain.Messaging;

namespace BimJsonRevitImporters.UI.ViewModels
{
    public class WallGroupRow : INotifyPropertyChanged
    {
        private double _thicknessCm;
        private int _count;
        private WallTypeInfo _selectedWallType;

        public double ThicknessCm
        {
            get => _thicknessCm;
            set { _thicknessCm = value; RaisePropertyChanged(); }
        }

        public int Count
        {
            get => _count;
            set { _count = value; RaisePropertyChanged(); }
        }

        public WallTypeInfo SelectedWallType
        {
            get => _selectedWallType;
            set { _selectedWallType = value; RaisePropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
