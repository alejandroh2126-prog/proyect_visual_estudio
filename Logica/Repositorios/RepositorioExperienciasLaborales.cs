using Entidades;
using Logica.Utilidades;
namespace Logica.Repositorios
{
    public class RepositorioExperienciasLaborales : RepositorioCsv<ExperienciaLaboral>
    {
        public RepositorioExperienciasLaborales() : base("experiencias_laborales.csv") { }
        protected override string Serializar(ExperienciaLaboral e) => CsvUtil.Unir(CsvUtil.DeEntero(e.Id), CsvUtil.DeEntero(e.UsuarioId), e.Empresa, e.Cargo, CsvUtil.DeFecha(e.Inicio), CsvUtil.DeFecha(e.Fin), e.Motivo);
        protected override ExperienciaLaboral Deserializar(string[] c) => new ExperienciaLaboral { Id = CsvUtil.AEntero(c[0]), UsuarioId = CsvUtil.AEntero(c[1]), Empresa = c[2], Cargo = c[3], Inicio = CsvUtil.AFecha(c[4]), Fin = CsvUtil.AFecha(c[5]), Motivo = c[6] };
        protected override int ObtenerId(ExperienciaLaboral e) => e.Id;
        protected override void AsignarId(ExperienciaLaboral e, int id) => e.Id = id;
    }
}
