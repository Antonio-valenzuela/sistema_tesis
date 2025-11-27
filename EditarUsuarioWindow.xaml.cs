using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Models;
using SistemaSeguimientoTesis.Services;

namespace SistemaSeguimientoTesis.Views
{
    public partial class EditarUsuarioWindow : Window
    {
        private readonly int usuarioId;
        private readonly string emailOriginal;
        private readonly string rolOriginal;

        public EditarUsuarioWindow(Usuario usuario)
        {
            InitializeComponent();

            usuarioId = usuario.UsuarioID;
            emailOriginal = usuario.Email;
            rolOriginal = usuario.Rol;

            txtNombre.Text = usuario.Nombre;
            txtEmail.Text = usuario.Email;
            txtPassword.Password = ""; // vacío = no cambiar password

            // C# 7.3: switch clásico, no switch expression
            cbRol.SelectedIndex = -1;
            switch (usuario.Rol)
            {
                case "Admin":
                    cbRol.SelectedIndex = 0;
                    break;
                case "Profesor":
                    cbRol.SelectedIndex = 1;
                    break;
                case "Alumno":
                    cbRol.SelectedIndex = 2;
                    break;
                default:
                    cbRol.SelectedIndex = -1;
                    break;
            }
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            string nombre = txtNombre.Text.Trim();
            string email = txtEmail.Text.Trim();
            string password = txtPassword.Password == null ? "" : txtPassword.Password.Trim();
            string rol = (cbRol.SelectedItem as ComboBoxItem)?.Content?.ToString();

            if (string.IsNullOrWhiteSpace(nombre) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(rol))
            {
                MessageBox.Show("Nombre, Email y Rol son obligatorios.",
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
                    // 1) Validar que usuario siga activo
                    if (!UsuarioActivo(conexion, usuarioId))
                    {
                        MessageBox.Show("Este usuario está desactivado. No se puede editar.",
                                        "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 2) Validar duplicados si cambió el email
                    if (!email.Equals(emailOriginal, StringComparison.OrdinalIgnoreCase) &&
                        EmailYaExiste(conexion, email, usuarioId))
                    {
                        MessageBox.Show("El email ya está registrado en otro usuario activo.",
                                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 3) Armar query según si cambia password
                    bool cambiaPassword = !string.IsNullOrWhiteSpace(password);

                    if (cambiaPassword)
                    {
                        string passwordFinal = HashPasswordSiBCrypt(password);

                        string query = @"
                            UPDATE Usuarios
                            SET Nombre = @nombre,
                                Email = @email,
                                Contraseña = @password,
                                Rol = @rol
                            WHERE UsuarioID = @id;";

                        using (var cmd = new MySqlCommand(query, conexion))
                        {
                            cmd.Parameters.AddWithValue("@nombre", nombre);
                            cmd.Parameters.AddWithValue("@email", email);
                            cmd.Parameters.AddWithValue("@password", passwordFinal);
                            cmd.Parameters.AddWithValue("@rol", rol);
                            cmd.Parameters.AddWithValue("@id", usuarioId);

                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string query = @"
                            UPDATE Usuarios
                            SET Nombre = @nombre,
                                Email = @email,
                                Rol = @rol
                            WHERE UsuarioID = @id;";

                        using (var cmd = new MySqlCommand(query, conexion))
                        {
                            cmd.Parameters.AddWithValue("@nombre", nombre);
                            cmd.Parameters.AddWithValue("@email", email);
                            cmd.Parameters.AddWithValue("@rol", rol);
                            cmd.Parameters.AddWithValue("@id", usuarioId);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                MessageBox.Show("Usuario actualizado correctamente.",
                                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al actualizar usuario: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ---------------------------------------------------------
        // MÉTODOS AUXILIARES
        // ---------------------------------------------------------

        private bool EsEmailValido(string email)
        {
            // Regex simple para validar email
            string patron = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, patron);
        }

        private bool UsuarioActivo(MySqlConnection conexion, int id)
        {
            string query = "SELECT COUNT(*) FROM Usuarios WHERE UsuarioID = @id";
            using (var cmd = new MySqlCommand(query, conexion))
            {
                cmd.Parameters.AddWithValue("@id", id);
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
        }

        private bool EmailYaExiste(MySqlConnection conexion, string email, int idExcluir)
        {
            string query = "SELECT COUNT(*) FROM Usuarios WHERE Email = @email AND UsuarioID != @id";
            using (var cmd = new MySqlCommand(query, conexion))
            {
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@id", idExcluir);
                int count = Convert.ToInt32(cmd.ExecuteScalar());
                return count > 0;
            }
        }

        private string HashPasswordSiBCrypt(string password)
        {
            // TODO: Aquí deberías implementar tu lógica de encriptación real (ej. BCrypt.Net)
            // Por ahora retorna la contraseña tal cual para que compile.
            return password;
        }
    }
}

