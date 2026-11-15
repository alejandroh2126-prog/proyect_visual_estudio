using System;
using System.Collections.Generic;
using Entidades;
using Datos;
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
        private readonly IEmprendimientoDao _emprendimientos;
        private readonly ICargoDao _cargos;
        private readonly ITrabajadorDao _trabajadores;
        private readonly IProductoDao _productos;
        private readonly IVentaDao _ventas;
        private readonly IMovimientoDao _movimientos;
        private readonly IPresupuestoDao _presupuestos;

        public ServicioEmprendimientos(IEmprendimientoDao emprendimientos, ICargoDao cargos,
            ITrabajadorDao trabajadores, IProductoDao productos,
            IVentaDao ventas, IMovimientoDao movimientos,
            IPresupuestoDao presupuestos)
        {
            _emprendimientos = emprendimientos;
            _cargos = cargos;
            _trabajadores = trabajadores;
            _productos = productos;
            _ventas = ventas;
            _movimientos = movimientos;
            _presupuestos = presupuestos;
        }

        public List<Emprendimiento> ListarDelUsuario()
        {
            int usuarioId = Sesion.IdUsuario;
            return _emprendimientos.Listar().FindAll(e => e.UsuarioId == usuarioId);
        }

        public Emprendimiento Crear(string nombre, string descripcion, string sector, DateTime fechaInicio)
        {
            Validar.Requerido(nombre, "Nombre");
            Emprendimiento e = new Emprendimiento
            {
                UsuarioId = Sesion.IdUsuario,
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
            _emprendimientos.Actualizar(e);
        }

        public void Eliminar(int id)
        {
            Emprendimiento? actual = _emprendimientos.ObtenerPorId(id);
            if (actual == null || actual.UsuarioId != Sesion.IdUsuario)
                throw new ReglaDeNegocioException("Emprendimiento no encontrado.");

            _trabajadores.EliminarPorEmprendimiento(id);
            _cargos.EliminarPorEmprendimiento(id);
            _productos.EliminarPorEmprendimiento(id);
            _ventas.EliminarPorEmprendimiento(id);
            _movimientos.EliminarPorEmprendimiento(id);
            _presupuestos.EliminarPorEmprendimiento(id);
            _emprendimientos.Eliminar(id);
        }
    }
}