using System;
using Entidades;
using Logica.Utilidades;
namespace Logica.Repositorios
{
    public class RepositorioPostulaciones : RepositorioCsv<Postulacion>
    {
        public RepositorioPostulaciones() : base("postulaciones.csv") { }
        protected override string Serializar(Postulacion p) => CsvUtil.Unir(CsvUtil.DeEntero(p.Id), CsvUtil.DeEntero(p.VacanteId), CsvUtil.DeEntero(p.TrabajadorUsuarioId), CsvUtil.DeEntero(p.EmprendedorUsuarioId), CsvUtil.DeEntero(p.EmprendimientoId), p.Empresa, p.Cargo, CsvUtil.DeFecha(p.Fecha), p.Estado.ToString(), p.ClaveEmpresa);
        protected override Postulacion Deserializar(string[] c) => new Postulacion { Id = CsvUtil.AEntero(c[0]), VacanteId = CsvUtil.AEntero(c[1]), TrabajadorUsuarioId = CsvUtil.AEntero(c[2]), EmprendedorUsuarioId = CsvUtil.AEntero(c[3]), EmprendimientoId = CsvUtil.AEntero(c[4]), Empresa = c[5], Cargo = c[6], Fecha = CsvUtil.AFecha(c[7]), Estado = Enum.Parse<EstadoPostulacion>(c[8]), ClaveEmpresa = c.Length > 9 ? c[9] : "" };
        protected override int ObtenerId(Postulacion p) => p.Id;
        protected override void AsignarId(Postulacion p, int id) => p.Id = id;
    }
}
