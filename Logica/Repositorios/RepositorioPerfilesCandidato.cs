using Entidades;
using Logica.Utilidades;
namespace Logica.Repositorios
{
    public class RepositorioPerfilesCandidato : RepositorioCsv<PerfilCandidato>
    {
        public RepositorioPerfilesCandidato() : base("perfiles_candidatos.csv") { }
        protected override string Serializar(PerfilCandidato p) => CsvUtil.Unir(CsvUtil.DeEntero(p.Id), CsvUtil.DeEntero(p.UsuarioId), p.Ciudad, p.Telefono, p.Profesion, p.Experiencia, p.TipoDocumento, p.NumeroDocumento, p.HojaVida, p.Visible ? "1" : "0", p.DocumentoFrente, p.DocumentoReverso);
        protected override PerfilCandidato Deserializar(string[] c) => new PerfilCandidato { Id = CsvUtil.AEntero(c[0]), UsuarioId = CsvUtil.AEntero(c[1]), Ciudad = c[2], Telefono = c[3], Profesion = c[4], Experiencia = c[5], TipoDocumento = c[6], NumeroDocumento = c[7], HojaVida = c[8], Visible = c[9] == "1", DocumentoFrente = c.Length > 10 ? c[10] : "", DocumentoReverso = c.Length > 11 ? c[11] : "" };
        protected override int ObtenerId(PerfilCandidato p) => p.Id;
        protected override void AsignarId(PerfilCandidato p, int id) => p.Id = id;
    }
}
