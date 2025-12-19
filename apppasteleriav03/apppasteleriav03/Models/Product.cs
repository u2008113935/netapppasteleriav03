using System;
using System.Text.Json.Serialization;

namespace apppasteleriav03.Models
{
    /// <summary>
    /// Clase Product mínima y didáctica.
    /// Usa string para Id para evitar problemas con UUID en deserialización.
    /// </summary>
    public class Product
    {
        [JsonPropertyName("idproducto")]
        public Guid Id { get; set; }

        [JsonPropertyName("nombreproducto")]
        public string? Nombre { get; set; }

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; } 

        [JsonPropertyName("categoria")]
        public string? Categoria { get; set; } 

        [JsonPropertyName("imagen_url")]
        public string? ImagenPath { get; set; } 

        [JsonPropertyName("precio")]
        public decimal? Precio { get; set; }

        // Nombre a mostrar (evita nulls en UI)
        public string DisplayName => string.IsNullOrWhiteSpace(Nombre) ? "Producto" : Nombre!;

        /// <summary>
        /// Valida el producto y devuelve una lista de errores. Lista vacía = válido.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            if (Id == Guid.Empty)
                errors.Add("Id inválido");

            if (string.IsNullOrWhiteSpace(Nombre))
                errors.Add("Nombre vacío");

            if (Precio == null)
                errors.Add("Precio no especificado");
            else if (Precio < 0m)
                errors.Add("Precio no puede ser negativo");

            // ImagenPath puede ser null (permitimos fallback), pero si existe comprobamos longitud mínima
            if (ImagenPath != null && ImagenPath.Length < 3)
                errors.Add("ImagenPath inválida");

            return errors;
        }
    }
}