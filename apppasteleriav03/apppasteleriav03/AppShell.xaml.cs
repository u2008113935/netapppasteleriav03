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
        Profile _currentProfile = new Profile { FullName = "Invitado" };
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
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AppShell InitializeComponent failed: {ex}");
            }

            // Registrar sólo rutas que NO están declaradas como ShellContent en XAML
            Routing.RegisterRoute("checkout", typeof(CheckoutPage));
            Routing.RegisterRoute("login", typeof(LoginPage));
            Routing.RegisterRoute("profile", typeof(ProfilePage));
            Routing.RegisterRoute("cart", typeof(Views.CartPage));

            this.BindingContext = this;

            _ = LoadProfileAsync();

            // Suscribirse a navegación para cerrar flyout automáticamente
            this.Navigated += OnShellNavigated;

            try
            {
                // Suscribirse a cambios del carrito para actualizar badge (si procede)
                CartService.Instance.CartChanged += (s, e) => UpdateCartBadge();

                CartService.Instance.Items.CollectionChanged += (s, e) => UpdateCartBadge();
                CartService.Instance.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(CartService.Count) || e.PropertyName == nameof(CartService.Total))
                        UpdateCartBadge();
                };

            }
            catch(Exception ex)
            {
                Debug.WriteLine($"AppShell: failed to subcribe to CartService events: {ex}");
            }
            
            UpdateCartBadge();
        }

        private void OnShellNavigated(object sender, ShellNavigatedEventArgs e)
        {
            // Cerrar el Flyout después de navegar
            try
            {
                if (FlyoutIsPresented)
                    FlyoutIsPresented = false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnShellNavigated error: {ex}");
            }
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
                await MainThread.InvokeOnMainThreadAsync(() => DisplayAlert("Error", "No se pudo abrir el perfil", "OK"));
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
                await MainThread.InvokeOnMainThreadAsync(() => DisplayAlert("Error", "No se pudo abrir el login", "OK"));
            }
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlert(
                    "Acerca de Delicia",
                    "Delicia Pastelería v3.0" +
                    "\n\n" +
                    "La mejor app para ordenar tus pasteles favoritos." +
                    "\n\n" +
                    "© 2025 Todos los derechos reservados",
                    "OK"));
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

                // Asignar CurrentProfile en UI-thread
                // Asignar perfil invitado en UI-thread (evita posibles null refs en bindings)
                await MainThread.InvokeOnMainThreadAsync(() =>
                    CurrentProfile = new Profile { FullName = "Invitado", Email = "Inicia sesión para guardar tus pedidos" });

                await Shell.Current.GoToAsync("//login");
                await DisplayAlert("Sesión cerrada", "Has cerrado sesión exitosamente", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OnLogoutClicked error: {ex.Message}");
                await MainThread.InvokeOnMainThreadAsync(() => 
                DisplayAlert("Error", "No se pudo cerrar la sesión", "OK"));
            }
        }

        async Task LoadProfileAsync()
        {
            try
            {
                Debug.WriteLine("LoadProfileAsync: start");
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

                Debug.WriteLine($"LoadProfileAsync: userIdStr={userIdStr}, authEmail={authEmail}");


                if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out Guid userId))
                {
                    var profile = await SupabaseService.Instance.GetProfileAsync(userId);
                    
                    if (profile != null)
                    {
                        
                        if(!string.IsNullOrWhiteSpace(authEmail))
                        {
                            profile.Email = authEmail;
                        }

                        // Asegurar asignación en hilo UI
                        await MainThread.InvokeOnMainThreadAsync(() => CurrentProfile = profile);
                        Debug.WriteLine($"Profile loaded: {profile.FullName} ({profile.Email})");
                    }
                    else
                    {
                        var fallback = new Profile { FullName = "Usuario" };
                        if (!string.IsNullOrWhiteSpace(authEmail))
                            fallback.Email = authEmail;
                        else
                            fallback.Email = "usuario@ejemplo.com";

                        // fallback: asignar en hilo UI
                        await MainThread.InvokeOnMainThreadAsync(() => CurrentProfile = fallback);

                    }
                }
                else
                {
                    var guest = new Profile { FullName = "Invitado" };
                    if (!string.IsNullOrWhiteSpace(authEmail))
                        guest.Email = authEmail;
                    else
                        guest.Email = "Inicia sesión para guardar tus pedidos";

                    await MainThread.InvokeOnMainThreadAsync(() => CurrentProfile = guest);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadProfileAsync failed: {ex}");
                await MainThread.InvokeOnMainThreadAsync(() => 
                CurrentProfile = new Profile { FullName = "Usuario", Email = "Error al cargar perfil" });
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
                Debug.WriteLine("UpdateCartBadge: start");
                int cartItemCount = 0;

                // Intentamos obtener cuenta de CartService de forma defensiva
                try
                {
                    cartItemCount = CartService.Instance?.GetItemCount() ?? CartService.Instance?.Count ?? 0;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"UpdateCartBadge: error getting cart count: {ex}");
                }

                // Intentar localizar el Tab correspondiente al carrito (varios tipos posibles)
                foreach (var item in Items)
                {
                    try
                    {
                        // ShellItem -> Tab (items inside)
                        foreach (var child in item.Items)
                        {
                            var title = child?.Title;
                            if (title?.Equals("Carrito", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                var method = typeof(Shell).GetMethod("SetTabBarBadge", BindingFlags.Public | BindingFlags.Static);
                                if (method != null)
                                {
                                    try
                                    {
                                        method.Invoke(null, new object[] { child, cartItemCount > 0 ? cartItemCount.ToString() : null });
                                    }
                                    catch (TargetInvocationException tie)
                                    {
                                        Debug.WriteLine($"SetTabBarBadge invocation failed: {tie.InnerException ?? tie}");
                                        // fallback to updating title
                                        MainThread.BeginInvokeOnMainThread(() =>
                                        {
                                            child.Title = cartItemCount > 0 ? $"Carrito ({cartItemCount})" : "Carrito";
                                        });
                                    }
                                }
                                else
                                {
                                    // Alternativa: actualizar el título con la cuenta (simple y compatible)
                                    MainThread.BeginInvokeOnMainThread(() =>
                                    {
                                        child.Title = cartItemCount > 0 ? $"Carrito ({cartItemCount})" : "Carrito";
                                    });
                                }
                                return;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"UpdateCartBadge: error iterating children: {ex}");
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