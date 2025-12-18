using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using apppasteleriav03.Services;
using System.Diagnostics;

namespace apppasteleriav03.Converters
{
    public class ImagePathToUriConverter : IValueConverter
    {
        const string PlaceholderFile = "placeholder_food.png";

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var raw = value as string;
                var url = ImageHelper.Normalize(raw);

                if (string.IsNullOrWhiteSpace(url))
                    return ImageSource.FromFile(PlaceholderFile);

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                    return ImageSource.FromFile(PlaceholderFile);

                return new UriImageSource
                {
                    Uri = uri,
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(1)
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImagePathToUriConverter error: {ex.Message}");
                return ImageSource.FromFile(PlaceholderFile);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}