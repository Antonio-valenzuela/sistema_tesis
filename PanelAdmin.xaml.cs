using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Services;
using SistemaSeguimientoTesis.Models;

namespace SistemaSeguimientoTesis.Views
{
    public partial class PanelAdmin : Window
    {
        public ObservableCollection<dynamic> Proyectos { get; set; }

        public PanelAdmin()
        {
            InitializeComponent();
            Proyectos = new ObservableCollection<dynamic>();

            CargarUsers();
            CargarProfesoresComboBox();
            CargarProyectos();
        }

        // =======================
        // CARGAR USUARIOS (solo activos)
        // =======================
        private void CargarUsers()
        {
            var usuarios = new List<Usuario>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT UsuarioID, Nombre, Email, Rol
                    FROM Usuarios
                    WHERE Activo = 1
                    ORDER BY UsuarioID DESC;";

                using var cmd = new MySqlCommand(query, conexion);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    usuarios.Add(new Usuario
                    {
                        UsuarioID = reader.GetInt32("UsuarioID"),
                        Nombre = reader.GetString("Nombre"),
                        Email = reader.GetString("Email"),
                        Rol = reader.GetString("Rol")
                    });
                }
            }

            dgUsuarios.ItemsSource = usuarios;
        }

        // =======================
        // CARGAR PROYECTOS (solo activos)
        // - separa sin asignar/asignados
        // - detecta profesor inactivo
        // =======================
        private void CargarProyectos()
        {
            var proyectosSinAsignar = new List<dynamic>();
            var proyectosAsignados = new List<dynamic>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT p.ProyectoID,
                           p.Titulo,
                           p.Estatus,
                           p.ProfesorID,
                           u.Nombre AS NombreProfesor,
                           u.Activo AS ProfesorActivo
                    FROM Proyectos p
                    LEFT JOIN Usuarios u ON p.ProfesorID = u.UsuarioID
                    WHERE p.Activo = 1
                    ORDER BY p.ProyectoID DESC;";

                using var cmd = new MySqlCommand(query, conexion);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    int? profesorId = reader.IsDBNull(reader.GetOrdinal("ProfesorID"))
                                      ? (int?)null
                                      : reader.GetInt32("ProfesorID");

                    string nombreProfesor;
                    if (profesorId == null)
                    {
                        nombreProfesor = "Sin asignar";
                    }
                    else
                    {
                        bool profActivo = reader.IsDBNull(reader.GetOrdinal("ProfesorActivo"))
                                          ? false
                                          : reader.GetBoolean("ProfesorActivo");

                        string np = reader.IsDBNull(reader.GetOrdinal("NombreProfesor"))
                                    ? "Sin asignar"
                                    : reader.GetString("NombreProfesor");

                        nombreProfesor = profActivo ? np : $"{np} (inactivo)";
                    }

                    var proyecto = new
                    {
                        ProyectoID = reader.GetInt32("ProyectoID"),
                        Titulo = reader.GetString("Titulo"),
                        Estatus = reader.GetString("Estatus"),
                        NombreProfesor = nombreProfesor,
                        ProfesorID = profesorId
                    };

                    if (proyecto.ProfesorID == null)
                        proyectosSinAsignar.Add(proyecto);
                    else
                        proyectosAsignados.Add(proyecto);
                }
            }

            dgProyectos.ItemsSource = proyectosSinAsignar;
            dgProyectosAsignados.ItemsSource = proyectosAsignados;
        }

        // =======================
        // CARGAR PROFESORES EN COMBOBOX (solo activos)
        // =======================
        private void CargarProfesoresComboBox()
        {
            var profesores = new List<Usuario>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT UsuarioID, Nombre
                    FROM Usuarios
                    WHERE Rol = 'Profesor'
                      AND Activo = 1
                    ORDER BY Nombre;";

                using var cmd = new MySqlCommand(query, conexion);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    profesores.Add(new Usuario
                    {
                        UsuarioID = reader.GetInt32("UsuarioID"),
                        Nombre = reader.GetString("Nombre")
                    });
                }
            }

            cbProfesores.ItemsSource = profesores;
            cbProfesores.DisplayMemberPath = "Nombre";
            cbProfesores.SelectedValuePath = "UsuarioID";
        }

        // =======================
        // ASIGNAR PROFESOR A PROYECTO + NOTIFICACIONES
        // =======================
        private void BtnAsignarProfesor_Click(object sender, RoutedEventArgs e)
        {
            if (dgProyectos.SelectedItem == null || cbProfesores.SelectedItem == null)
            {
                MessageBox.Show("Selecciona un proyecto y un profesor.",
                                "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dynamic proyectoSeleccionado = dgProyectos.SelectedItem;
            int proyectoID = proyectoSeleccionado.ProyectoID;
            int profesorID = (int)cbProfesores.SelectedValue;

            try
            {
                using var conexion = ConexionBD.ObtenerConexion();
                using var tx = conexion.BeginTransaction();

                // 1) Asignar profesor
                string updateProyecto = @"
                    UPDATE Proyectos
                    SET ProfesorID = @ProfesorID,
                        Estatus = IF(Estatus='Pendiente','En proceso', Estatus)
                    WHERE ProyectoID = @ProyectoID
                      AND Activo = 1;";

                using var cmd = new MySqlCommand(updateProyecto, conexion, tx);
                cmd.Parameters.AddWithValue("@ProfesorID", profesorID);
                cmd.Parameters.AddWithValue("@ProyectoID", proyectoID);
                cmd.ExecuteNonQuery();

                // 2) Obtener AlumnoID y Titulo
                int alumnoID = 0;
                string tituloProyecto = "";

                string qInfo = "SELECT AlumnoID, Titulo FROM Proyectos WHERE ProyectoID=@pid LIMIT 1;";
                using var cmdInfo = new MySqlCommand(qInfo, conexion, tx);
                cmdInfo.Parameters.AddWithValue("@pid", proyectoID);

                using (var rInfo = cmdInfo.ExecuteReader())
                {
                    if (rInfo.Read())
                    {
                        alumnoID = rInfo.GetInt32("AlumnoID");
                        tituloProyecto = rInfo.GetString("Titulo");
                    }
                }

                // 3) Notificar profesor
                string notifProfesor = @"
                    INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                    VALUES (@uid,
                            'Nuevo proyecto asignado',
                            CONCAT('Se te asignó el proyecto: ', @titulo),
                            'Proyecto',
                            @refId);";

                using var cmdNP = new MySqlCommand(notifProfesor, conexion, tx);
                cmdNP.Parameters.AddWithValue("@uid", profesorID);
                cmdNP.Parameters.AddWithValue("@titulo", tituloProyecto);
                cmdNP.Parameters.AddWithValue("@refId", proyectoID);
                cmdNP.ExecuteNonQuery();

                // 4) Notificar alumno
                string notifAlumno = @"
                    INSERT INTO Notificaciones (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                    VALUES (@uid,
                            'Profesor asignado',
                            'Tu proyecto ya tiene profesor asignado. Revisa tu panel.',
                            'Proyecto',
                            @refId);";

                using var cmdNA = new MySqlCommand(notifAlumno, conexion, tx);
                cmdNA.Parameters.AddWithValue("@uid", alumnoID);
                cmdNA.Parameters.AddWithValue("@refId", proyectoID);
                cmdNA.ExecuteNonQuery();

                tx.Commit();

                MessageBox.Show("Profesor asignado correctamente.",
                                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                CargarProyectos();
                cbProfesores.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al asignar profesor: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =======================
        // BUSCAR USUARIOS (por texto o por ID)
        // =======================
        private void BtnBuscar_Click(object sender, RoutedEventArgs e)
        {
            string textoBusqueda = txtBuscar.Text.Trim();

            if (string.IsNullOrEmpty(textoBusqueda))
            {
                CargarUsers();
                return;
            }

            bool esId = int.TryParse(textoBusqueda, out int idBuscado);
            var usuarios = new List<Usuario>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"
                    SELECT UsuarioID, Nombre, Email, Rol
                    FROM Usuarios
                    WHERE Activo = 1
                      AND (
                            (@esId = 1 AND UsuarioID = @id)
                         OR Nombre LIKE @busqueda
                         OR Email LIKE @busqueda
                         OR Rol LIKE @busqueda
                      )
                    ORDER BY UsuarioID DESC;";

                using var cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@esId", esId ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", idBuscado);
                cmd.Parameters.AddWithValue("@busqueda", "%" + textoBusqueda + "%");

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    usuarios.Add(new Usuario
                    {
                        UsuarioID = reader.GetInt32("UsuarioID"),
                        Nombre = reader.GetString("Nombre"),
                        Email = reader.GetString("Email"),
                        Rol = reader.GetString("Rol")
                    });
                }
            }

            dgUsuarios.ItemsSource = usuarios;
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            txtBuscar.Text = "";
            CargarUsers();
        }

        // =======================
        // CRUD Usuarios
        // =======================
        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            AgregarUsuarioWindow ventanaAgregar = new AgregarUsuarioWindow();
            ventanaAgregar.Owner = this;

            if (ventanaAgregar.ShowDialog() == true)
            {
                CargarUsers();
                CargarProfesoresComboBox();
            }
        }

        private void BtnEditar_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsuarios.SelectedItem is Usuario usuarioSeleccionado)
            {
                EditarUsuarioWindow ventanaEditar = new EditarUsuarioWindow(usuarioSeleccionado);
                ventanaEditar.Owner = this;

                if (ventanaEditar.ShowDialog() == true)
                {
                    CargarUsers();
                    CargarProfesoresComboBox();
                }
            }
            else
            {
                MessageBox.Show("Selecciona un usuario para editar.",
                                "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // =======================
        // ELIMINAR USUARIO (SOFT DELETE)
        // =======================
        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (dgUsuarios.SelectedItem is not Usuario usuarioSeleccionado)
            {
                MessageBox.Show("Selecciona un usuario para eliminar.",
                                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (usuarioSeleccionado.Rol == "Admin")
            {
                MessageBox.Show("No puedes eliminar administradores desde el panel.",
                                "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var resultado = MessageBox.Show(
                $"¿Deseas desactivar a {usuarioSeleccionado.Nombre}? (No se borrará historial)",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (resultado != MessageBoxResult.Yes) return;

            try
            {
                using (var conexion = ConexionBD.ObtenerConexion())
                {
                    string query = @"
                        UPDATE Usuarios
                        SET Activo = 0,
                            FechaBaja = NOW()
                        WHERE UsuarioID = @id;";

                    using var cmd = new MySqlCommand(query, conexion);
                    cmd.Parameters.AddWithValue("@id", usuarioSeleccionado.UsuarioID);
                    cmd.ExecuteNonQuery();
                }

                MessageBox.Show("Usuario desactivado correctamente.",
                                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

                CargarUsers();
                CargarProfesoresComboBox();
                CargarProyectos();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al desactivar usuario: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}




