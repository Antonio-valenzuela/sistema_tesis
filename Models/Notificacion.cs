namespace SistemaSeguimientoTesis.Models
{
    public class Notificacion
    {
        public int NotificacionID { get; set; }
        public int AlumnoID { get; set; }
        public string Titulo { get; set; }
        public string Mensaje { get; set; }
        public DateTime Fecha { get; set; }
        public string Estado { get; set; }

        // Propiedad de solo lectura para mostrar la fecha formateada
        public string FechaTexto => Fecha.ToString("dd/MM/yyyy HH:mm");
    }
}

