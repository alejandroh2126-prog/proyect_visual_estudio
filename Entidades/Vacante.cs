using System;
namespace Entidades
{
    public class Vacante
    {
        public int Id { get; set; }
        public int EmprendedorId { get; set; }
        public int EmprendimientoId { get; set; }
        public string ClaveEmpresa { get; set; } = "";
        public string Empresa { get; set; } = "";
        public string Cargo { get; set; } = "";
        public string Ciudad { get; set; } = "";
        public string Descripcion { get; set; } = "";
        public decimal Salario { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public bool Abierta { get; set; } = true;
    }
}
