using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Services;

namespace SistemaSeguimientoTesis.Views
{
    public partial class PanelProfesor : Window
    {
        private readonly int profesorID;

        private int? entregaIdSeleccionada = null;
        private int? alumnoIdEntregaSeleccionada = null;

        public PanelProfesor(int ProfesorID)
        {
            InitializeComponent();
            profesorID = ProfesorID;

            try
            {
                CargarProyectosAsignados();
                CargarCitasProfesor();
                CargarDisponibilidad();
                CargarEntregas();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar datos del profesor: " + ex.Message,
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        #region Carga de datos

        private void CargarProyectosAsignados()
        {
            var proyectos = new List<ProyectoVM>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT p.ProyectoID,
                           p.Titulo,
                           u.Nombre AS NombreAlumno,
                           p.Progreso
                    FROM Proyectos p
                    JOIN Usuarios u ON p.AlumnoID = u.UsuarioID
                    WHERE p.ProfesorID = @ProfesorID
                      AND p.Activo = 1
                      AND u.Activo = 1;";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@ProfesorID", profesorID);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        proyectos.Add(new ProyectoVM
                        {
                            ProyectoID = reader.GetInt32("ProyectoID"),
                            Titulo = reader.GetString("Titulo"),
                            NombreAlumno = reader.GetString("NombreAlumno"),
                            Progreso = reader.GetInt32("Progreso")
                        });
                    }
                }
            }

