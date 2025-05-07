using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyCADTool.ViewModels
{
    public class ClusterAndBasePanelViewModel : INotifyPropertyChanged
    {
        public PointsClusterPanelViewModel PointsClusterVM { get; }
        public BaseDimensionPanelViewModel BaseDimVM { get; }

        public ClusterAndBasePanelViewModel()
        {
            PointsClusterVM = new PointsClusterPanelViewModel();
            BaseDimVM = new BaseDimensionPanelViewModel();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
