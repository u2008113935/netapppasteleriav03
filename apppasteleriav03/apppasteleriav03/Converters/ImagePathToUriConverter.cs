using System;
using System.Globalization;
using Microsoft.Maui.Controls;
using apppasteleriav03.Services;
using System.Diagnostics;

namespace apppasteleriav03.Converters
{
    public class ImagePathToUriConverter : IValueConverter
    {
        // Usar la misma constante que en ImageHelper
        const string PlaceholderFile = ImageHelper.DefaultPlaceholder;

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var raw = value as string;
                Debug.WriteLine($"ImagePathToUriConverter: raw='{raw}'");

                var url = ImageHelper.Normalize(raw);
                Debug.WriteLine($"ImagePathToUriConverter: normalized='{url}'");

                if (string.IsNullOrWhiteSpace(url))
                    return ImageSource.FromFile(PlaceholderFile);

                // Data URI
                if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    return ImageSource.FromUri(new Uri(url));

                // Si es una URL absoluta http/https -> usar UriImageSource (mejor caching)
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    return new UriImageSource
                    {
                        Uri = uri,
                        CachingEnabled = true,
                        CacheValidity = TimeSpan.FromDays(1)
                    };
                }

                // Si queda una cadena simple (sin slash) la tratamos como recurso local
                var trimmed = url.Replace('\\', '/').TrimStart('/');
                if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.Contains("/"))
                {
                    return ImageSource.FromFile(trimmed);
                }

                // Fallback: evitar que Glide intente abrir rutas con '/' como fichero local (evita ENOENT)
                return ImageSource.FromFile(PlaceholderFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImagePathToUriConverter error: {ex}");
                return ImageSource.FromFile(PlaceholderFile);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}