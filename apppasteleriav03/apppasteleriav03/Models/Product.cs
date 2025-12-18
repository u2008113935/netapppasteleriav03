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
        public string Nombre { get; set; }

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } 

        [JsonPropertyName("categoria")]
        public string Categoria { get; set; } 

        [JsonPropertyName("imagen_url")]
        public string? ImagenPath { get; set; } 

        [JsonPropertyName("precio")]
        public decimal? Precio { get; set; } 

            }
}