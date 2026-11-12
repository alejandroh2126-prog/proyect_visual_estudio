using Entidades;
using Logica.Utilidades;
namespace Logica.Repositorios
{
    public class RepositorioFichasTrabajador : RepositorioCsv<FichaTrabajador>
    {
        public RepositorioFichasTrabajador() : base("fichas_trabajadores.csv") { }
        protected override string Serializar(FichaTrabajador f) => CsvUtil.Unir(CsvUtil.DeEntero(f.Id), f.ClaveTrabajador, f.Telefono, f.Correo, f.Ciudad, f.Direccion, f.Nacionalidad, f.Estudios, f.TipoContrato, f.Foto, f.DocumentoFrente, f.DocumentoReverso, f.HojaVida);
        protected override FichaTrabajador Deserializar(string[] c) => new FichaTrabajador { Id = CsvUtil.AEntero(c[0]), ClaveTrabajador = c[1], Telefono = c[2], Correo = c[3], Ciudad = c[4], Direccion = c[5], Nacionalidad = c[6], Estudios = c[7], TipoContrato = c[8], Foto = c[9], DocumentoFrente = c[10], DocumentoReverso = c[11], HojaVida = c[12] };
        protected override int ObtenerId(FichaTrabajador f) => f.Id;
        protected override void AsignarId(FichaTrabajador f, int id) => f.Id = id;
    }
}
