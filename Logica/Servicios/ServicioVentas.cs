using System;
using System.Collections.Generic;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: registrar y consultar ventas.
    /// Primero VALIDA todo el carrito y recien despues descuenta stock y guarda,
    /// asi una venta nunca queda a medias.
    /// </summary>
    public class ServicioVentas
    {
        private readonly RepositorioVentas _ventas;
        private readonly RepositorioProductos _productos;
        private readonly ServicioFinanzas _finanzas;

        public ServicioVentas(RepositorioVentas ventas, RepositorioProductos productos, ServicioFinanzas finanzas)
        {
            _ventas = ventas;
            _productos = productos;
            _finanzas = finanzas;
        }

        public List<Venta> Listar(int emprendimientoId)
        {
            List<Venta> lista = _ventas.Listar().FindAll(v => v.EmprendimientoId == emprendimientoId);
            lista.Sort((a, b) => b.Fecha.CompareTo(a.Fecha));
            return lista;
        }

        public Venta Registrar(int emprendimientoId, string observacion, List<DetalleVenta> carrito)
        {
            if (carrito == null || carrito.Count == 0)
                throw new ReglaDeNegocioException("Agrega al menos un producto a la venta.");

            Dictionary<int, Producto> productos = new Dictionary<int, Producto>();
            foreach (Producto p in _productos.Listar())
            {
                if (p.EmprendimientoId == emprendimientoId) productos[p.Id] = p;
            }

            Venta venta = new Venta
            {
                EmprendimientoId = emprendimientoId,
                Fecha = DateTime.Now,
                Observacion = (observacion ?? string.Empty).Trim()
            };

            // 1) Validar y armar la venta (sin tocar archivos todavia).
            foreach (DetalleVenta item in carrito)
            {
                if (!productos.ContainsKey(item.ProductoId))
                    throw new ReglaDeNegocioException("Un producto de la venta ya no existe.");
                Validar.MayorQueCero(item.Cantidad, "Cantidad");

                Producto prod = productos[item.ProductoId];
                venta.AgregarDetalle(new DetalleVenta
                {
                    ProductoId = prod.Id,
                    ProductoNombre = prod.Nombre,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = prod.PrecioVenta
                });
            }

            foreach (DetalleVenta d in venta.Detalles)
            {
                Producto prod = productos[d.ProductoId];
                if (!prod.HayStock(d.Cantidad))
                    throw new ReglaDeNegocioException("Stock insuficiente de '" + prod.Nombre +
                                                      "'. Disponible: " + prod.Stock);
            }

            // 2) Aplicar cambios.
            foreach (DetalleVenta d in venta.Detalles)
            {
                Producto prod = productos[d.ProductoId];
                prod.ReducirStock(d.Cantidad);
                _productos.Actualizar(prod);
            }

            _ventas.Insertar(venta);
            _finanzas.Registrar(emprendimientoId, TipoMovimiento.Venta, Categorias.Ventas,
                "Venta #" + venta.Id, venta.Total, "venta-" + venta.Id);
            return venta;
        }
    }
}