using System;
using System.Text.Json.Serialization;

namespace apppasteleriav03.Models
{
    public class Profile
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        // Alinear con el nombre de columna usado en tu API (se usa "full_name" en el payload)
        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        // Alinear con el nombre de columna usado en tu API (se usa "avatar_url" en el payload)
        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; } = string.Empty;

        [JsonIgnore]

        // Propiedad adicional para el correo electrónico
        public string Email { get; set; } = string.Empty;

        // Propiedad calculada para obtener la URL pública completa del avatar
        public string AvatarPublicUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AvatarUrl)) return null;

                var baseUrl = apppasteleriav03.Services.SupabaseConfig.SUPABASE_URL?.TrimEnd('/');
                if (string.IsNullOrWhiteSpace(baseUrl)) return AvatarUrl;

                return $"{baseUrl}/storage/v1/object/public/{apppasteleriav03.Services.SupabaseConfig.BUCKET_NAME}/{Uri.EscapeDataString(AvatarUrl)}";
            }
        }
    }
}



