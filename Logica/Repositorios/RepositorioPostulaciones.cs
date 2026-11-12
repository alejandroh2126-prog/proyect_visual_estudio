using System;
using Entidades;
using Logica.Utilidades;
namespace Logica.Repositorios
{
    public class RepositorioPostulaciones : RepositorioCsv<Postulacion>
    {
        public RepositorioPostulaciones() : base("postulaciones.csv") { }
        protected override string Serializar(Postulacion p) => CsvUtil.Unir(CsvUtil.DeEntero(p.Id), CsvUtil.DeEntero(p.VacanteId), CsvUtil.DeEntero(p.TrabajadorUsuarioId), CsvUtil.DeEntero(p.EmprendedorUsuarioId), CsvUtil.DeEntero(p.EmprendimientoId), p.Empresa, p.Cargo, CsvUtil.DeFecha(p.Fecha), p.Estado.ToString(), p.ClaveEmpresa, p.NombreTrabajador, p.InicioLaboral.HasValue ? CsvUtil.DeFecha(p.InicioLaboral.Value) : "", p.FinLaboral.HasValue ? CsvUtil.DeFecha(p.FinLaboral.Value) : "", p.MotivoRetiro, p.SolicitudRevision, p.RespuestaRevision, CsvUtil.DeDecimal(p.SalarioPropuesto));
        protected override Postulacion Deserializar(string[] c) => new Postulacion { Id = CsvUtil.AEntero(c[0]), VacanteId = CsvUtil.AEntero(c[1]), TrabajadorUsuarioId = CsvUtil.AEntero(c[2]), EmprendedorUsuarioId = CsvUtil.AEntero(c[3]), EmprendimientoId = CsvUtil.AEntero(c[4]), Empresa = c[5], Cargo = c[6], Fecha = CsvUtil.AFecha(c[7]), Estado = Enum.Parse<EstadoPostulacion>(c[8]), ClaveEmpresa = c.Length > 9 ? c[9] : "",
            NombreTrabajador = c.Length > 10 ? c[10] : "", InicioLaboral = c.Length > 11 && c[11] != "" ? CsvUtil.AFecha(c[11]) : null, FinLaboral = c.Length > 12 && c[12] != "" ? CsvUtil.AFecha(c[12]) : null, MotivoRetiro = c.Length > 13 ? c[13] : "", SolicitudRevision = c.Length > 14 ? c[14] : "", RespuestaRevision = c.Length > 15 ? c[15] : "", SalarioPropuesto = c.Length > 16 ? CsvUtil.ADecimal(c[16]) : 0m };
        protected override int ObtenerId(Postulacion p) => p.Id;
        protected override void AsignarId(Postulacion p, int id) => p.Id = id;
    }
}
