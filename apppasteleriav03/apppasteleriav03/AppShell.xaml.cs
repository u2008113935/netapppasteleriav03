using apppasteleriav03.Views;
using apppasteleriav03.Models;
using apppasteleriav03.Services;
using Microsoft.Maui.Storage;
using System;
using System.Diagnostics;
using System.Threading.Tasks;


namespace apppasteleriav03
{
    public partial class AppShell : Shell
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

            // Suscribirse a eventos de navegación
            this.Navigated += OnShellNavigated;
        }

        #region Navigation Events

        /// <summary>
        /// Maneja eventos de navegación para cerrar el Flyout automáticamente
        /// </summary>
        private void OnShellNavigated(object sender, ShellNavigatedEventArgs e)
        {
            // Cerrar el Flyout después de navegar
            if (FlyoutIsPresented)
            {
                FlyoutIsPresented = false;
            }
        }

        #endregion

        //#region Button Click Handlers

        // Boton para editar perfil desde el FlyoutHeader
        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("//profile");
                FlyoutIsPresented = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnEditProfileClicked error: {ex.Message}");
                await DisplayAlert("Error", "No se pudo abrir el perfil", "OK");
            }
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("//login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnLoginClicked error:  {ex.Message}");
                await DisplayAlert("Error", "No se pudo abrir el login", "OK");
            }
        }

        /// <summary>
        /// Mostrar información de la app
        /// </summary>
        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert(
                "Acerca de Delicia",
                "Delicia Pastelería v3.0\n\n" +
                "La mejor app para ordenar tus pasteles favoritos.\n\n" +
                "© 2025 Todos los derechos reservados",
                "OK");
        }

        /// <summary>
        /// Cerrar sesión del usuario
        /// </summary>
        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Cerrar sesión",
                "¿Estás seguro que deseas salir de tu cuenta?",
                "Sí, salir",
                "Cancelar");

            if (confirm)
            {
                try
                {
                    // Limpiar datos de autenticación
                    AuthService.Instance.Logout();
                    SecureStorage.Default.Remove("auth_user_id");
                    SecureStorage.Default.Remove("auth_token");

                    // Limpiar perfil actual
                    CurrentProfile = null;

                    // Navegar a login
                    await Shell.Current.GoToAsync("//login");

                    await DisplayAlert("Sesión cerrada", "Has cerrado sesión exitosamente", "OK");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnLogoutClicked error: {ex.Message}");
                    await DisplayAlert("Error", "No se pudo cerrar la sesión", "OK");
                }
            }
        }

//#endregion

        private async Task LoadProfileAsync()
        {
            try
            {
                var userIdStr = AuthService.Instance.UserId;

                if (string.IsNullOrWhiteSpace(userIdStr))
                {
                    userIdStr = await SecureStorage.Default.GetAsync("auth_user_id");
                }

                if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out Guid userId))
                {
                    var profile = await SupabaseService.Instance.GetProfileAsync(userId);

                    if (profile != null)
                    {
                        CurrentProfile = profile;
                        Debug.WriteLine($"Profile loaded: {profile.FullName}");
                    }
                    else
                    {
                        Debug.WriteLine("Profile not found in database");
                        // Crear perfil por defecto si no existe
                        CurrentProfile = new Profile
                        {
                            FullName = "Usuario",
                            Email = "usuario@ejemplo.com"
                        };
                    }
                }
                else
                {
                    Debug.WriteLine("No user ID found - user not logged in");
                    CurrentProfile = new Profile
                    {
                        FullName = "Invitado",
                        Email = "Inicia sesión para guardar tus pedidos"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProfileAsync failed: {ex.Message}");

                // Perfil fallback en caso de error
                CurrentProfile = new Profile
                {
                    FullName = "Usuario",
                    Email = "Error al cargar perfil"
                };
            }
        }

        /// Método público para recargar el perfil (llamar después de editar)
        /// </summary>
        public async Task RefreshProfileAsync()
        {
            await LoadProfileAsync();
        }

#endregion

        #region Badge Management

        /// <summary>
        /// Actualizar el badge del carrito con la cantidad de items
        /// </summary>
        private void UpdateCartBadge()
        {
            try
            {
                // Obtener cantidad de items en el carrito
                int cartItemCount = CartService.Instance.GetItemCount();

                if (cartItemCount > 0)
                {
                    // Buscar el Tab del carrito (segundo tab, índice 1)
                    if (Items.Count > 0 && Items[0] is TabBar tabBar)
                    {
                        if (tabBar.Items.Count > 1)
                        {
                            var cartTab = tabBar.Items[1]; // Tab del carrito
                            Shell.SetTabBarBadge(cartTab, cartItemCount.ToString());
                        }
                    }
                }
                else
                {
                    // Limpiar badge si no hay items
                    if (Items.Count > 0 && Items[0] is TabBar tabBar)
                    {
                        if (tabBar.Items.Count > 1)
                        {
                            var cartTab = tabBar.Items[1];
                            Shell.SetTabBarBadge(cartTab, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCartBadge error: {ex.Message}");
            }
        }

        /// <summary>
        /// Método público para actualizar badge desde otras páginas
        /// </summary>
        public void RefreshCartBadge()
        {
            UpdateCartBadge();
        }

        //#endregion

    }

}
