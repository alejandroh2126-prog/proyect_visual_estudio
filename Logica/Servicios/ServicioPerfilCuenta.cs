using System;
using System.Globalization;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    public class ServicioPerfilCuenta
    {
        private readonly RepositorioPerfilesCuenta _repo;
        public ServicioPerfilCuenta(RepositorioPerfilesCuenta repo) { _repo = repo; }
        private static string Ambito(int id) => "cuenta-" + id;
        public PerfilCuenta Obtener()
        {
            int id = Sesion.IdUsuario;
            return _repo.Listar().Find(p => p.UsuarioId == id) ?? new PerfilCuenta { UsuarioId = id };
        }
        private static void ValidarColor(string valor, bool fondo)
        {
            if (valor == "") return;
            if (valor.Length != 7 || valor[0] != '#' || !int.TryParse(valor.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                throw new ReglaDeNegocioException("Color inválido.");
            double Canal(int c) { double v = c / 255.0; return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
            double l = 0.2126 * Canal((rgb >> 16) & 255) + 0.7152 * Canal((rgb >> 8) & 255) + 0.0722 * Canal(rgb & 255);
            if (fondo ? l < 0.65 : l > 0.18)
                throw new ReglaDeNegocioException(fondo ? "Elige un fondo claro para conservar la legibilidad." : "Elige un color oscuro para los botones de texto blanco.");
        }
        public PerfilCuenta Guardar(string fondo, string botones, string? fotoNueva, bool quitarFoto)
        {
            ValidarColor(fondo, true); ValidarColor(botones, false);
            PerfilCuenta p = Obtener(); string anterior = p.Foto;
            string nuevo = fotoNueva == null ? "" : ArchivosPrivados.Importar(Ambito(p.UsuarioId), fotoNueva, true, false);
            p.ColorFondo = fondo; p.ColorBotones = botones;
            if (fotoNueva != null) p.Foto = nuevo; else if (quitarFoto) p.Foto = "";
            try { if (p.Id == 0) _repo.Insertar(p); else _repo.Actualizar(p); }
            catch { ArchivosPrivados.Borrar(Ambito(p.UsuarioId), nuevo); throw; }
            if (anterior != p.Foto) ArchivosPrivados.Borrar(Ambito(p.UsuarioId), anterior);
            return p;
        }
        public string RutaFoto() { PerfilCuenta p = Obtener(); return ArchivosPrivados.Ruta(Ambito(p.UsuarioId), p.Foto); }
    }
}
