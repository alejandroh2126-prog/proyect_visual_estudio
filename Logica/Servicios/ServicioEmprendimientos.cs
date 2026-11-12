using System;
using System.Collections.Generic;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: CRUD de emprendimientos del usuario que inicio sesion.
    /// Al eliminar un emprendimiento se borran tambien sus datos (cascada),
    /// algo que en la version de Java quedaba huerfano en los archivos.
    /// </summary>
    public class ServicioEmprendimientos
    {
        private readonly RepositorioEmprendimientos _emprendimientos;
        private readonly RepositorioCargos _cargos;
        private readonly RepositorioTrabajadores _trabajadores;
        private readonly RepositorioProductos _productos;
        private readonly RepositorioVentas _ventas;
        private readonly RepositorioMovimientos _movimientos;
        private readonly RepositorioPresupuestos _presupuestos;
        private readonly ServicioFichaTrabajador _fichas;
        private readonly ServicioEmpleos _empleos;

        public ServicioEmprendimientos(RepositorioEmprendimientos emprendimientos, RepositorioCargos cargos,
            RepositorioTrabajadores trabajadores, RepositorioProductos productos,
            RepositorioVentas ventas, RepositorioMovimientos movimientos,
            RepositorioPresupuestos presupuestos, ServicioFichaTrabajador fichas, ServicioEmpleos empleos)
        {
            _emprendimientos = emprendimientos;
            _cargos = cargos;
            _trabajadores = trabajadores;
            _productos = productos;
            _ventas = ventas;
            _movimientos = movimientos;
            _presupuestos = presupuestos; _fichas = fichas; _empleos = empleos;
        }

        public List<Emprendimiento> ListarDelUsuario()
        {
            int usuarioId = Sesion.IdUsuario;
            return _emprendimientos.Listar().FindAll(e => e.UsuarioId == usuarioId);
        }

        public Emprendimiento Crear(string nombre, string descripcion, string sector, DateTime fechaInicio)
        {
            if (Sesion.UsuarioActual?.Rol != RolUsuario.Emprendedor) throw new ReglaDeNegocioException("Solo un emprendedor puede crear un negocio.");
            Validar.Requerido(nombre, "Nombre");
            Emprendimiento e = new Emprendimiento
            {
                UsuarioId = Sesion.IdUsuario,
                ClaveLaboral = Guid.NewGuid().ToString("N"),
                Nombre = nombre.Trim(),
                Descripcion = (descripcion ?? string.Empty).Trim(),
                Sector = (sector ?? string.Empty).Trim(),
                FechaInicio = fechaInicio,
                Estado = Estados.Activo
            };
            return _emprendimientos.Insertar(e);
        }

        public void Actualizar(Emprendimiento e)
        {
            Validar.Requerido(e.Nombre, "Nombre");
            Emprendimiento? actual = _emprendimientos.ObtenerPorId(e.Id);
            if (actual == null || actual.UsuarioId != Sesion.IdUsuario)
                throw new ReglaDeNegocioException("Emprendimiento no encontrado.");
            e.UsuarioId = actual.UsuarioId;
            e.ClaveLaboral = actual.ClaveLaboral;
            _emprendimientos.Actualizar(e);
        }

        public void Eliminar(int id)
        {
            Emprendimiento? actual = _emprendimientos.ObtenerPorId(id);
            if (actual == null || actual.UsuarioId != Sesion.IdUsuario)
                throw new ReglaDeNegocioException("Emprendimiento no encontrado.");

            _empleos.CerrarVacantesEmpresa(id);
            foreach (Trabajador t in _trabajadores.Listar().FindAll(t => t.EmprendimientoId == id)) _fichas.EliminarFicha(t.Id);
            _trabajadores.EliminarDonde(t => t.EmprendimientoId == id);
            _cargos.EliminarDonde(c => c.EmprendimientoId == id);
            _productos.EliminarDonde(p => p.EmprendimientoId == id);
            _ventas.EliminarDonde(v => v.EmprendimientoId == id);
            _movimientos.EliminarDonde(m => m.EmprendimientoId == id);
            _presupuestos.EliminarDonde(p => p.EmprendimientoId == id);
            _emprendimientos.Eliminar(id);
        }
    }
}