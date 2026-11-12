using Entidades;
using Logica.Utilidades;
namespace Logica.Repositorios
{
    public class RepositorioVacantes : RepositorioCsv<Vacante>
    {
        public RepositorioVacantes() : base("vacantes.csv") { }
        protected override string Serializar(Vacante v) => CsvUtil.Unir(CsvUtil.DeEntero(v.Id), CsvUtil.DeEntero(v.EmprendedorId), CsvUtil.DeEntero(v.EmprendimientoId), v.Empresa, v.Cargo, v.Ciudad, v.Descripcion, CsvUtil.DeDecimal(v.Salario), CsvUtil.DeFecha(v.Fecha), v.Abierta ? "1" : "0", v.ClaveEmpresa);
        protected override Vacante Deserializar(string[] c) => new Vacante { Id = CsvUtil.AEntero(c[0]), EmprendedorId = CsvUtil.AEntero(c[1]), EmprendimientoId = CsvUtil.AEntero(c[2]), Empresa = c[3], Cargo = c[4], Ciudad = c[5], Descripcion = c[6], Salario = CsvUtil.ADecimal(c[7]), Fecha = CsvUtil.AFecha(c[8]), Abierta = c[9] == "1", ClaveEmpresa = c.Length > 10 ? c[10] : "" };
        protected override int ObtenerId(Vacante v) => v.Id;
        protected override void AsignarId(Vacante v, int id) => v.Id = id;
    }
}
