using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace apppasteleriav03.Models
{
    // Asegúrate de usar este modelo (o que tu modelo existente implemente INotifyPropertyChanged)
    public class CartItem : INotifyPropertyChanged
    {
        public Guid ProductId { get; set; }
        public string? Nombre { get; set; }
        public string? ImagenPath { get; set; }

        private decimal _price;
        public decimal Price 
        { 
            get => _price;
            set
            {
                if (_price == value) return;
                _price = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtotal));
            }
            
        }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Subtotal));
            }
        }

        //Propiedad calculada para el subtotal del ítem.
        //Es solo de lectura y se recalcula automáticamente
        public decimal Subtotal => Price * Quantity;

        // Evitar escribir lógica adicional en las propiedades ProductName y ProductImageUrl
        public string? ProductName => Nombre;
        public string? ProductImageUrl => ImagenPath;

        //Impletación de subtotal como propiedad calculada
        public event PropertyChangedEventHandler? PropertyChanged;
        
        //Helper para notificar cambios de propiedad
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}