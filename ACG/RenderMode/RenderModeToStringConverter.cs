using System.Globalization;
using System.Windows.Data;

namespace Graphics.UI.Resources;

public class RenderModeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            RenderMode.Shadow => "Shadows",
            RenderMode.Wireframe => "Wireframe",
            RenderMode.Rasterized => "Rasterized",
            _ => value.ToString()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}