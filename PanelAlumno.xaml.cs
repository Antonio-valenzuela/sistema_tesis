using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Services;

namespace SistemaSeguimientoTesis.Views
{
    public partial class PanelAlumno : Window, INotifyPropertyChanged
    {
        private readonly int alumnoId;

        // Proyecto actual en memoria
        private int? proyectoIdActual = null;
        private int? profesorIdActual = null;

        // Selecciones actuales
        private int? entregaIdSeleccionada = null;
        private int? comentarioIdSeleccionado = null;
        private int? disponibilidadIdSeleccionada = null;
        private int? citaIdSeleccionada = null;

        // --- propiedades para mostrar/ocultar panel de proyecto ---
        private Visibility panelSinProyectoVisibility;
        public Visibility PanelSinProyectoVisibility
        {
            get => panelSinProyectoVisibility;
            set { panelSinProyectoVisibility = value; OnPropertyChanged(nameof(PanelSinProyectoVisibility)); }
        }

        private Visibility panelConProyectoVisibility;
        public Visibility PanelConProyectoVisibility
        {
            get => panelConProyectoVisibility;
            set { panelConProyectoVisibility = value; OnPropertyChanged(nameof(PanelConProyectoVisibility)); }
        }

        public PanelAlumno(int alumnoId)
        {
            InitializeComponent();
            this.alumnoId = alumnoId;
            DataContext = this;

            ActualizarVisibilidadProyecto();
            CargarSeguimiento();
            CargarNotificaciones();
            CargarAgenda(); // disponibilidad + mis citas
        }

        // ================== PROYECTO ==================

        private bool VerificarProyectoRegistrado()
        {
            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    string query = "SELECT COUNT(*) FROM Proyectos WHERE AlumnoID = @id AND Activo = 1";
                    using var cmd = new MySqlCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@id", alumnoId);
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al verificar proyecto: " + ex.Message);
                return false;
            }
        }

        private void ActualizarVisibilidadProyecto()
        {
            if (VerificarProyectoRegistrado())
            {
                PanelSinProyectoVisibility = Visibility.Collapsed;
                PanelConProyectoVisibility = Visibility.Visible;
            }
            else
            {
                PanelSinProyectoVisibility = Visibility.Visible;
                PanelConProyectoVisibility = Visibility.Collapsed;
            }
        }

       

        private void BtnIrASeguimiento_Click(object sender, RoutedEventArgs e)
        {
            TabControlAlumno.SelectedIndex = 1;
        }

        // ================== UTILIDADES ==================

        private int? ObtenerProyectoIdActual(MySqlConnection conexion)
        {
            string query = "SELECT ProyectoID, ProfesorID FROM Proyectos WHERE AlumnoID=@alumnoId AND Activo=1 LIMIT 1";
            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@alumnoId", alumnoId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            proyectoIdActual = reader.GetInt32("ProyectoID");
            profesorIdActual = reader["ProfesorID"] == DBNull.Value ? (int?)null : reader.GetInt32("ProfesorID");
            return proyectoIdActual;
        }

        // ================== SEGUIMIENTO Y ENTREGAS ==================

        private void CargarSeguimiento()
        {
            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    var pid = ObtenerProyectoIdActual(conexion);
                    if (pid == null)
                    {
                        listaEntregas.ItemsSource = null;
                        barraProgreso.Value = 0;
                        entregaIdSeleccionada = null;
                        lbComentarios.ItemsSource = null;
                        return;
                    }

                    int proyectoId = pid.Value;

                    // Entregas (incluye EntregaID)
                    string queryEntregas = @"
                        SELECT EntregaID, ArchivoEntrega, Tipo, FechaEntrega
                        FROM Entregas
                        WHERE ProyectoID = @proyectoId
                        ORDER BY FechaEntrega ASC;";

                    using var cmdEntregas = new MySqlCommand(queryEntregas, conexion);
                    cmdEntregas.Parameters.AddWithValue("@proyectoId", proyectoId);

                    var entregas = new List<EntregaVM>();
                    using (MySqlDataReader reader = cmdEntregas.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            entregas.Add(new EntregaVM
                            {
                                EntregaID = reader.GetInt32("EntregaID"),
                                Titulo = reader.GetString("ArchivoEntrega"),
                                Tipo = reader.GetString("Tipo"),
                                FechaEntrega = reader.GetDateTime("FechaEntrega").ToString("dd/MM/yyyy")
                            });
                        }
                    }

