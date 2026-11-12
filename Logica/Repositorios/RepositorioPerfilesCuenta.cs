using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
    public class RepositorioPerfilesCuenta : RepositorioCsv<PerfilCuenta>
    {
        public RepositorioPerfilesCuenta() : base("perfiles_cuenta.csv") { }
        protected override string Serializar(PerfilCuenta p) => CsvUtil.Unir(CsvUtil.DeEntero(p.Id), CsvUtil.DeEntero(p.UsuarioId), p.Foto, p.ColorFondo, p.ColorBotones);
        protected override PerfilCuenta Deserializar(string[] c) => new PerfilCuenta { Id = CsvUtil.AEntero(c[0]), UsuarioId = CsvUtil.AEntero(c[1]), Foto = c[2], ColorFondo = c[3], ColorBotones = c[4] };
        protected override int ObtenerId(PerfilCuenta p) => p.Id;
        protected override void AsignarId(PerfilCuenta p, int id) => p.Id = id;
    }
}
