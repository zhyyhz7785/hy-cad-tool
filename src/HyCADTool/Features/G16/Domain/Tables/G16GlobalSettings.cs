using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Tables
{
    /// <summary>16G101 全局查表设定（面板顶部共享）。</summary>
    public class G16GlobalSettings : INotifyPropertyChanged
    {
        private G16ConcreteGrade _concreteGrade = G16ConcreteGrade.C30;
        private RebarGrade _rebarGrade = RebarGrade.HRB400;
        private SeismicGrade _seismicGrade = SeismicGrade.Grade2;
        private EnvironmentClass _environmentClass = EnvironmentClass.ClassI;
        private int _rebarDiameter = 16;

        public G16ConcreteGrade ConcreteGrade
        {
            get => _concreteGrade;
            set { if (_concreteGrade == value) return; _concreteGrade = value; OnPropertyChanged(); }
        }

        public RebarGrade RebarGrade
        {
            get => _rebarGrade;
            set { if (_rebarGrade == value) return; _rebarGrade = value; OnPropertyChanged(); }
        }

        public SeismicGrade SeismicGrade
        {
            get => _seismicGrade;
            set { if (_seismicGrade == value) return; _seismicGrade = value; OnPropertyChanged(); }
        }

        public EnvironmentClass EnvironmentClass
        {
            get => _environmentClass;
            set { if (_environmentClass == value) return; _environmentClass = value; OnPropertyChanged(); }
        }

        public int RebarDiameter
        {
            get => _rebarDiameter;
            set { if (_rebarDiameter == value) return; _rebarDiameter = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
