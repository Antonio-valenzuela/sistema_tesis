using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Services;

namespace SistemaSeguimientoTesis.Views
{
    public partial class AgregarUsuarioWindow : Window
    {
        public AgregarUsuarioWindow()
        {
            InitializeComponent();
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string nombre = txtNombre.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password == null ? "" : txtPassword.Password.Trim();
            string rol = (cbRol.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(rol))
            {
                MessageBox.Show("Todos los campos son obligatorios.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!EsEmailValido(email))
            {
                MessageBox.Show("El email no tiene un formato válido.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    // 1) Validar duplicado (la BD tiene UNIQUE en Email)
                    if (EmailYaExiste(conexion, email))
                    {
                        MessageBox.Show("El email ya está registrado. Usa otro correo.",
                                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 2) Hash (si existe BCrypt) o plano (compatibilidad)
                    string passwordFinal = HashPasswordSiBCrypt(password);

                    // 3) Insertar usuario (Activo=1 por soft delete)
                    string query = @"
                        INSERT INTO Usuarios (Nombre, Email, Contraseña, Rol, Activo)
                        VALUES (@nombre, @email, @password, @rol, 1);";

                    using (var cmd = new MySqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@nombre", nombre);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@password", passwordFinal);
                        cmd.Parameters.AddWithValue("@rol", rol);

                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Usuario agregado correctamente.",
                                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar usuario: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Botón cancelar (lo llama el XAML)
        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ===========================
        // Validaciones auxiliares
        // ===========================

        private bool EmailYaExiste(MySqlConnection conexion, string email)
        {
            string q = "SELECT COUNT(*) FROM Usuarios WHERE Email=@email LIMIT 1;";
            using (var cmd = new MySqlCommand(q, conexion))
            {
                cmd.Parameters.AddWithValue("@email", email);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
        }

        private bool EsEmailValido(string email)
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }

        /// <summary>
        /// Si tienes BCrypt instalado (BCrypt.Net-Next), se usa hash.
        /// Si no, guarda texto plano (compatibilidad).
        /// No rompe compilación porque usa reflexión.
        /// </summary>
        private string HashPasswordSiBCrypt(string plain)
        {
            try
            {
                var t = Type.GetType("BCrypt.Net.BCrypt, BCrypt.Net-Next");
                if (t != null)
                {
                    var m = t.GetMethod("HashPassword", new[] { typeof(string) });
                    if (m != null)
                    {
                        return (string)m.Invoke(null, new object[] { plain });
                    }
                }
            }
            catch
            {
                // fallback a texto plano
            }

            return plain;
        }
    }
}

