using Entidades;
using Logica.Utilidades;

namespace Logica.Repositorios
{
    public class RepositorioUsuarios : RepositorioCsv<Usuario>
    {
        public RepositorioUsuarios() : base("usuarios.csv") { }

        protected override string Serializar(Usuario u)
        {
            return CsvUtil.Unir(CsvUtil.DeEntero(u.Id), u.Nombre, u.Email, u.ClaveHash, CsvUtil.DeFecha(u.FechaRegistro), u.Rol.ToString());
        }

        protected override Usuario? Deserializar(string[] c)
        {
            return new Usuario
            {
                Id = CsvUtil.AEntero(c[0]),
                Nombre = c[1],
                Email = c[2],
                ClaveHash = c[3],
                FechaRegistro = CsvUtil.AFecha(c[4]),
                Rol = c.Length > 5 ? System.Enum.Parse<RolUsuario>(c[5]) : RolUsuario.Emprendedor
            };
        }

        protected override int ObtenerId(Usuario u) { return u.Id; }
        protected override void AsignarId(Usuario u, int id) { u.Id = id; }
    }
}