                    listaEntregas.ItemsSource = entregas;

                    // Total esperado
                    string queryPlan = "SELECT TotalEntregas FROM plan_entregas WHERE ProyectoID = @proyectoId LIMIT 1";
                    using var cmdPlan = new MySqlCommand(queryPlan, conexion);
                    cmdPlan.Parameters.AddWithValue("@proyectoId", proyectoId);
                    object totalObj = cmdPlan.ExecuteScalar();
                    int totalEsperado = (totalObj != null) ? Convert.ToInt32(totalObj) : 6;

                    int entregadas = entregas.Count;
                    double porcentaje = (entregadas * 100.0) / totalEsperado;
                    barraProgreso.Value = Math.Min(porcentaje, 100);

                    // Guardar progreso
                    string updateProgreso = "UPDATE Proyectos SET Progreso = @progreso WHERE ProyectoID = @proyectoId";
                    using var cmdUpdate = new MySqlCommand(updateProgreso, conexion);
                    cmdUpdate.Parameters.AddWithValue("@progreso", (int)porcentaje);
                    cmdUpdate.Parameters.AddWithValue("@proyectoId", proyectoId);
                    cmdUpdate.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar el seguimiento: " + ex.Message);
            }
        }

        private void BtnSubirEntrega_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    var pid = ObtenerProyectoIdActual(conexion);
                    if (pid == null)
                    {
                        MessageBox.Show("No se encontró el proyecto del alumno.");
                        return;
                    }

                    var ventana = new SubirEntregaWindow(pid.Value, alumnoId);
                    if (ventana.ShowDialog() == true)
                    {
                        CargarSeguimiento();
                        CargarNotificaciones();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al subir entrega: " + ex.Message);
            }
        }

        private void ListaEntregas_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (listaEntregas.SelectedItem is EntregaVM entrega)
            {
                entregaIdSeleccionada = entrega.EntregaID;
                CargarComentariosEntrega(entrega.EntregaID);
            }
        }

        private void ListaEntregas_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            AbrirVistaPreviaSeleccionada();
        }

        private void BtnVistaPreviaEntrega_Click(object sender, RoutedEventArgs e)
        {
            AbrirVistaPreviaSeleccionada();
        }

        private void AbrirVistaPreviaSeleccionada()
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

        private void BtnDescargarEntrega_Click(object sender, RoutedEventArgs e)
        {
            if (entregaIdSeleccionada == null)
            {
                MessageBox.Show("Selecciona una entrega primero.");
                return;
            }

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();

                string query = @"
                    SELECT ArchivoEntrega, ArchivoContenido
                    FROM Entregas
                    WHERE EntregaID=@id
                    LIMIT 1;";

                using var cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@id", entregaIdSeleccionada.Value);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    MessageBox.Show("No se encontró la entrega.");
                    return;
                }

                string nombreArchivo = reader.GetString("ArchivoEntrega");
                byte[] contenido = (byte[])reader["ArchivoContenido"];

                SaveFileDialog dialog = new SaveFileDialog
                {
                    FileName = nombreArchivo,
                    Filter = "PDF (*.pdf)|*.pdf|Word (*.docx)|*.docx|Todos (*.*)|*.*",
                    DefaultExt = Path.GetExtension(nombreArchivo)
                };

                if (dialog.ShowDialog() == true)
                {
                    File.WriteAllBytes(dialog.FileName, contenido);
                    MessageBox.Show("Entrega descargada correctamente.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al descargar: " + ex.Message);
            }
        }

        // ================== COMENTARIOS (FEEDBACK) ==================

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
            if (entregaIdSeleccionada == null)
            {
                MessageBox.Show("Selecciona una entrega para comentar.");
                return;
            }

            string texto = txtNuevoComentario.Text.Trim();
            if (string.IsNullOrEmpty(texto))
            {
                MessageBox.Show("Escribe un comentario primero.");
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
                cmd.Parameters.AddWithValue("@autorId", alumnoId);
                cmd.Parameters.AddWithValue("@texto", texto);
                cmd.ExecuteNonQuery();

                // Notificar profesor si está asignado
                if (profesorIdActual != null)
                {
                    string notif = @"
                        INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                        VALUES (@uid, 'Nuevo comentario del alumno',
                                'El alumno dejó feedback en una entrega.',
                                'Comentario', @refId);";

                    using var cmdN = new MySqlCommand(notif, conexion, tx);
                    cmdN.Parameters.AddWithValue("@uid", profesorIdActual.Value);
                    cmdN.Parameters.AddWithValue("@refId", entregaIdSeleccionada.Value);
                    cmdN.ExecuteNonQuery();
                }

                tx.Commit();

                txtNuevoComentario.Clear();
                CargarComentariosEntrega(entregaIdSeleccionada.Value);
                CargarNotificaciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al enviar comentario: " + ex.Message);
            }
        }

        private void BtnResolverComentario_Click(object sender, RoutedEventArgs e)
        {
            if (lbComentarios.SelectedItem is not ComentarioVM comentario)
            {
                MessageBox.Show("Selecciona un comentario.");
                return;
            }

            comentarioIdSeleccionado = comentario.ComentarioID;

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                string update = "UPDATE ComentariosEntrega SET Resuelto=1 WHERE ComentarioID=@id;";
                using var cmd = new MySqlCommand(update, conexion);
                cmd.Parameters.AddWithValue("@id", comentarioIdSeleccionado.Value);
                cmd.ExecuteNonQuery();

                if (entregaIdSeleccionada != null)
                    CargarComentariosEntrega(entregaIdSeleccionada.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al resolver comentario: " + ex.Message);
            }
        }

        // ================== AGENDA / CITAS ==================

        private void CargarAgenda()
        {
            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                var pid = ObtenerProyectoIdActual(conexion);

                if (pid == null || profesorIdActual == null)
                {
                    dgDisponibilidad.ItemsSource = null;
                    dgMisCitas.ItemsSource = null;
                    return;
                }

                CargarDisponibilidadProfesor(profesorIdActual.Value);
                CargarMisCitas(profesorIdActual.Value);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar agenda: " + ex.Message);
            }
        }

        private void CargarDisponibilidadProfesor(int profesorId)
        {
            var lista = new List<DisponibilidadVM>();

            string query = @"
                SELECT DisponibilidadID, Fecha, HoraInicio, HoraFin, Comentario
                FROM DisponibilidadProfesor
                WHERE ProfesorID=@profesorId
                  AND Activo=1
                  AND Fecha >= CURDATE()
                ORDER BY Fecha ASC, HoraInicio ASC;";

            using var conexion = ConexionBD.ObtenerConexion();
            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@profesorId", profesorId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new DisponibilidadVM
                {
                    DisponibilidadID = r.GetInt32("DisponibilidadID"),
                    Fecha = r.GetDateTime("Fecha"),
                    FechaTexto = r.GetDateTime("Fecha").ToString("dd/MM/yyyy"),
                    HoraInicio = r.GetTimeSpan("HoraInicio").ToString(@"hh\:mm"),
                    HoraFin = r.GetTimeSpan("HoraFin").ToString(@"hh\:mm"),
                    Comentario = r["Comentario"] == DBNull.Value ? "" : r.GetString("Comentario")
                });
            }

            dgDisponibilidad.ItemsSource = lista;
        }

        private void CargarMisCitas(int profesorId)
        {
            var lista = new List<CitaVM>();

            string query = @"
                SELECT CitaID, FechaCita, Descripcion, Estado, LinkVirtual
                FROM Citas
                WHERE AlumnoID=@alumnoId
                  AND ProfesorID=@profesorId
                ORDER BY FechaCita DESC;";

            using var conexion = ConexionBD.ObtenerConexion();
            using var cmd = new MySqlCommand(query, conexion);
            cmd.Parameters.AddWithValue("@alumnoId", alumnoId);
            cmd.Parameters.AddWithValue("@profesorId", profesorId);

            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                lista.Add(new CitaVM
                {
                    CitaID = r.GetInt32("CitaID"),
                    FechaCita = r.GetDateTime("FechaCita"),
                    FechaTexto = r.GetDateTime("FechaCita").ToString("dd/MM/yyyy HH:mm"),
                    Descripcion = r.GetString("Descripcion"),
                    Estado = r.GetString("Estado"),
                    LinkVirtual = r["LinkVirtual"] == DBNull.Value ? "" : r.GetString("LinkVirtual")
                });
            }

            dgMisCitas.ItemsSource = lista;
        }

        private void BtnActualizarAgenda_Click(object sender, RoutedEventArgs e)
        {
            CargarAgenda();
        }

        private void BtnAgendarCita_Click(object sender, RoutedEventArgs e)
        {
            if (dgDisponibilidad.SelectedItem is not DisponibilidadVM disp)
            {
                MessageBox.Show("Selecciona una disponibilidad.");
                return;
            }
            if (profesorIdActual == null)
            {
                MessageBox.Show("No tienes un profesor asignado aún.");
                return;
            }

            disponibilidadIdSeleccionada = disp.DisponibilidadID;

            if (!TimeSpan.TryParse(disp.HoraInicio, out var hi))
            {
                MessageBox.Show("Hora inválida en esta disponibilidad.");
                return;
            }

            DateTime fechaCita = disp.Fecha.Date.Add(hi);

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                using var tx = conexion.BeginTransaction();

                // Validación solape (±60 min)
                string existsQuery = @"
                    SELECT COUNT(*) 
                    FROM Citas
                    WHERE ProfesorID=@profId
                      AND Estado IN ('pendiente','confirmada')
                      AND ABS(TIMESTAMPDIFF(MINUTE, FechaCita, @fecha)) < 60;";

                using var existsCmd = new MySqlCommand(existsQuery, conexion, tx);
                existsCmd.Parameters.AddWithValue("@profId", profesorIdActual.Value);
                existsCmd.Parameters.AddWithValue("@fecha", fechaCita);
                int solapes = Convert.ToInt32(existsCmd.ExecuteScalar());

                if (solapes > 0)
                {
                    MessageBox.Show("Esa hora ya está ocupada. Elige otra disponibilidad.");
                    tx.Rollback();
                    return;
                }

                // Insertar cita
                string insert = @"
                    INSERT INTO Citas (ProfesorID, AlumnoID, FechaCita, Descripcion, Estado)
                    VALUES (@profId, @alumnoId, @fecha, @desc, 'pendiente');";

                using var cmd = new MySqlCommand(insert, conexion, tx);
                cmd.Parameters.AddWithValue("@profId", profesorIdActual.Value);
                cmd.Parameters.AddWithValue("@alumnoId", alumnoId);
                cmd.Parameters.AddWithValue("@fecha", fechaCita);
                cmd.Parameters.AddWithValue("@desc", "Revisión de tesis");
                cmd.ExecuteNonQuery();

                int citaId = (int)cmd.LastInsertedId;

                // Desactivar disponibilidad reservada
                string desactDisp = "UPDATE DisponibilidadProfesor SET Activo=0 WHERE DisponibilidadID=@dispId;";
                using var cmdD = new MySqlCommand(desactDisp, conexion, tx);
                cmdD.Parameters.AddWithValue("@dispId", disponibilidadIdSeleccionada.Value);
                cmdD.ExecuteNonQuery();

                // Notificar profesor
                string notif = @"
                    INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                    VALUES (@uid, 'Nueva cita solicitada',
                            'Un alumno solicitó una cita. Revisa tu agenda.',
                            'Cita', @refId);";

                using var cmdN = new MySqlCommand(notif, conexion, tx);
                cmdN.Parameters.AddWithValue("@uid", profesorIdActual.Value);
                cmdN.Parameters.AddWithValue("@refId", citaId);
                cmdN.ExecuteNonQuery();

                tx.Commit();

                MessageBox.Show("Cita solicitada. Queda en estado pendiente.");
                CargarAgenda();
                CargarNotificaciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agendar cita: " + ex.Message);
            }
        }

        private void BtnCancelarCita_Click(object sender, RoutedEventArgs e)
        {
            if (dgMisCitas.SelectedItem is not CitaVM cita)
            {
                MessageBox.Show("Selecciona una cita.");
                return;
            }

            citaIdSeleccionada = cita.CitaID;

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                string update = "UPDATE Citas SET Estado='cancelada' WHERE CitaID=@id;";
                using var cmd = new MySqlCommand(update, conexion);
                cmd.Parameters.AddWithValue("@id", citaIdSeleccionada.Value);
                cmd.ExecuteNonQuery();

                MessageBox.Show("Cita cancelada.");
                CargarAgenda();
                CargarNotificaciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cancelar: " + ex.Message);
            }
        }

        // ================== NOTIFICACIONES ==================

        private void CargarNotificaciones()
        {
            try
            {
                var lista = new List<NotificacionVM>();

                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    string query = @"
                        SELECT NotificacionID, UsuarioID, Titulo, Mensaje, Tipo,
                               FechaCreacion, Leida
                        FROM Notificaciones
                        WHERE UsuarioID = @usuario
                        ORDER BY FechaCreacion DESC;";

                    using var cmd = new MySqlCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@usuario", alumnoId);

                    using (MySqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            bool leida = reader.GetBoolean("Leida");
                            lista.Add(new NotificacionVM
                            {
                                NotificacionID = reader.GetInt32("NotificacionID"),
                                Titulo = reader.GetString("Titulo"),
                                Mensaje = reader.GetString("Mensaje"),
                                FechaTexto = reader.GetDateTime("FechaCreacion").ToString("dd/MM/yyyy HH:mm"),
                                Estado = leida ? "Leída" : "Pendiente"
                            });
                        }
                    }
                }

                dgNotificaciones.ItemsSource = lista;

                int pendientes = lista.Count(n => n.Estado == "Pendiente");
                txtNotificacionesPendientes.Text = pendientes.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar notificaciones: " + ex.Message);
            }
        }

        private void BtnActualizarNotificaciones_Click(object sender, RoutedEventArgs e)
        {
            CargarNotificaciones();
        }

        private void BtnMarcarLeidas_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    string query = @"
                        UPDATE Notificaciones
                        SET Leida = 1
                        WHERE UsuarioID = @usuario AND Leida = 0;";
                    using var cmd = new MySqlCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@usuario", alumnoId);
                    cmd.ExecuteNonQuery();
                }

                CargarNotificaciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al marcar como leídas: " + ex.Message);
            }
        }

        // ================== INotifyPropertyChanged ==================

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        // ================== ViewModels simples ==================

        private class EntregaVM
        {
            public int EntregaID { get; set; }
            public string Titulo { get; set; }
            public string Tipo { get; set; }
            public string FechaEntrega { get; set; }
        }

        private class ComentarioVM
        {
            public int ComentarioID { get; set; }
            public string Autor { get; set; }
            public string FechaTexto { get; set; }
            public string Texto { get; set; }
            public bool Resuelto { get; set; }
        }

        private class DisponibilidadVM
        {
            public int DisponibilidadID { get; set; }
            public DateTime Fecha { get; set; }
            public string FechaTexto { get; set; }
            public string HoraInicio { get; set; }
            public string HoraFin { get; set; }
            public string Comentario { get; set; }
        }

        private class CitaVM
        {
            public int CitaID { get; set; }
            public DateTime FechaCita { get; set; }
            public string FechaTexto { get; set; }
            public string Descripcion { get; set; }
            public string Estado { get; set; }
            public string LinkVirtual { get; set; }
        }

        private class NotificacionVM
        {
            public int NotificacionID { get; set; }
            public string FechaTexto { get; set; }
            public string Titulo { get; set; }
            public string Mensaje { get; set; }
            public string Estado { get; set; }
        }
    }
}


