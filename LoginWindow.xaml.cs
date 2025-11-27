using System;
using System.Windows;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Services;

namespace SistemaSeguimientoTesis.Views
{
    
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void IniciarSesion_Click(object sender, RoutedEventArgs e)
        {
            string correo = txtCorreo.Text.Trim();
            string password = txtPassword.Password?.Trim();

            if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Ingresa correo y contraseña.",
                                "Aviso",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();

                // buscar por email y validar contraseña en código
                string query = @"
                    SELECT UsuarioID, Rol, Contraseña, Activo
                    FROM Usuarios
                    WHERE Email = @email
                    LIMIT 1;";

                using var cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@email", correo);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    MessageBox.Show("Usuario o contraseña incorrectos.",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                bool activo = reader.GetBoolean("Activo");
                if (!activo)
                {
                    MessageBox.Show("Este usuario está desactivado. Contacta al administrador.",
                                    "Acceso denegado",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                    return;
                }

                int usuarioId = reader.GetInt32("UsuarioID");
                string rolUsuario = reader.GetString("Rol");
                string passwordBD = reader.GetString("Contraseña");

                if (!VerificarPassword(password, passwordBD))
                {
                    MessageBox.Show("Usuario o contraseña incorrectos.",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                // Abrir panel según rol
                Window panel = rolUsuario switch
                {
                    "Admin" => new PanelAdmin(),
                    "Profesor" => new PanelProfesor(usuarioId),
                    "Alumno" => new PanelAlumno(usuarioId),
                    _ => null
                };

                if (panel == null)
                {
                    MessageBox.Show("Rol no reconocido. Contacta al administrador.",
                                    "Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    return;
                }

                panel.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al iniciar sesión: " + ex.Message,
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
        private void Cerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

       
        private bool VerificarPassword(string inputPassword, string passwordBD)
        {
            // --- Compatibilidad actual (texto plano) ---
            if (inputPassword == passwordBD) return true;

         

            return false;
        }
    }
}

