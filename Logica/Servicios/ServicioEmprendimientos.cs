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

        public ServicioEmprendimientos(RepositorioEmprendimientos emprendimientos, RepositorioCargos cargos,
            RepositorioTrabajadores trabajadores, RepositorioProductos productos,
            RepositorioVentas ventas, RepositorioMovimientos movimientos,
            RepositorioPresupuestos presupuestos)
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