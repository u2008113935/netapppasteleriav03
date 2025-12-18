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


        [JsonIgnore]
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();



    }
}
