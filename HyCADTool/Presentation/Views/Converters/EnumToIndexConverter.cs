using System;
using System.Globalization;
using System.Windows.Data;

namespace HyCADTool.Presentation.Views.Converters
{
    /// <summary>
    /// 枚举值与 ComboBox SelectedIndex (int) 之间的双向转换器
    /// 要求枚举值从 0 开始连续编号，且 ComboBoxItem 顺序与枚举定义顺序一致
    /// 
    /// 用法：SelectedIndex="{Binding EnumProperty, Converter={StaticResource EnumToIndexConverter}}"
    /// </summary>
    public class EnumToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return 0;

            return (int)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || targetType == null)
                return null;

            // 处理 Nullable<Enum>
            var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (!enumType.IsEnum)
                return null;

            return Enum.ToObject(enumType, (int)value);
        }
    }
}
