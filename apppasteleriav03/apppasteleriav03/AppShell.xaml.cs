using apppasteleriav03.Views;
using apppasteleriav03.Models;
using apppasteleriav03.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;

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

            // Registrar sólo rutas que NO están declaradas como ShellContent en XAML
            Routing.RegisterRoute("checkout", typeof(CheckoutPage));
            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("profile", typeof(ProfilePage));

            this.BindingContext = this;

            _ = LoadProfileAsync();

            // Suscribirse a navegación para cerrar flyout automáticamente
            this.Navigated += OnShellNavigated;

            // Suscribirse a cambios del carrito para actualizar badge (si procede)
            CartService.Instance.CartChanged += (s, e) => UpdateCartBadge();
            UpdateCartBadge();
        }

        private void OnShellNavigated(object sender, ShellNavigatedEventArgs e)
        {
            // Cerrar el Flyout después de navegar
            if (FlyoutIsPresented)
                FlyoutIsPresented = false;
        }

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

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert(
                "Acerca de Delicia",
                "Delicia Pastelería v3.0\n\nLa mejor app para ordenar tus pasteles favoritos.\n\n© 2025 Todos los derechos reservados",
                "OK");
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Cerrar sesión", "¿Estás seguro que deseas salir de tu cuenta?", "Sí, salir", "Cancelar");
            if (!confirm) return;

            try
            {
                // Limpiar datos de autenticación
                AuthService.Instance.Logout();
                SecureStorage.Default.Remove("auth_user_id");
                SecureStorage.Default.Remove("auth_token");

                CurrentProfile = null;

                await Shell.Current.GoToAsync("//login");
                await DisplayAlert("Sesión cerrada", "Has cerrado sesión exitosamente", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnLogoutClicked error: {ex.Message}");
                await DisplayAlert("Error", "No se pudo cerrar la sesión", "OK");
            }
        }

        async Task LoadProfileAsync()
        {
            try
            {
                var userIdStr = AuthService.Instance.UserId;

                if (string.IsNullOrWhiteSpace(userIdStr))
                    userIdStr = await SecureStorage.Default.GetAsync("auth_user_id");

                string authEmail = AuthService.Instance?.UserEmail;

                if (string.IsNullOrWhiteSpace(authEmail))
                {
                    try
                    {
                        var currentUser = SupabaseService.Instance?.GetCurrentUser();
                        authEmail = currentUser?.Email;
                    }
                    catch 
                    { 
                    //ignorar no critico
                    }
                }


                if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out Guid userId))
                {
                    var profile = await SupabaseService.Instance.GetProfileAsync(userId);
                    
                    if (profile != null)
                    {
                        
                        if(!string.IsNullOrWhiteSpace(authEmail))
                        {
                            profile.Email = authEmail;
                        }
                        
                        CurrentProfile = profile;
                        Debug.WriteLine($"Profile loaded: {profile.FullName} ({profile.Email})");
                    }
                    else
                    {
                        var fallback = new Profile { FullName = "Usuario" };
                        if (!string.IsNullOrWhiteSpace(authEmail))
                            fallback.Email = authEmail;
                        else
                            fallback.Email = "usuario@ejemplo.com";

                        // fallback
                        CurrentProfile = fallback;

                    }
                }
                else
                {
                    var guest = new Profile { FullName = "Invitado" };
                    if (!string.IsNullOrWhiteSpace(authEmail))
                        guest.Email = authEmail;
                    else
                        guest.Email = "Inicia sesión para guardar tus pedidos";

                    CurrentProfile = guest;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProfileAsync failed: {ex.Message}");
                CurrentProfile = new Profile { FullName = "Usuario", Email = "Error al cargar perfil" };
            }
        }

        /// <summary>
        /// Actualiza el badge del carrito. Usa reflexión para invocar Shell.SetTabBarBadge si está disponible
        /// (evita errores de compilación en versiones de MAUI que no exponen ese método).
        /// </summary>
        private void UpdateCartBadge()
        {
            try
            {
                int cartItemCount = CartService.Instance.GetItemCount();

                // Intentar localizar el Tab correspondiente al carrito
                // En nuestra estructura: Items -> FlyoutItem -> Tab -> ShellContent
                foreach (var item in Items)
                {
                    if (item is FlyoutItem flyoutItem)
                    {
                        foreach (var tab in flyoutItem.Items)
                        {
                            // identificar por Title o por ruta
                            if (tab.Title?.Equals("Carrito", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                // Intentar invocar Shell.SetTabBarBadge(object tab, string value) por reflexión
                                var method = typeof(Shell).GetMethod("SetTabBarBadge", BindingFlags.Public | BindingFlags.Static);
                                if (method != null)
                                {
                                    // method signature: SetTabBarBadge(BindableObject, string)
                                    method.Invoke(null, new object[] { tab, cartItemCount > 0 ? cartItemCount.ToString() : null });
                                }
                                else
                                {
                                    // Alternativa: actualizar el título con la cuenta (simple y compatible)
                                    tab.Title = cartItemCount > 0 ? $"Carrito ({cartItemCount})" : "Carrito";
                                }
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateCartBadge error: {ex.Message}");
            }
        }

        // Permite actualizar desde otras páginas
        public void RefreshCartBadge() => UpdateCartBadge();
    }
}