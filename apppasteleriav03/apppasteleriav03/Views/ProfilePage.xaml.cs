using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;
using apppasteleriav03.Models;
using apppasteleriav03.Services;

namespace apppasteleriav03.Views
{
    /// <summary>
    /// Code-behind de ProfilePage.xaml
    /// - Carga el perfil del usuario usando SupabaseService.GetProfileAsync
    /// - Permite editar FullName y AvatarPath (ruta dentro del bucket)
    /// - Guarda cambios con un PATCH a /rest/v1/profiles (se realiza desde aquí usando SupabaseConfig)
    /// Nota: para subir un archivo al Storage necesitarás implementar upload (no incluido aquí).
    /// </summary>
    public partial class ProfilePage : ContentPage
    {
        readonly SupabaseService _supabase = new SupabaseService(); // servicio para leer perfil
        Profile _profile; // modelo cargado

        public ProfilePage()
        {
            InitializeComponent();
            // Cargar datos cuando la página aparece
            _ = LoadProfileAsync();
        }

        // Carga el perfil del usuario actual (leer user_id de SecureStorage)
        async Task LoadProfileAsync()
        {
            try
            {
                InfoLabel.Text = "Cargando perfil...";
                // Leer user_id almacenado (guardado en el login)
                var userIdStr = await SecureStorage.Default.GetAsync("user_id");
                if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
                {
                    InfoLabel.Text = "No se encontró sesión. Inicia sesión primero.";
                    return;
                }

                // Guardar userId en UI
                UserIdEntry.Text = userId.ToString();

                // Obtener perfil desde Supabase (servicio)
                _profile = await _supabase.GetProfileAsync(userId);
                if (_profile == null)
                {
                    InfoLabel.Text = "Perfil no encontrado. Puedes crear tus datos.";
                    // Inicializar valores mínimos
                    FullNameEntry.Text = string.Empty;
                    AvatarPathEntry.Text = string.Empty;
                    SetAvatarImage(null);
                    return;
                }

                // Llenar controles con los datos del perfil
                FullNameEntry.Text = _profile.FullName ?? string.Empty;
                AvatarPathEntry.Text = _profile.AvatarUrl ?? string.Empty;

                // AvatarPublicUrl es una propiedad auxiliar en el modelo que construye la URL pública
                SetAvatarImage(_profile.AvatarPublicUrl);

                InfoLabel.Text = "Perfil cargado";
            }
            catch (Exception ex)
            {
                InfoLabel.Text = $"Error cargando perfil: {ex.Message}";
            }
        }

        // Muestra la imagen del avatar o una imagen placeholder si es null/empty
        void SetAvatarImage(string avatarPublicUrl)
        {
            if (!string.IsNullOrWhiteSpace(avatarPublicUrl))
            {
                AvatarImage.Source = ImageSource.FromUri(new Uri(avatarPublicUrl));
            }
            else
            {
                // Imagen por defecto embebida o recurso local
                AvatarImage.Source = "avatar_placeholder.png"; // asegúrate de tener este recurso o cambia por otro
            }
        }

        // Evento: activar modo edición (habilita campos y botones Guardar/Cancelar)
        void OnEditClicked(object sender, EventArgs e)
        {
            FullNameEntry.IsEnabled = true;
            AvatarPathEntry.IsEnabled = true;
            SaveButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
            EditButton.IsEnabled = false;
            InfoLabel.Text = "Modo edición activo";
        }

        // Evento: cancelar edición (restaurar valores originales)
        void OnCancelClicked(object sender, EventArgs e)
        {
            if (_profile != null)
            {
                FullNameEntry.Text = _profile.FullName;
                AvatarPathEntry.Text = _profile.AvatarUrl;
                SetAvatarImage(_profile.AvatarPublicUrl);
            }
            else
            {
                FullNameEntry.Text = string.Empty;
                AvatarPathEntry.Text = string.Empty;
                SetAvatarImage(null);
            }

            FullNameEntry.IsEnabled = false;
            AvatarPathEntry.IsEnabled = false;
            SaveButton.IsEnabled = false;
            CancelButton.IsEnabled = false;
            EditButton.IsEnabled = true;
            InfoLabel.Text = "Edición cancelada";
        }

        // Evento: guardar cambios en la tabla profiles usando PATCH a PostgREST
        async void OnSaveClicked(object sender, EventArgs e)
        {
            // Validación básica
            var newName = FullNameEntry.Text?.Trim();
            var newAvatarPath = AvatarPathEntry.Text?.Trim();

            if (string.IsNullOrEmpty(newName))
            {
                await DisplayAlert("Validación", "El nombre no puede estar vacío.", "OK");
                return;
            }

            // Obtener user id desde UI (o SecureStorage)
            if (!Guid.TryParse(UserIdEntry.Text, out var userId))
            {
                await DisplayAlert("Error", "UserId inválido.", "OK");
                return;
            }

            // Construir payload con los campos a actualizar
            var payload = new { full_name = newName, avatar_url = string.IsNullOrEmpty(newAvatarPath) ? null : newAvatarPath };

            // Mostrar indicador simple en InfoLabel
            InfoLabel.Text = "Guardando cambios...";
            SaveButton.IsEnabled = false;
            CancelButton.IsEnabled = false;

            try
            {
                // Construir la URL PATCH a PostgREST (profiles)
                var baseUrl = SupabaseConfig.SUPABASE_URL.TrimEnd('/');
                var url = $"{baseUrl}/rest/v1/profiles?id=eq.{Uri.EscapeDataString(userId.ToString())}";

                // Preparar HttpClient y cabeceras (igual que SupabaseService)
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("apikey", SupabaseConfig.SUPABASE_ANON_KEY);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SupabaseConfig.SUPABASE_ANON_KEY);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Serializar payload y armar la petición PATCH
                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(new HttpMethod("PATCH"), url) { Content = content };
                // Prefer:return=representation pide que la API devuelva la fila actualizada
                req.Headers.Add("Prefer", "return=representation");

                // Ejecutar petición
                var resp = await client.SendAsync(req);
                var respText = await resp.Content.ReadAsStringAsync();

                // Verificar resultado
                if (!resp.IsSuccessStatusCode)
                {
                    // Mostrar el body del error para depuración; en producción parsear y mostrar mensaje amigable
                    await DisplayAlert("Error", $"No se pudo actualizar el perfil: {resp.StatusCode}\n{respText}", "OK");
                    InfoLabel.Text = "Error al guardar";
                }
                else
                {
                    // Deserializar la representación devuelta (array) y actualizar UI/local state
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var list = JsonSerializer.Deserialize<Profile[]>(respText, options);
                    if (list != null && list.Length > 0)
                    {
                        _profile = list[0];
                        FullNameEntry.Text = _profile.FullName;
                        AvatarPathEntry.Text = _profile.AvatarUrl;
                        SetAvatarImage(_profile.AvatarPublicUrl);
                        InfoLabel.Text = "Perfil actualizado correctamente";
                    }
                    else
                    {
                        InfoLabel.Text = "Perfil actualizado (sin representación devuelta).";
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Exception: {ex.Message}", "OK");
                InfoLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                // Restaurar estados de botones (salir de modo edición)
                FullNameEntry.IsEnabled = false;
                AvatarPathEntry.IsEnabled = false;
                SaveButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                EditButton.IsEnabled = true;
            }
        }
    }
}