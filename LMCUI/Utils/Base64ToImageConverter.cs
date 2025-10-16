namespace LMCUI.Utils;
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System.IO;

public class Base64ToImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string base64String && !string.IsNullOrEmpty(base64String))
        {
            try
            {
                // 处理可能包含的data URI前缀
                var cleanBase64 = base64String.Contains(",") ? base64String.Split(',')[1] : base64String;
                var bytes = System.Convert.FromBase64String(cleanBase64);
                using (var stream = new MemoryStream(bytes))
                {
                    return new Bitmap(stream);
                }
            }
            catch
            {
                return null;
            }
        }
        return null;
    }


    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}