using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Services;

namespace SistemaSeguimientoTesis.Views
{
    public partial class VistaPreviaWindow : Window
    {
        private readonly int entregaId;
        private byte[] archivoBytes;
        private string nombreArchivo;

        public VistaPreviaWindow(int entregaId)
        {
            InitializeComponent();
            this.entregaId = entregaId;

            CargarEntrega();
        }

        private void CargarEntrega()
        {
            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    string query = @"
                        SELECT ArchivoEntrega, ArchivoContenido, MimeType, TamañoBytes
                        FROM Entregas
                        WHERE EntregaID = @id
                        LIMIT 1;";

                    using (var cmd = new MySqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@id", entregaId);

                        using (var r = cmd.ExecuteReader())
                        {
                            if (!r.Read())
                            {
                                MessageBox.Show("No se encontró la entrega.");
                                Close();
                                return;
                            }

                            nombreArchivo = r.GetString("ArchivoEntrega");
                            archivoBytes = (byte[])r["ArchivoContenido"];
                            long tam = r.IsDBNull(r.GetOrdinal("TamañoBytes"))
                                       ? archivoBytes.Length
                                       : r.GetInt64("TamañoBytes");
                            string mime = r.IsDBNull(r.GetOrdinal("MimeType"))
                                          ? ""
                                          : r.GetString("MimeType");

                            txtNombreArchivo.Text = nombreArchivo;
                            txtInfoArchivo.Text =
                                $"Tamaño: {(tam / 1024.0):0.0} KB    MIME: {mime}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar la entrega: " + ex.Message);
                Close();
            }
        }

        private void BtnGuardarComo_Click(object sender, RoutedEventArgs e)
        {
            if (archivoBytes == null || string.IsNullOrEmpty(nombreArchivo))
            {
                MessageBox.Show("No hay archivo para guardar.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = nombreArchivo,
                Filter = "PDF (*.pdf)|*.pdf|Word (*.docx)|*.docx|Todos (*.*)|*.*",
                DefaultExt = Path.GetExtension(nombreArchivo)
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(dialog.FileName, archivoBytes);
                    MessageBox.Show("Archivo guardado correctamente.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error al guardar archivo: " + ex.Message);
                }
            }
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}



