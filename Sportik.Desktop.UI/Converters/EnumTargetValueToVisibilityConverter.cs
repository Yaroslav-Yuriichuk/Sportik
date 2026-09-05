using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace Sportik.Desktop.UI.Converters
{
    internal sealed class EnumTargetValueToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(parameter is string targetValuesString))
            {
                return Visibility.Collapsed;
            }

            string[] targetValueStrings = targetValuesString.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string targetValueString in targetValueStrings)
            {
                if (Enum.TryParse(value.GetType(), targetValueString, out object targetValue) &&
                    value.Equals(targetValue))
                {
                    return Visibility.Visible;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
