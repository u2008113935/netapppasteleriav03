using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace apppasteleriav03.Models
{
    public class Order
    {
        [JsonPropertyName("idpedido")]
        public Guid Id { get; set; }

        [JsonPropertyName("userid")]
        public Guid UserId { get; set; }

        [JsonPropertyName ("total")]
        public decimal Total { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("repartidor_asignado")]
        public Guid? RepartidorAsignado { get; set; }

        [JsonPropertyName("latitud_actual")]
        public double? LatitudActual { get; set; }

        [JsonPropertyName("longitud_actual")]
        public double? LongitudActual { get; set; }

        [JsonPropertyName("hora_est_llegada")]
        public DateTime? HoraEstimadaLlegada { get; set; }

        [JsonPropertyName("entregado_en")]
        public DateTime? EntregadoEn { get; set; }

        [JsonIgnore]
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();



    }
}
