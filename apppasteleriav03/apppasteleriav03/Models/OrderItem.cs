using System;
using System.Text.Json.Serialization;

namespace apppasteleriav03.Models
{
    public class OrderItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        // Alineado con el payload que el servicio está enviando: 'pedido_id'
        [JsonPropertyName("pedido_id")]
        public Guid OrderId { get; set; }

        [JsonPropertyName("producto_id")]
        public Guid ProductId { get; set; }

        [JsonPropertyName("cantidad")]
        public int Quantity { get; set; }

        [JsonPropertyName("precio")]
        public decimal Price { get; set; }
    }
}