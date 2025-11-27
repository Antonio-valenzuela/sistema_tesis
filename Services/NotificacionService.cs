using System;
using System.Collections.Generic;
using MySql.Data.MySqlClient;
using SistemaSeguimientoTesis.Models;

namespace SistemaSeguimientoTesis.Services
{
    public static class NotificacionService
    {
        // Crear una notificación
        public static void CrearNotificacion(
            int usuarioId,
            string titulo,
            string mensaje,
            string tipo = null,
            int? referenciaId = null)
        {
            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"INSERT INTO Notificaciones
                                 (UsuarioID, Titulo, Mensaje, Tipo, ReferenciaID)
                                 VALUES (@UsuarioID, @Titulo, @Mensaje, @Tipo, @ReferenciaID)";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@UsuarioID", usuarioId);
                cmd.Parameters.AddWithValue("@Titulo", titulo);
                cmd.Parameters.AddWithValue("@Mensaje", mensaje);
                cmd.Parameters.AddWithValue("@Tipo", (object)tipo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ReferenciaID", (object)referenciaId ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        // Obtener lista de notificaciones de un usuario
        public static List<Notificacion> ObtenerNotificaciones(int usuarioId, bool soloNoLeidas = false)
        {
            List<Notificacion> lista = new List<Notificacion>();

            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"SELECT NotificacionID, UsuarioID, Titulo, Mensaje,
                                        Tipo, FechaCreacion, Leida, ReferenciaID
                                 FROM Notificaciones
                                 WHERE UsuarioID = @UsuarioID";

                if (soloNoLeidas)
                    query += " AND Leida = 0";

                query += " ORDER BY FechaCreacion DESC";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@UsuarioID", usuarioId);

                using (MySqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        lista.Add(new Notificacion
                        {
                            NotificacionID = reader.GetInt32("NotificacionID"),
                            UsuarioID = reader.GetInt32("UsuarioID"),
                            Titulo = reader.GetString("Titulo"),
                            Mensaje = reader.GetString("Mensaje"),
                            Tipo = reader.IsDBNull("Tipo") ? null : reader.GetString("Tipo"),
                            FechaCreacion = reader.GetDateTime("FechaCreacion"),
                            Leida = reader.GetBoolean("Leida"),
                            ReferenciaID = reader.IsDBNull("ReferenciaID")
                                ? (int?)null
                                : reader.GetInt32("ReferenciaID")
                        });
                    }
                }
            }

            return lista;
        }

        // Marcar una notificación como leída
        public static void MarcarComoLeida(int notificacionId)
        {
            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"UPDATE Notificaciones
                                 SET Leida = 1
                                 WHERE NotificacionID = @Id";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@Id", notificacionId);
                cmd.ExecuteNonQuery();
            }
        }

        // Marcar todas las notificaciones de un usuario como leídas
        public static void MarcarTodasComoLeidas(int usuarioId)
        {
            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"UPDATE Notificaciones
                                 SET Leida = 1
                                 WHERE UsuarioID = @UsuarioID AND Leida = 0";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@UsuarioID", usuarioId);
                cmd.ExecuteNonQuery();
            }
        }

        // Contar no leídas (para el ícono ??)
        public static int ContarNoLeidas(int usuarioId)
        {
            using (var conexion = ConexionBD.ObtenerConexion())
            {
                string query = @"SELECT COUNT(*) FROM Notificaciones
                                 WHERE UsuarioID = @UsuarioID AND Leida = 0";

                MySqlCommand cmd = new MySqlCommand(query, conexion);
                cmd.Parameters.AddWithValue("@UsuarioID", usuarioId);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }
    }
}
