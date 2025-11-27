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
        private string nombreArchivo;
        private byte[] contenidoArchivo;

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
                            contenidoArchivo = (byte[])r["ArchivoContenido"];

                            long tam = r.IsDBNull(r.GetOrdinal("TamañoBytes"))
                                ? contenidoArchivo.Length
                                : r.GetInt64("TamañoBytes");

                            string mime = r.IsDBNull(r.GetOrdinal("MimeType"))
                                ? "desconocido"
                                : r.GetString("MimeType");

                            txtNombreArchivo.Text = nombreArchivo;
                            txtInfoArchivo.Text =
                                string.Format("Tamaño: {0:0.0} KB  |  Tipo: {1}",
                                              tam / 1024.0, mime);
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
            if (contenidoArchivo == null || string.IsNullOrEmpty(nombreArchivo))
            {
                MessageBox.Show("No hay archivo para guardar.");
                return;
            }

            var dlg = new SaveFileDialog
            {
                FileName = nombreArchivo,
                Title = "Guardar copia de la entrega",
                Filter = "Todos los archivos (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllBytes(dlg.FileName, contenidoArchivo);
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

