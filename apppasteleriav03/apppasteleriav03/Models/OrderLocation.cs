using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace apppasteleriav03.Models
{
    public class OrderLocation
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("pedido_id")]
        public Guid PedidoId { get; set; }

        [JsonPropertyName("latitud")]
        public double Latitud { get; set; }
        
        [JsonPropertyName("longitud")]
        public double Longitud { get; set; }

        [JsonPropertyName("registro_en")]
        public DateTime RegistroEn { get; set; }

        [JsonPropertyName("dispositivo_id")]
        public string? DispositivoId { get; set; }

        [JsonPropertyName("velocidad")]
        public decimal? Velocidad { get; set; }

        [JsonPropertyName("rumb")]
        public decimal? Rumbo { get; set; }

    }
}
