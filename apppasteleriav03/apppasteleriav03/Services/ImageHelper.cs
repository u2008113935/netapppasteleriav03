using System;
using System.Diagnostics;

namespace apppasteleriav03.Services
{
    /// <summary>
    /// Normaliza rutas de imagen provenientes del API.
    /// Devuelve una URL absoluta válida o null si no hay ruta.
    /// </summary>
    public static class ImageHelper
    {
        // Nombre del fichero placeholder usado por la app (un único sitio de verdad)
        public const string DefaultPlaceholder = "placeholder_food.png";

        public static string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            // Descodifica y limpia
            var decoded = Uri.UnescapeDataString(raw).Trim();

            // Eliminar comillas sobrantes (codificadas o literales) que puedan romper tokens
            decoded = decoded.Replace("%22", string.Empty).Trim().Trim('"').Trim('\'');

            // Si ya es una URL absoluta (signed o pública) la usamos tal cual
            if (Uri.TryCreate(decoded, UriKind.Absolute, out var absolute))
            {
                Debug.WriteLine($"ImageHelper: detected absolute URL -> {absolute}");
                return absolute.ToString();
            }

            try
            {
                var baseUrl = SupabaseConfig.SUPABASE_URL?.TrimEnd('/') ?? string.Empty;
                var bucket = SupabaseConfig.BUCKET_NAME ?? string.Empty;

                if (!string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(bucket))
                {
                    var basePublic = $"{baseUrl}/storage/v1/object/public/{bucket}/";

                    // Si la cadena decodificada ya contiene la base pública (por ejemplo venía codificada)
                    if (decoded.StartsWith(basePublic, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"ImageHelper: starts with basePublic -> {decoded}");
                        return decoded;
                    }

                    var doubleDecoded = Uri.UnescapeDataString(decoded);
                    if (doubleDecoded.StartsWith(basePublic, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"ImageHelper: double-decoded starts with basePublic -> {doubleDecoded}");
                        return doubleDecoded;
                    }

                    // Construir URL pública a partir de un filename/relative path
                    var fileName = decoded.TrimStart('/');
                    var result = $"{basePublic}{Uri.EscapeDataString(fileName)}";
                    Debug.WriteLine($"ImageHelper: built public URL -> {result}");
                    return result;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ImageHelper.Normalize error: {ex.Message}");
            }

            // Fallback: devolver la versión decodificada
            Debug.WriteLine($"ImageHelper: returning decoded fallback -> {decoded}");
            return decoded;
        }
    }
}