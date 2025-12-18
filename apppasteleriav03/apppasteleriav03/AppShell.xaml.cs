using apppasteleriav03.Views;
using apppasteleriav03.Models;
using apppasteleriav03.Services;
using Microsoft.Maui.Storage;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;


using System.ComponentModel;

namespace apppasteleriav03
{
    public partial class AppShell : Shell, INotifyPropertyChanged
    {

        Profile _currentProfile;
        public Profile CurrentProfile
        {
            get => _currentProfile;
                set
            {
                if (_currentProfile != value)
                {
                    _currentProfile = value;
                    OnPropertyChanged(nameof(CurrentProfile));
                }
            }
        }


        public AppShell()
        {
            InitializeComponent();

            // Registrar rutas para usar con GotoAsync
            //Routing.RegisterRoute("cart", typeof(CartPage));
            Routing.RegisterRoute("checkout", typeof(CheckoutPage));
            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("profile", typeof(ProfilePage));
            //Routing.RegisterRoute("order", typeof(OrdersPage));

            //Asignar el BindingContext con FlyoutHeader
            this.BindingContext = this;

            //Cargar el perfil actual
            _ = LoadProfileAsync();
        }

        // Boton para editar perfil desde el FlyoutHeader
        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//profile");
        }


        async Task LoadProfileAsync()
        {
            try
            {
                var userIdStr = AuthService.Instance.UserId;
                if (string.IsNullOrWhiteSpace(userIdStr))
                
                    userIdStr = await SecureStorage.Default.GetAsync("auth_user_id");

                    if (Guid.TryParse(userIdStr, out Guid userId))
                    {
                        var profile = await SupabaseService.Instance.GetProfileAsync(userId);
                        if (profile != null)
                        {
                            CurrentProfile = profile;
                        }
                    }                
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadingProfileAsync failed: {ex.Message}");
            }
        }
    }
}
