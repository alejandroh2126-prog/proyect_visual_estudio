using System;
using System.Collections.Generic;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>UNA SOLA MISION: productos y control de stock (con alertas de stock bajo).</summary>
    public class ServicioInventario
    {
        private readonly RepositorioProductos _productos;
        private readonly ServicioFinanzas _finanzas;

        public ServicioInventario(RepositorioProductos productos, ServicioFinanzas finanzas)
        {
            _productos = productos;
            _finanzas = finanzas;
        }

        public List<Producto> Listar(int emprendimientoId)
        {
            return _productos.Listar().FindAll(p => p.EmprendimientoId == emprendimientoId);
        }

        public List<Producto> ListarStockBajo(int emprendimientoId)
        {
            return Listar(emprendimientoId).FindAll(p => p.TieneStockBajo);
        }

        /// <summary>
        /// Crea o edita un producto. Al editar, el stock NO cambia aqui
        /// (solo cambia con EntradaStock / SalidaManual, para que quede historial).
        /// </summary>
        public Producto Guardar(Producto p)
        {
            Validar.Requerido(p.Nombre, "Nombre");
            Validar.NoNegativo(p.PrecioCompra, "Precio de compra");
            Validar.MayorQueCero(p.PrecioVenta, "Precio de venta");
            Validar.NoNegativo(p.StockMinimo, "Stock mínimo");

            if (p.Id == 0) return _productos.Insertar(p);

            Producto? actual = _productos.ObtenerPorId(p.Id);
            if (actual == null) throw new ReglaDeNegocioException("Producto no encontrado.");
            p.EstablecerStock(actual.Stock);
            _productos.Actualizar(p);
            return p;
        }

        public void Eliminar(int productoId)
        {
            _productos.Eliminar(productoId);
        }

        public Producto EntradaStock(int productoId, int cantidad)
        {
            Producto p = Obtener(productoId);
            Validar.MayorQueCero(cantidad, "Cantidad");
            p.AgregarStock(cantidad);
            _productos.Actualizar(p);
            _finanzas.Registrar(p.EmprendimientoId, TipoMovimiento.Compra, Categorias.Inventario,
                "Entrada de stock: " + p.Nombre + " +" + cantidad,
                p.PrecioCompra * cantidad, "producto-" + p.Id);
            return p;
        }

        public Producto SalidaManual(int productoId, int cantidad)
        {
            Producto p = Obtener(productoId);
            Validar.MayorQueCero(cantidad, "Cantidad");
            if (!p.HayStock(cantidad))
                throw new ReglaDeNegocioException("Stock insuficiente. Disponible: " + p.Stock);
            p.ReducirStock(cantidad);
            _productos.Actualizar(p);
            _finanzas.Registrar(p.EmprendimientoId, TipoMovimiento.AjusteStock, Categorias.Inventario,
                "Salida manual: " + p.Nombre + " -" + cantidad,
                p.PrecioVenta * cantidad, "producto-" + p.Id);
            return p;
        }

        private Producto Obtener(int productoId)
        {
            Producto? p = _productos.ObtenerPorId(productoId);
            if (p == null) throw new ReglaDeNegocioException("Producto no encontrado.");
            return p;
        }
    }
}