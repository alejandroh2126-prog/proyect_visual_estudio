using System;
using System.Collections.Generic;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;
namespace Logica.Servicios
{
    public class ServicioEmpleos
    {
        private readonly RepositorioPerfilesCandidato _perfiles;
        private readonly RepositorioVacantes _vacantes;
        private readonly RepositorioPostulaciones _postulaciones;
        private readonly RepositorioUsuarios _usuarios;
        private readonly RepositorioEmprendimientos _emp;
        public ServicioEmpleos(RepositorioPerfilesCandidato perfiles, RepositorioVacantes vacantes, RepositorioPostulaciones postulaciones, RepositorioUsuarios usuarios, RepositorioEmprendimientos emp)
        { _perfiles = perfiles; _vacantes = vacantes; _postulaciones = postulaciones; _usuarios = usuarios; _emp = emp; }
        private static int Exigir(RolUsuario rol)
        {
            int id = Sesion.IdUsuario;
            if (Sesion.UsuarioActual?.Rol != rol) throw new ReglaDeNegocioException("Esta función no está disponible para tu tipo de cuenta.");
            return id;
        }
        private Emprendimiento AutorizarEmpresa(int id)
        {
            int usuario = Exigir(RolUsuario.Emprendedor);
            Emprendimiento? e = _emp.ObtenerPorId(id);
            if (e == null || e.UsuarioId != usuario) throw new ReglaDeNegocioException("Emprendimiento no encontrado o sin autorización.");
            return e;
        }
        private static string Ambito(int id) => "candidato-" + id;
        public PerfilCandidato ObtenerMiPerfil()
        {
            int id = Exigir(RolUsuario.Trabajador);
            return _perfiles.Listar().Find(p => p.UsuarioId == id) ?? new PerfilCandidato { UsuarioId = id };
        }
        private void Guardar(PerfilCandidato p) { if (p.Id == 0) _perfiles.Insertar(p); else _perfiles.Actualizar(p); }
        public void GuardarMiPerfil(PerfilCandidato datos)
        {
            PerfilCandidato p = ObtenerMiPerfil();
            if (!TiposDocumento.EsValido(datos.TipoDocumento)) throw new ReglaDeNegocioException("Tipo de documento inválido.");
            if (datos.Visible) { Validar.Requerido(datos.Ciudad, "Ciudad"); Validar.Requerido(datos.Profesion, "Perfil profesional"); }
            p.Ciudad = datos.Ciudad.Trim(); p.Telefono = datos.Telefono.Trim(); p.Profesion = datos.Profesion.Trim(); p.Experiencia = datos.Experiencia.Trim(); p.TipoDocumento = datos.TipoDocumento; p.NumeroDocumento = datos.NumeroDocumento.Trim(); p.Visible = datos.Visible;
            Guardar(p);
        }
        public void CambiarMiCv(string? origen)
        {
            PerfilCandidato p = ObtenerMiPerfil(); string ambito = Ambito(p.UsuarioId); string anterior = p.HojaVida;
            string nuevo = origen == null ? "" : ArchivosPrivados.Importar(ambito, origen, false, true);
            p.HojaVida = nuevo;
            try { Guardar(p); } catch { ArchivosPrivados.Borrar(ambito, nuevo); throw; }
            ArchivosPrivados.Borrar(ambito, anterior);
        }
        public void CambiarMiIdentificacion(bool reverso, string? origen)
        {
            PerfilCandidato p = ObtenerMiPerfil(); string ambito = Ambito(p.UsuarioId);
            string anterior = reverso ? p.DocumentoReverso : p.DocumentoFrente;
            string nuevo = origen == null ? "" : ArchivosPrivados.Importar(ambito, origen, true, true);
            if (reverso) p.DocumentoReverso = nuevo; else p.DocumentoFrente = nuevo;
            try { Guardar(p); } catch { ArchivosPrivados.Borrar(ambito, nuevo); throw; }
            ArchivosPrivados.Borrar(ambito, anterior);
        }
        public string RutaMiIdentificacion(bool reverso)
        {
            PerfilCandidato p = ObtenerMiPerfil();
            return ArchivosPrivados.Ruta(Ambito(p.UsuarioId), reverso ? p.DocumentoReverso : p.DocumentoFrente);
        }
        public string RutaMiCv() { PerfilCandidato p = ObtenerMiPerfil(); return ArchivosPrivados.Ruta(Ambito(p.UsuarioId), p.HojaVida); }
        public List<CandidatoPublico> ListarCandidatos()
        {
            Exigir(RolUsuario.Emprendedor);
            List<CandidatoPublico> lista = new List<CandidatoPublico>();
            foreach (PerfilCandidato p in _perfiles.Listar())
            {
                if (!p.Visible) continue;
                Usuario? u = _usuarios.ObtenerPorId(p.UsuarioId);
                if (u == null || u.Rol != RolUsuario.Trabajador) continue;
                lista.Add(new CandidatoPublico { UsuarioId = u.Id, Nombre = u.Nombre, Ciudad = p.Ciudad, Profesion = p.Profesion, Experiencia = p.Experiencia, TieneHojaVida = p.HojaVida != "" });
            }
            return lista;
        }
        public string RutaCvCandidato(int usuarioId)
        {
            Exigir(RolUsuario.Emprendedor);
            PerfilCandidato? p = _perfiles.Listar().Find(x => x.UsuarioId == usuarioId && x.Visible);
            if (p == null || _usuarios.ObtenerPorId(usuarioId)?.Rol != RolUsuario.Trabajador) throw new ReglaDeNegocioException("El perfil ya no está compartido.");
            return ArchivosPrivados.Ruta(Ambito(usuarioId), p.HojaVida);
        }
        public Vacante CrearVacante(int empresaId, string cargo, string ciudad, string descripcion, decimal salario)
        {
            Emprendimiento e = AutorizarEmpresa(empresaId);
            if (e.Estado != Estados.Activo) throw new ReglaDeNegocioException("El emprendimiento está inactivo.");
            Validar.Requerido(cargo, "Cargo"); Validar.Requerido(ciudad, "Ciudad"); Validar.Requerido(descripcion, "Descripción"); Validar.NoNegativo(salario, "Salario");
            if (e.ClaveLaboral == "") { e.ClaveLaboral = Guid.NewGuid().ToString("N"); _emp.Actualizar(e); }
            return _vacantes.Insertar(new Vacante { EmprendedorId = Sesion.IdUsuario, EmprendimientoId = e.Id, ClaveEmpresa = e.ClaveLaboral, Empresa = e.Nombre, Cargo = cargo.Trim(), Ciudad = ciudad.Trim(), Descripcion = descripcion.Trim(), Salario = salario });
        }
        private bool Publicable(Vacante v)
        {
            Emprendimiento? e = _emp.ObtenerPorId(v.EmprendimientoId);
            return v.Abierta && e != null && e.UsuarioId == v.EmprendedorId && v.ClaveEmpresa != "" && v.ClaveEmpresa == e.ClaveLaboral && e.Estado == Estados.Activo;
        }
        public List<Vacante> ListarVacantesPublicas() { Exigir(RolUsuario.Trabajador); return _vacantes.Listar().FindAll(Publicable); }
        public List<Vacante> ListarVacantesPropias(int empresaId) { Emprendimiento e = AutorizarEmpresa(empresaId); return _vacantes.Listar().FindAll(v => v.EmprendedorId == Sesion.IdUsuario && v.EmprendimientoId == empresaId && v.ClaveEmpresa == e.ClaveLaboral); }
        public void CerrarVacante(int id)
        {
            Vacante? v = _vacantes.ObtenerPorId(id);
            if (v == null || v.EmprendedorId != Exigir(RolUsuario.Emprendedor)) throw new ReglaDeNegocioException("Vacante no encontrada.");
            if (AutorizarEmpresa(v.EmprendimientoId).ClaveLaboral != v.ClaveEmpresa) throw new ReglaDeNegocioException("Vacante de un negocio eliminado.");
            v.Abierta = false; _vacantes.Actualizar(v);
        }
        public void CerrarVacantesEmpresa(int empresaId)
        {
            if (ListarPostulacionesEmpresa(empresaId).Exists(p => p.Estado == EstadoPostulacion.Contratada))
                throw new ReglaDeNegocioException("Finaliza las vinculaciones activas antes de eliminar el emprendimiento.");
            foreach (Vacante v in ListarVacantesPropias(empresaId)) if (v.Abierta) CerrarVacante(v.Id);
        }
        public void Postular(int vacanteId)
        {
            PerfilCandidato perfil = ObtenerMiPerfil();
            if (!perfil.Visible) throw new ReglaDeNegocioException("Primero guarda tu perfil profesional y autoriza compartirlo con emprendedores.");
            Vacante? v = _vacantes.ObtenerPorId(vacanteId);
            if (v == null || !Publicable(v)) throw new ReglaDeNegocioException("La vacante ya no está disponible.");
            if (_postulaciones.Listar().Exists(p => p.VacanteId == vacanteId && p.TrabajadorUsuarioId == perfil.UsuarioId)) throw new ReglaDeNegocioException("Ya te postulaste a esta vacante.");
            _postulaciones.Insertar(new Postulacion { VacanteId = v.Id, TrabajadorUsuarioId = perfil.UsuarioId, EmprendedorUsuarioId = v.EmprendedorId, EmprendimientoId = v.EmprendimientoId, ClaveEmpresa = v.ClaveEmpresa, Empresa = v.Empresa, Cargo = v.Cargo, NombreTrabajador = Sesion.UsuarioActual!.Nombre, SalarioPropuesto = v.Salario, Estado = EstadoPostulacion.Pendiente });
        }
        private Postulacion ObtenerPostulacion(int id) => _postulaciones.ObtenerPorId(id) ?? throw new ReglaDeNegocioException("Postulación no encontrada.");
        private Postulacion PostulacionEmpresa(int id)
        {
            int usuario = Exigir(RolUsuario.Emprendedor); Postulacion p = ObtenerPostulacion(id);
            if (p.EmprendedorUsuarioId != usuario || AutorizarEmpresa(p.EmprendimientoId).ClaveLaboral != p.ClaveEmpresa) throw new ReglaDeNegocioException("Sin autorización para esta postulación.");
            return p;
        }
        private Postulacion PostulacionPropia(int id)
        {
            int usuario = Exigir(RolUsuario.Trabajador); Postulacion p = ObtenerPostulacion(id);
            if (p.TrabajadorUsuarioId != usuario) throw new ReglaDeNegocioException("Sin autorización para esta postulación.");
            return p;
        }
        public void Ofertar(int id)
        {
            Postulacion p = PostulacionEmpresa(id);
            if (p.Estado != EstadoPostulacion.Pendiente) throw new ReglaDeNegocioException("Solo se puede ofertar una postulación pendiente.");
            Vacante? v = _vacantes.ObtenerPorId(p.VacanteId);
            if (v == null || !Publicable(v)) throw new ReglaDeNegocioException("La vacante está cerrada.");
            if (!_perfiles.Listar().Exists(x => x.UsuarioId == p.TrabajadorUsuarioId && x.Visible)) throw new ReglaDeNegocioException("El candidato retiró el acceso a su perfil.");
            p.Estado = EstadoPostulacion.Oferta; _postulaciones.Actualizar(p);
        }
        public void Rechazar(int id)
        {
            Postulacion p = PostulacionEmpresa(id);
            if (p.Estado != EstadoPostulacion.Pendiente && p.Estado != EstadoPostulacion.Oferta) throw new ReglaDeNegocioException("La postulación ya no está pendiente.");
            p.Estado = EstadoPostulacion.Rechazada; _postulaciones.Actualizar(p);
        }
        public void ResponderOferta(int id, bool aceptar)
        {
            Postulacion p = PostulacionPropia(id);
            if (p.Estado != EstadoPostulacion.Oferta) throw new ReglaDeNegocioException("No hay una oferta pendiente.");
            if (aceptar)
            {
                Vacante? v = _vacantes.ObtenerPorId(p.VacanteId);
                if (v == null || !Publicable(v)) throw new ReglaDeNegocioException("La vacante ya no está disponible.");
                p.Estado = EstadoPostulacion.Contratada; p.InicioLaboral = DateTime.Today; p.NombreTrabajador = Sesion.UsuarioActual!.Nombre;
            }
            else p.Estado = EstadoPostulacion.Rechazada;
            _postulaciones.Actualizar(p);
        }
        public void FinalizarVinculacion(int id, string motivo)
        {
            Postulacion p = PostulacionEmpresa(id);
            if (p.Estado != EstadoPostulacion.Contratada) throw new ReglaDeNegocioException("Solo se finalizan vinculaciones activas.");
            if (!MotivosRetiro.EsValido(motivo)) throw new ReglaDeNegocioException("Motivo inválido.");
            p.Estado = EstadoPostulacion.Finalizada; p.FinLaboral = DateTime.Today; p.MotivoRetiro = motivo;
            _postulaciones.Actualizar(p);
        }
        public void SolicitarRevision(int id, string solicitud)
        {
            Postulacion p = PostulacionPropia(id);
            if (p.Estado != EstadoPostulacion.Finalizada) throw new ReglaDeNegocioException("Solo se revisan vinculaciones finalizadas.");
            Validar.Requerido(solicitud, "Solicitud"); if (solicitud.Length > 1000) throw new ReglaDeNegocioException("Máximo 1000 caracteres.");
            p.SolicitudRevision = solicitud.Trim(); p.RespuestaRevision = ""; _postulaciones.Actualizar(p);
        }
        public void ResolverRevision(int id, string motivo, bool modificar)
        {
            int usuario = Exigir(RolUsuario.Emprendedor); Postulacion p = ObtenerPostulacion(id);
            if (p.EmprendedorUsuarioId != usuario) throw new ReglaDeNegocioException("Sin autorización.");
            if (p.Estado != EstadoPostulacion.Finalizada || p.SolicitudRevision == "" || p.RespuestaRevision != "") throw new ReglaDeNegocioException("No hay una solicitud pendiente.");
            if (modificar) { if (!MotivosRetiro.EsValido(motivo)) throw new ReglaDeNegocioException("Motivo inválido."); p.MotivoRetiro = motivo; }
            p.RespuestaRevision = modificar ? "Motivo corregido" : "Motivo mantenido"; _postulaciones.Actualizar(p);
        }
        public List<Postulacion> ListarHistorialEmprendedor()
        {
            int id = Exigir(RolUsuario.Emprendedor);
            return _postulaciones.Listar().FindAll(p => p.EmprendedorUsuarioId == id && p.Estado == EstadoPostulacion.Finalizada);
        }
        public List<Postulacion> ListarMisPostulaciones() { int id = Exigir(RolUsuario.Trabajador); return _postulaciones.Listar().FindAll(p => p.TrabajadorUsuarioId == id); }
        public List<Postulacion> ListarPostulacionesEmpresa(int empresaId) { Emprendimiento e = AutorizarEmpresa(empresaId); return _postulaciones.Listar().FindAll(p => p.EmprendimientoId == empresaId && p.EmprendedorUsuarioId == Sesion.IdUsuario && p.ClaveEmpresa == e.ClaveLaboral); }
    }
}