            dgProyectosAsignados.ItemsSource = proyectos;
        }

        private void CargarCitasProfesor()
        {
            var citas = new List<CitaVM>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT c.CitaID,
                           c.FechaCita,
                           c.Descripcion,
                           c.Estado,
                           c.LinkVirtual,
                           u.Nombre AS NombreAlumno
                    FROM Citas c
                    JOIN Usuarios u ON c.AlumnoID = u.UsuarioID
                    WHERE c.ProfesorID = @ProfesorID
                    ORDER BY c.FechaCita;";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@ProfesorID", profesorID);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        citas.Add(new CitaVM
                        {
                            CitaID = reader.GetInt32("CitaID"),
                            FechaCita = reader.GetDateTime("FechaCita"),
                            FechaTexto = reader.GetDateTime("FechaCita").ToString("dd/MM/yyyy HH:mm"),
                            Descripcion = reader.GetString("Descripcion"),
                            Estado = reader.GetString("Estado"),
                            LinkVirtual = reader["LinkVirtual"] == DBNull.Value ? "" : reader.GetString("LinkVirtual"),
                            NombreAlumno = reader.GetString("NombreAlumno")
                        });
                    }
                }
            }

            dgCitas.ItemsSource = citas;
        }

        private void CargarDisponibilidad()
        {
            var disponibilidad = new List<DisponibilidadVM>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT DisponibilidadID, Fecha, HoraInicio, HoraFin, Comentario
                    FROM DisponibilidadProfesor
                    WHERE ProfesorID = @ProfesorID
                      AND Activo = 1
                    ORDER BY Fecha, HoraInicio;";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@ProfesorID", profesorID);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        disponibilidad.Add(new DisponibilidadVM
                        {
                            DisponibilidadID = reader.GetInt32("DisponibilidadID"),
                            FechaTexto = reader.GetDateTime("Fecha").ToString("dd/MM/yyyy"),
                            HoraInicio = reader.GetTimeSpan("HoraInicio").ToString(@"hh\:mm"),
                            HoraFin = reader.GetTimeSpan("HoraFin").ToString(@"hh\:mm"),
                            Comentario = reader["Comentario"] == DBNull.Value ? "" : reader.GetString("Comentario")
                        });
                    }
                }
            }

            dgDisponibilidad.ItemsSource = disponibilidad;
        }

        private void CargarEntregas()
        {
            var entregas = new List<EntregaVM>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT e.EntregaID,
                           e.ProyectoID,
                           e.AlumnoID,
                           e.FechaEntrega,
                           e.Tipo,
                           e.ArchivoEntrega,
                           e.Comentarios,
                           e.Calificacion,
                           u.Nombre AS NombreAlumno,
                           p.Titulo AS TituloProyecto
                    FROM Entregas e
                    JOIN Proyectos p ON e.ProyectoID = p.ProyectoID
                    JOIN Usuarios u ON e.AlumnoID = u.UsuarioID
                    WHERE p.ProfesorID = @ProfesorID
                      AND p.Activo = 1
                    ORDER BY e.FechaEntrega DESC;";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@ProfesorID", profesorID);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        entregas.Add(new EntregaVM
                        {
                            EntregaID = reader.GetInt32("EntregaID"),
                            ProyectoID = reader.GetInt32("ProyectoID"),
                            AlumnoID = reader.GetInt32("AlumnoID"),
                            FechaEntregaTexto = reader.GetDateTime("FechaEntrega").ToString("dd/MM/yyyy HH:mm"),
                            Tipo = reader.GetString("Tipo"),
                            ArchivoEntrega = reader.GetString("ArchivoEntrega"),
                            Comentarios = reader.IsDBNull(reader.GetOrdinal("Comentarios"))
                                          ? ""
                                          : reader.GetString("Comentarios"),
                            Calificacion = reader.IsDBNull(reader.GetOrdinal("Calificacion"))
                                          ? (int?)null
                                          : reader.GetInt32("Calificacion"),
                            NombreAlumno = reader.GetString("NombreAlumno"),
                            TituloProyecto = reader.GetString("TituloProyecto")
                        });
                    }
                }
            }

            dgEntregas.ItemsSource = entregas;
        }

        #endregion

        #region Entregas: selección + preview/descarga + evaluación + comentarios

        private void DgEntregas_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgEntregas.SelectedItem is not EntregaVM sel)
                return;

            entregaIdSeleccionada = sel.EntregaID;
            alumnoIdEntregaSeleccionada = sel.AlumnoID;

            txtComentarioProfesor.Text = sel.Comentarios ?? "";
            if (sel.Calificacion != null)
            {
                var item = cbCalificacion.Items
                    .OfType<ComboBoxItem>()
                    .FirstOrDefault(i => i.Content.ToString() == sel.Calificacion.ToString());
                if (item != null) cbCalificacion.SelectedItem = item;
            }
            else cbCalificacion.SelectedIndex = -1;

            CargarComentariosEntrega(sel.EntregaID);
        }

        private void BtnVistaPreviaEntrega_Click(object sender, RoutedEventArgs e)
        {
            if (entregaIdSeleccionada == null)
            {
                MessageBox.Show("Selecciona una entrega primero.");
                return;
            }

            try
            {
                var preview = new VistaPreviaWindow(entregaIdSeleccionada.Value);
                preview.Owner = this;
                preview.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo abrir la vista previa: " + ex.Message);
            }
        }

        // MÉTODO EXTRA para coincidir con el XAML (BtnVerEntrega_Click)
        private void BtnVerEntrega_Click(object sender, RoutedEventArgs e)
        {
            // Reutilizamos la misma lógica de vista previa
            BtnVistaPreviaEntrega_Click(sender, e);
        }

        private void BtnDescargarEntrega_Click(object sender, RoutedEventArgs e)
        {
            if (entregaIdSeleccionada == null)
            {
                MessageBox.Show("Selecciona una entrega primero.");
                return;
            }

            try
            {
                string nombreArchivo;
                byte[] contenido;

                using var conexion = ConexionBD.ObtenerConexion();
                string query = @"
                    SELECT ArchivoEntrega, ArchivoContenido
                    FROM Entregas
                    WHERE EntregaID=@id
                    LIMIT 1;";

                using var cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@id", entregaIdSeleccionada.Value);

                using var r = cmd.ExecuteReader();
                if (!r.Read())
                {
                    MessageBox.Show("No se encontró la entrega.");
                    return;
                }

                nombreArchivo = r.GetString("ArchivoEntrega");
                contenido = (byte[])r["ArchivoContenido"];

                SaveFileDialog dialog = new SaveFileDialog
                {
                    FileName = nombreArchivo,
                    Title = "Guardar entrega",
                    Filter = "PDF (*.pdf)|*.pdf|Word (*.docx)|*.docx|Todos (*.*)|*.*",
                    DefaultExt = Path.GetExtension(nombreArchivo)
                };

                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(dialog.FileName, contenido);
                    MessageBox.Show("Archivo guardado correctamente.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al descargar: " + ex.Message);
            }
        }

        private void BtnGuardarEvaluacion_Click(object sender, RoutedEventArgs e)
        {
            if (entregaIdSeleccionada == null || alumnoIdEntregaSeleccionada == null)
            {
                MessageBox.Show("Selecciona una entrega.");
                return;
            }

            if (cbCalificacion.SelectedItem is not ComboBoxItem item ||
                !int.TryParse(item.Content.ToString(), out int calificacion))
            {
                MessageBox.Show("Selecciona una calificación válida.");
                return;
            }

            string comentario = txtComentarioProfesor.Text.Trim();

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                using var tx = conexion.BeginTransaction();

                string query = @"
                    UPDATE Entregas
                    SET Calificacion = @Calificacion,
                        Comentarios = @Comentarios
                    WHERE EntregaID = @EntregaID;";

                using var cmd = new MySqlCommand(query, conexion, tx);
                cmd.Parameters.AddWithValue("@Calificacion", calificacion);
                cmd.Parameters.AddWithValue("@Comentarios", comentario);
                cmd.Parameters.AddWithValue("@EntregaID", entregaIdSeleccionada.Value);
                cmd.ExecuteNonQuery();

                string notif = @"
                    INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                    VALUES (@uid, 'Entrega calificada',
                            CONCAT('Tu entrega fue evaluada con ', @cal, ' puntos.'),
                            'Entrega', @refId);";

                using var cmdN = new MySqlCommand(notif, conexion, tx);
                cmdN.Parameters.AddWithValue("@uid", alumnoIdEntregaSeleccionada.Value);
                cmdN.Parameters.AddWithValue("@cal", calificacion);
                cmdN.Parameters.AddWithValue("@refId", entregaIdSeleccionada.Value);
                cmdN.ExecuteNonQuery();

                tx.Commit();

                MessageBox.Show("Evaluación guardada.");
                CargarEntregas();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al guardar evaluación: " + ex.Message);
            }
        }

        private void CargarComentariosEntrega(int entregaId)
        {
            try
            {
                var comentarios = new List<ComentarioVM>();

                using var conexion = ConexionBD.ObtenerConexion();
                string query = @"
                    SELECT c.ComentarioID, c.Texto, c.Fecha, c.Resuelto,
                           u.Nombre AS Autor
                    FROM ComentariosEntrega c
                    JOIN Usuarios u ON c.AutorID = u.UsuarioID
                    WHERE c.EntregaID = @entregaId
                    ORDER BY c.Fecha ASC;";

                using var cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@entregaId", entregaId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    comentarios.Add(new ComentarioVM
                    {
                        ComentarioID = r.GetInt32("ComentarioID"),
                        Autor = r.GetString("Autor"),
                        Texto = r.GetString("Texto"),
                        FechaTexto = r.GetDateTime("Fecha").ToString("dd/MM/yyyy HH:mm"),
                        Resuelto = r.GetBoolean("Resuelto")
                    });
                }

                lbComentarios.ItemsSource = comentarios;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar comentarios: " + ex.Message);
            }
        }

        private void BtnEnviarComentario_Click(object sender, RoutedEventArgs e)
        {
            if (entregaIdSeleccionada == null || alumnoIdEntregaSeleccionada == null)
            {
                MessageBox.Show("Selecciona una entrega primero.");
                return;
            }

            string texto = txtNuevoComentario.Text.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                MessageBox.Show("Escribe un comentario.");
                return;
            }

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                using var tx = conexion.BeginTransaction();

                string insert = @"
                    INSERT INTO ComentariosEntrega (EntregaID, AutorID, Texto)
                    VALUES (@entregaId, @autorId, @texto);";

                using var cmd = new MySqlCommand(insert, conexion, tx);
                cmd.Parameters.AddWithValue("@entregaId", entregaIdSeleccionada.Value);
                cmd.Parameters.AddWithValue("@autorId", profesorID);
                cmd.Parameters.AddWithValue("@texto", texto);
                cmd.ExecuteNonQuery();

                string notif = @"
                    INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                    VALUES (@uid, 'Nuevo comentario del profesor',
                            'Tu profesor dejó feedback en tu entrega.',
                            'Comentario', @refId);";

                using var cmdN = new MySqlCommand(notif, conexion, tx);
                cmdN.Parameters.AddWithValue("@uid", alumnoIdEntregaSeleccionada.Value);
                cmdN.Parameters.AddWithValue("@refId", entregaIdSeleccionada.Value);
                cmdN.ExecuteNonQuery();

                tx.Commit();

                txtNuevoComentario.Clear();
                CargarComentariosEntrega(entregaIdSeleccionada.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al enviar comentario: " + ex.Message);
            }
        }

        #endregion

        #region Disponibilidad

        private void BtnAgregarDisponibilidad_Click(object sender, RoutedEventArgs e)
        {
            if (dpFechaDisponible.SelectedDate == null ||
                string.IsNullOrWhiteSpace(txtHoraInicio.Text) ||
                string.IsNullOrWhiteSpace(txtHoraFin.Text))
            {
                MessageBox.Show("Completa fecha y horas.");
                return;
            }

            DateTime fecha = dpFechaDisponible.SelectedDate.Value;
            string horaInicio = txtHoraInicio.Text.Trim();
            string horaFin = txtHoraFin.Text.Trim();
            string comentario = txtComentarioDisponibilidad.Text.Trim();

            if (!TimeSpan.TryParse(horaInicio, out TimeSpan hi) ||
                !TimeSpan.TryParse(horaFin, out TimeSpan hf))
            {
                MessageBox.Show("Formato de hora incorrecto. Usa HH:mm.");
                return;
            }

            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    string query = @"
                        INSERT INTO DisponibilidadProfesor
                            (ProfesorID, Fecha, HoraInicio, HoraFin, Comentario, Activo)
                        VALUES
                            (@ProfesorID, @Fecha, @HoraInicio, @HoraFin, @Comentario, 1);";

                    MySqlCommand cmd = new MySqlCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@ProfesorID", profesorID);
                    cmd.Parameters.AddWithValue("@Fecha", fecha.Date);
                    cmd.Parameters.AddWithValue("@HoraInicio", hi);
                    cmd.Parameters.AddWithValue("@HoraFin", hf);
                    cmd.Parameters.AddWithValue("@Comentario", string.IsNullOrEmpty(comentario) ? (object)DBNull.Value : comentario);

                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Disponibilidad agregada.");
                dpFechaDisponible.SelectedDate = null;
                txtHoraInicio.Clear();
                txtHoraFin.Clear();
                txtComentarioDisponibilidad.Clear();

                CargarDisponibilidad();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar disponibilidad: " + ex.Message);
            }
        }

        private void BtnEliminarDisponibilidad_Click(object sender, RoutedEventArgs e)
        {
            if (dgDisponibilidad.SelectedItem is not DisponibilidadVM seleccionada)
            {
                MessageBox.Show("Selecciona una disponibilidad.");
                return;
            }

            if (MessageBox.Show("¿Desactivar esta disponibilidad?",
                                "Confirmación",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    string query = "UPDATE DisponibilidadProfesor SET Activo=0 WHERE DisponibilidadID=@ID;";
                    MySqlCommand cmd = new MySqlCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@ID", seleccionada.DisponibilidadID);
                    cmd.ExecuteNonQuery();
                }

                CargarDisponibilidad();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al desactivar disponibilidad: " + ex.Message);
            }
        }

        #endregion

        #region Citas

        private void BtnActualizarCitas_Click(object sender, RoutedEventArgs e)
        {
            CargarCitasProfesor();
        }

        private void BtnConfirmarCita_Click(object sender, RoutedEventArgs e)
        {
            if (dgCitas.SelectedItem is not CitaVM cita)
            {
                MessageBox.Show("Selecciona una cita.");
                return;
            }

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                string q = "UPDATE Citas SET Estado='confirmada' WHERE CitaID=@id;";
                using var cmd = new MySqlCommand(q, conexion);
                cmd.Parameters.AddWithValue("@id", cita.CitaID);
                cmd.ExecuteNonQuery();

                CargarCitasProfesor();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al confirmar: " + ex.Message);
            }
        }

        private void BtnCancelarCita_Click(object sender, RoutedEventArgs e)
        {
            if (dgCitas.SelectedItem is not CitaVM cita)
            {
                MessageBox.Show("Selecciona una cita.");
                return;
            }

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                string q = "UPDATE Citas SET Estado='cancelada' WHERE CitaID=@id;";
                using var cmd = new MySqlCommand(q, conexion);
                cmd.Parameters.AddWithValue("@id", cita.CitaID);
                cmd.ExecuteNonQuery();

                CargarCitasProfesor();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cancelar: " + ex.Message);
            }
        }

        #endregion

        // ================== ViewModels internos ==================

        private class ProyectoVM
        {
            public int ProyectoID { get; set; }
            public string Titulo { get; set; }
            public string NombreAlumno { get; set; }
            public int Progreso { get; set; }
        }

        private class EntregaVM
        {
            public int EntregaID { get; set; }
            public int ProyectoID { get; set; }
            public int AlumnoID { get; set; }
            public string FechaEntregaTexto { get; set; }
            public string Tipo { get; set; }
            public string ArchivoEntrega { get; set; }
            public string Comentarios { get; set; }
            public int? Calificacion { get; set; }
            public string NombreAlumno { get; set; }
            public string TituloProyecto { get; set; }
        }

        private class ComentarioVM
        {
            public int ComentarioID { get; set; }
            public string Autor { get; set; }
            public string FechaTexto { get; set; }
            public string Texto { get; set; }
            public bool Resuelto { get; set; }
        }

        private class CitaVM
        {
            public int CitaID { get; set; }
            public DateTime FechaCita { get; set; }
            public string FechaTexto { get; set; }
            public string Descripcion { get; set; }
            public string Estado { get; set; }
            public string LinkVirtual { get; set; }
            public string NombreAlumno { get; set; }
        }

        private class DisponibilidadVM
        {
            public int DisponibilidadID { get; set; }
            public string FechaTexto { get; set; }
            public string HoraInicio { get; set; }
            public string HoraFin { get; set; }
            public string Comentario { get; set; }
        }

    }
}


