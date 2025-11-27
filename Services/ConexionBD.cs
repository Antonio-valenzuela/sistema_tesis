using MySql.Data.MySqlClient;

namespace SistemaSeguimientoTesis.Services
{
    public class ConexionBD
    {
        private static string connectionString =
     "Server=localhost;Database=SistemaTesis;Uid=tesis;Pwd=tesis123;SslMode=none;";

        public static MySqlConnection ObtenerConexion()
        {
            MySqlConnection conexion = new MySqlConnection(connectionString);
            conexion.Open();
            return conexion;
        }
    }
}

