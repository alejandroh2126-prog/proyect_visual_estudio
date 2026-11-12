using System;
using System.Collections.Generic;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;
namespace Logica.Servicios
{
    public class ServicioHistorialLaboral
    {
        private readonly RepositorioExperienciasLaborales _repo;
        private readonly ServicioEmpleos _empleos;
        public ServicioHistorialLaboral(RepositorioExperienciasLaborales repo, ServicioEmpleos empleos) { _repo = repo; _empleos = empleos; }
        private static int Usuario()
        {
            if (Sesion.UsuarioActual?.Rol != RolUsuario.Trabajador) throw new ReglaDeNegocioException("Solo disponible para trabajadores.");
            return Sesion.IdUsuario;
        }
        public List<ExperienciaLaboral> ListarDeclaradas() { int id = Usuario(); return _repo.Listar().FindAll(e => e.UsuarioId == id); }
        public List<Postulacion> ListarRegistradas() => _empleos.ListarMisPostulaciones().FindAll(p => p.Estado == EstadoPostulacion.Contratada || p.Estado == EstadoPostulacion.Finalizada);
        public void Guardar(ExperienciaLaboral datos)
        {
            int id = Usuario(); Validar.Requerido(datos.Empresa, "Empresa"); Validar.Requerido(datos.Cargo, "Cargo");
            if (datos.Inicio.Date > datos.Fin.Date || datos.Fin.Date > DateTime.Today) throw new ReglaDeNegocioException("Las fechas deben ser coherentes y no futuras.");
            if (!MotivosRetiro.EsValido(datos.Motivo)) throw new ReglaDeNegocioException("Selecciona un motivo válido.");
            ExperienciaLaboral e;
            if (datos.Id == 0) e = new ExperienciaLaboral { UsuarioId = id };
            else { e = _repo.ObtenerPorId(datos.Id) ?? throw new ReglaDeNegocioException("Experiencia no encontrada."); if (e.UsuarioId != id) throw new ReglaDeNegocioException("Sin autorización."); }
            e.Empresa = datos.Empresa.Trim(); e.Cargo = datos.Cargo.Trim(); e.Inicio = datos.Inicio.Date; e.Fin = datos.Fin.Date; e.Motivo = datos.Motivo;
            if (e.Id == 0) _repo.Insertar(e); else _repo.Actualizar(e);
        }
        public void Eliminar(int id)
        {
            int usuario = Usuario(); ExperienciaLaboral? e = _repo.ObtenerPorId(id);
            if (e == null || e.UsuarioId != usuario) throw new ReglaDeNegocioException("Experiencia no encontrada o sin autorización.");
            _repo.Eliminar(id);
        }
    }
}
