using System;
namespace Entidades
{
    public enum EstadoPostulacion { Pendiente, Oferta, Rechazada, Contratada, Finalizada }
    public class Postulacion
    {
        public int Id { get; set; }
        public int VacanteId { get; set; }
        public int TrabajadorUsuarioId { get; set; }
        public int EmprendedorUsuarioId { get; set; }
        public int EmprendimientoId { get; set; }
        public string ClaveEmpresa { get; set; } = "";
        public string Empresa { get; set; } = "";
        public string Cargo { get; set; } = "";
        public DateTime Fecha { get; set; } = DateTime.Now;
        public EstadoPostulacion Estado { get; set; }
    }
}
