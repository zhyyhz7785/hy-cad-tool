using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using HyCADTool.Features.SpongeCity.Domain.Standards;

namespace HyCADTool.Features.SpongeCity.Domain.Models
{
    /// <summary>
    /// LID 设施一行（与 Excel Sheet 设施配置 + 污染削减率 对应）。
    /// 规模、单位调蓄、调蓄量在计算回写后通知面板。
    /// </summary>
    public class FacilityItem : INotifyPropertyChanged
    {
        /// <summary>序号（F1~F8）。</summary>
        public string Code { get; set; } = "";

        /// <summary>设施名称。</summary>
        public string Name { get; set; } = "";

        /// <summary>单位（m² / m³ / m）。</summary>
        public string Unit { get; set; } = "";

        private double _quantity;
        /// <summary>规模数量（面积或容积或长度）。</summary>
        public double Quantity
        {
            get => _quantity;
            set => Set(ref _quantity, value);
        }

        private double _unitVolumeFactor;
        /// <summary>单位调蓄系数（m³/单位）。下沉绿地可取下沉深 m。</summary>
        public double UnitVolumeFactor
        {
            get => _unitVolumeFactor;
            set => Set(ref _unitVolumeFactor, value);
        }

        /// <summary>SS 削减率（0~1）。计算不覆盖用户改过的值。</summary>
        public double EtaSs { get; set; }

        private double _providedVolume;
        /// <summary>承担调蓄量 (m³)。按规范位数舍入后写回。</summary>
        public double ProvidedVolume
        {
            get => _providedVolume;
            set => Set(ref _providedVolume, value);
        }

        /// <summary>说明 / 依据。</summary>
        public string Note { get; set; } = "";

        /// <summary>容积类设施图层。空表示不建层。</summary>
        public string LayerName { get; set; } = "";

        public short AciColor { get; set; } = 7;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set(ref double field, double value, [CallerMemberName] string name = null)
        {
            if (field == value) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    /// <summary>8 类设施默认表 = 京津冀规范的默认设施，避免和注册表各写一份。</summary>
    public static class FacilityCatalog
    {
        public static FacilityItem[] CreateDefaults()
        {
            return SpongeStandardRegistry.GetOrDefault(SpongeStandardIds.Db13).CreateInput().Facilities.ToArray();
        }
    }
}
