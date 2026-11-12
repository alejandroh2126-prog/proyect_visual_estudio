using System;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;
namespace Logica.Servicios
{
    public class ServicioFichaTrabajador
    {
        private readonly RepositorioFichasTrabajador _fichas;
        private readonly RepositorioTrabajadores _trabajadores;
        private readonly RepositorioEmprendimientos _emp;
        public ServicioFichaTrabajador(RepositorioFichasTrabajador fichas, RepositorioTrabajadores trabajadores, RepositorioEmprendimientos emp) { _fichas = fichas; _trabajadores = trabajadores; _emp = emp; }
        private Trabajador Autorizar(int trabajadorId)
        {
            Trabajador? t = _trabajadores.ObtenerPorId(trabajadorId);
            if (t == null || _emp.ObtenerPorId(t.EmprendimientoId)?.UsuarioId != Sesion.IdUsuario)
                throw new ReglaDeNegocioException("Trabajador no encontrado o sin autorización.");
            return t;
        }
        private static string Ambito(string clave) => "empleado-" + clave;
        private FichaTrabajador Cargar(Trabajador t) => _fichas.Listar().Find(f => f.ClaveTrabajador == t.ClaveFicha && t.ClaveFicha != "") ?? new FichaTrabajador { ClaveTrabajador = t.ClaveFicha };
        public FichaTrabajador Obtener(int id) => Cargar(Autorizar(id));
        private Trabajador Preparar(int id)
        {
            Trabajador t = Autorizar(id);
            if (t.ClaveFicha == "") { t.ClaveFicha = Guid.NewGuid().ToString("N"); _trabajadores.Actualizar(t); }
            return t;
        }
        private void Persistir(FichaTrabajador f) { if (f.Id == 0) _fichas.Insertar(f); else _fichas.Actualizar(f); }
        public void Guardar(int id, FichaTrabajador datos)
        {
            if (!string.IsNullOrWhiteSpace(datos.Correo)) Validar.Correo(datos.Correo);
            FichaTrabajador f = Cargar(Preparar(id));
            f.Telefono = datos.Telefono.Trim(); f.Correo = datos.Correo.Trim(); f.Ciudad = datos.Ciudad.Trim(); f.Direccion = datos.Direccion.Trim(); f.Nacionalidad = datos.Nacionalidad.Trim(); f.Estudios = datos.Estudios.Trim(); f.TipoContrato = datos.TipoContrato.Trim();
            Persistir(f);
        }
        private static string Leer(FichaTrabajador f, AdjuntoTrabajador tipo) => tipo switch { AdjuntoTrabajador.Foto => f.Foto, AdjuntoTrabajador.DocumentoFrente => f.DocumentoFrente, AdjuntoTrabajador.DocumentoReverso => f.DocumentoReverso, AdjuntoTrabajador.HojaVida => f.HojaVida, _ => throw new ReglaDeNegocioException("Tipo inválido.") };
        private static void Escribir(FichaTrabajador f, AdjuntoTrabajador tipo, string valor)
        {
            switch (tipo) { case AdjuntoTrabajador.Foto: f.Foto = valor; break; case AdjuntoTrabajador.DocumentoFrente: f.DocumentoFrente = valor; break; case AdjuntoTrabajador.DocumentoReverso: f.DocumentoReverso = valor; break; case AdjuntoTrabajador.HojaVida: f.HojaVida = valor; break; default: throw new ReglaDeNegocioException("Tipo inválido."); }
        }
        public void CambiarAdjunto(int id, AdjuntoTrabajador tipo, string? origen)
        {
            FichaTrabajador f = Cargar(Preparar(id)); string anterior = Leer(f, tipo); string ambito = Ambito(f.ClaveTrabajador);
            string nuevo = origen == null ? "" : ArchivosPrivados.Importar(ambito, origen, tipo != AdjuntoTrabajador.HojaVida, tipo != AdjuntoTrabajador.Foto);
            Escribir(f, tipo, nuevo);
            try { Persistir(f); } catch { ArchivosPrivados.Borrar(ambito, nuevo); throw; }
            ArchivosPrivados.Borrar(ambito, anterior);
        }
        public string RutaAdjunto(int id, AdjuntoTrabajador tipo) { FichaTrabajador f = Obtener(id); return ArchivosPrivados.Ruta(Ambito(f.ClaveTrabajador), Leer(f, tipo)); }
        public void EliminarFicha(int id)
        {
            FichaTrabajador f = Obtener(id); if (f.Id == 0) return;
            _fichas.Eliminar(f.Id);
            foreach (AdjuntoTrabajador tipo in Enum.GetValues<AdjuntoTrabajador>()) ArchivosPrivados.Borrar(Ambito(f.ClaveTrabajador), Leer(f, tipo));
        }
    }
}
