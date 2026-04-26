using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace HyCADTool.Shared.UI.Converters
{
    /// <summary>
    /// 枚举值转描述文本转换器
    /// 使用 [Description] 特性定义的文本
    /// </summary>
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            var fieldInfo = value.GetType().GetField(value.ToString());
            if (fieldInfo == null)
                return value.ToString();

            var attributes = (DescriptionAttribute[])fieldInfo.GetCustomAttributes(typeof(DescriptionAttribute), false);
            
            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || targetType == null || !targetType.IsEnum)
                return null;

            foreach (var field in targetType.GetFields())
            {
                var attributes = (DescriptionAttribute[])field.GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attributes.Length > 0 && attributes[0].Description == value.ToString())
                {
                    return field.GetValue(null);
                }
                else if (field.Name == value.ToString())
                {
                    return field.GetValue(null);
                }
            }

            return null;
        }
    }
}


