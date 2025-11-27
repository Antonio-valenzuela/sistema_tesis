using System;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Services;

namespace SistemaSeguimientoTesis.Views
{
    public partial class SubirEntregaWindow : Window
    {
        private readonly int proyectoId;
        private readonly int alumnoId;
        private string archivoSeleccionado;

        public SubirEntregaWindow(int proyectoId, int alumnoId)
        {
            InitializeComponent();
            this.proyectoId = proyectoId;
            this.alumnoId = alumnoId;
        }

        private void BtnSeleccionarArchivo_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Documentos (*.pdf;*.docx)|*.pdf;*.docx|Todos (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                archivoSeleccionado = openFileDialog.FileName;
                txtArchivoSeleccionado.Text = Path.GetFileName(archivoSeleccionado);

                var info = new FileInfo(archivoSeleccionado);
                txtInfoArchivo.Text =
                    string.Format("Tamaño: {0:0.0} KB  |  Ext: {1}",
                                  info.Length / 1024.0,
                                  info.Extension.ToLower());
            }
        }

        private void BtnSubir_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(archivoSeleccionado) || comboTipo.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un archivo y un tipo de entrega.");
                return;
            }

            try
            {
                byte[] archivoBytes = File.ReadAllBytes(archivoSeleccionado);
                string nombreArchivo = Path.GetFileName(archivoSeleccionado);
                string extension = Path.GetExtension(nombreArchivo).ToLowerInvariant();
                string tipoEntrega = ((ComboBoxItem)comboTipo.SelectedItem).Content.ToString();

                long tamanoBytes = archivoBytes.Length;
                string mimeType = ObtenerMimeType(extension);
                string hashSha256 = CalcularSHA256(archivoBytes);

                using (var conexion = ConexionBD.ObtenerConexion())
                using (var tx = conexion.BeginTransaction())
                {
                    // 1) calcular versión
                    int numeroVersion = ObtenerSiguienteVersion(conexion, tx, proyectoId, tipoEntrega);

                    // 2) insertar en Entregas
                    string insertEntrega = @"
                        INSERT INTO Entregas
                            (ProyectoID, AlumnoID, FechaEntrega, ArchivoEntrega,
                             ArchivoContenido, Tipo, NumeroVersion,
                             MimeType, TamañoBytes, HashSHA256)
                        VALUES
                            (@proyectoId, @alumnoId, NOW(), @archivoNombre,
                             @archivoContenido, @tipo, @version,
                             @mime, @tam, @hash);";

                    using (var cmd = new MySqlCommand(insertEntrega, conexion, tx))
                    {
                        cmd.Parameters.AddWithValue("@proyectoId", proyectoId);
                        cmd.Parameters.AddWithValue("@alumnoId", alumnoId);
                        cmd.Parameters.AddWithValue("@archivoNombre", nombreArchivo);
                        cmd.Parameters.Add("@archivoContenido", MySqlDbType.LongBlob).Value = archivoBytes;
                        cmd.Parameters.AddWithValue("@tipo", tipoEntrega);
                        cmd.Parameters.AddWithValue("@version", numeroVersion);
                        cmd.Parameters.AddWithValue("@mime", mimeType);
                        cmd.Parameters.AddWithValue("@tam", tamanoBytes);
                        cmd.Parameters.AddWithValue("@hash", hashSha256);

                        cmd.ExecuteNonQuery();
                    }

                    int entregaId = (int)(new MySqlCommand("SELECT LAST_INSERT_ID();", conexion, tx)
                        .ExecuteScalar());

                    // 3) insertar en EntregaVersiones
                    string insertVersion = @"
                        INSERT INTO EntregaVersiones
                            (EntregaID, NumeroVersion, ArchivoEntrega,
                             ArchivoContenido, MimeType, TamañoBytes, HashSHA256)
                        VALUES
                            (@entregaId, @version, @archivoNombre,
                             @archivoContenido, @mime, @tam, @hash);";

                    using (var cmdV = new MySqlCommand(insertVersion, conexion, tx))
                    {
                        cmdV.Parameters.AddWithValue("@entregaId", entregaId);
                        cmdV.Parameters.AddWithValue("@version", numeroVersion);
                        cmdV.Parameters.AddWithValue("@archivoNombre", nombreArchivo);
                        cmdV.Parameters.Add("@archivoContenido", MySqlDbType.LongBlob).Value = archivoBytes;
                        cmdV.Parameters.AddWithValue("@mime", mimeType);
                        cmdV.Parameters.AddWithValue("@tam", tamanoBytes);
                        cmdV.Parameters.AddWithValue("@hash", hashSha256);
                        cmdV.ExecuteNonQuery();
                    }

                    // 4) notificar al profesor
                    int? profesorId = ObtenerProfesorProyecto(conexion, tx, proyectoId);
                    if (profesorId.HasValue)
                    {
                        string insertNotif = @"
                            INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                            VALUES (@uid, @titulo, @mensaje, 'Entrega', @refId);";

                        using (var cmdN = new MySqlCommand(insertNotif, conexion, tx))
                        {
                            cmdN.Parameters.AddWithValue("@uid", profesorId.Value);
                            cmdN.Parameters.AddWithValue("@titulo", "Nueva entrega recibida");
                            cmdN.Parameters.AddWithValue("@mensaje",
                                string.Format("El alumno subió: {0} ({1}) v{2}",
                                              nombreArchivo, tipoEntrega, numeroVersion));
                            cmdN.Parameters.AddWithValue("@refId", entregaId);
                            cmdN.ExecuteNonQuery();
                        }
                    }

                    tx.Commit();

                    // 5) copia local opcional
                    if (chkGuardarCopiaLocal.IsChecked == true)
                    {
                        GuardarCopiaLocal(nombreArchivo);
                    }
                }

                MessageBox.Show("Entrega subida correctamente.");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al subir la entrega: " + ex.Message);
            }
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // ================== helpers ==================

        private static string ObtenerMimeType(string ext)
        {
            switch (ext)
            {
                case ".pdf":
                    return "application/pdf";
                case ".docx":
                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                default:
                    return "application/octet-stream";
            }
        }

        private static string CalcularSHA256(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static int ObtenerSiguienteVersion(MySqlConnection con, MySqlTransaction tx, int proyectoId, string tipo)
        {
            string q = @"
                SELECT IFNULL(MAX(NumeroVersion), 0) + 1
                FROM Entregas
                WHERE ProyectoID=@pid AND Tipo=@tipo;";

            using (var cmd = new MySqlCommand(q, con, tx))
            {
                cmd.Parameters.AddWithValue("@pid", proyectoId);
                cmd.Parameters.AddWithValue("@tipo", tipo);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        private static int? ObtenerProfesorProyecto(MySqlConnection con, MySqlTransaction tx, int proyectoId)
        {
            string q = "SELECT ProfesorID FROM Proyectos WHERE ProyectoID=@pid LIMIT 1;";
            using (var cmd = new MySqlCommand(q, con, tx))
            {
                cmd.Parameters.AddWithValue("@pid", proyectoId);
                object r = cmd.ExecuteScalar();
                if (r == null || r == DBNull.Value) return null;
                return Convert.ToInt32(r);
            }
        }

        private void GuardarCopiaLocal(string nombreArchivo)
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string carpeta = Path.Combine(docs, "EntregasTesis");
                Directory.CreateDirectory(carpeta);

                string destino = Path.Combine(carpeta, nombreArchivo);
                File.Copy(archivoSeleccionado, destino, true);
            }
            catch
            {
                // es opcional, no rompemos el flujo
            }
        }
    }
}



