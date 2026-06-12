using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyCADTool.Features.G101.Domain.Tables
{
    /// <summary>22G101 全局查表设定（面板顶部共享）。</summary>
    public class G101GlobalSettings : INotifyPropertyChanged
    {
        private ConcreteGrade _concreteGrade = ConcreteGrade.C30;
        private RebarGrade _rebarGrade = RebarGrade.HRB400;
        private SeismicGrade _seismicGrade = SeismicGrade.Grade2;
        private EnvironmentClass _environmentClass = EnvironmentClass.ClassI;
        private int _rebarDiameter = 16;
        private double _scale = 40;

        public ConcreteGrade ConcreteGrade
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

        /// <summary>主筋直径 d（mm，实际尺寸）。</summary>
        public int RebarDiameter
        {
            get => _rebarDiameter;
            set { if (_rebarDiameter == value) return; _rebarDiameter = value; OnPropertyChanged(); }
        }

        /// <summary>出图比例（与 SettingsPanel 一致，文字/标注尺寸用）。</summary>
        public double Scale
        {
            get => _scale;
            set { if (System.Math.Abs(_scale - value) < 1e-9) return; _scale = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
