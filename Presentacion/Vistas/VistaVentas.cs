using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.Vistas
{
    /// <summary>Registrar ventas (carrito), generar facturas y ver el historial.</summary>
    public class VistaVentas : VistaBase
    {
        /// <summary>Elemento del ComboBox: muestra nombre, stock y precio, pero guarda el Producto.</summary>
        private class ItemProducto
        {
            public Producto Producto { get; }
            public ItemProducto(Producto producto) { Producto = producto; }
            public override string ToString()
            {
                return Producto.Nombre + "  |  stock " + Producto.Stock + "  |  " + Formato.Moneda(Producto.PrecioVenta);
            }
        }

        private readonly List<DetalleVenta> _carrito = new List<DetalleVenta>();
        private readonly ComboBox _productos = new ComboBox();
        private readonly NumericUpDown _cantidad = Tema.Numero(1000000m, 0);
        private readonly DataGridView _tablaCarrito = Tema.Tabla("Producto", "Cantidad", "Precio", "Subtotal");
        private readonly TextBox _observacion = new TextBox();
        private readonly Label _total = new Label();
        private readonly DataGridView _tablaHistorial = Tema.Tabla("N.º", "Fecha", "Productos", "Total", "Observación");

        public VistaVentas(FabricaServicios fabrica) : base(fabrica)
        {
            _tablaCarrito.AlinearDerecha(1, 2, 3);
            _tablaHistorial.AlinearDerecha(2, 3);
            _productos.DropDownStyle = ComboBoxStyle.DropDownList;
            _productos.Width = 380;
            _cantidad.Minimum = 1;
            _cantidad.Value = 1;
            _cantidad.Width = 90;
            _observacion.Width = 320;
            _total.Font = Tema.Grande;
            _total.ForeColor = Tema.VerdeOscuro;
            _total.AutoSize = true;
            _total.Margin = new Padding(24, 0, 0, 0);

            // ---- pestana Nueva venta ----
            Button agregar = Tema.Boton("Agregar al carrito", Tema.Verde);
            agregar.Click += delegate { Agregar(); };
            FlowLayoutPanel selector = Tema.Barra(Tema.Etiqueta("Producto:"), _productos,
                Tema.Etiqueta("Cantidad:"), _cantidad, agregar);
            _productos.Margin = new Padding(0, 4, 16, 0);
            _cantidad.Margin = new Padding(0, 4, 16, 0);

            Button quitar = Tema.Boton("Quitar del carrito", Tema.Gris);
            Button vaciar = Tema.Boton("Vaciar", Tema.Gris);
            Button registrar = Tema.Boton("Registrar venta", Tema.Verde);
            quitar.Click += delegate { Quitar(); };
            vaciar.Click += delegate { _carrito.Clear(); PintarCarrito(); };
            registrar.Click += delegate { Registrar(); };
            _observacion.Margin = new Padding(0, 4, 16, 0);
            FlowLayoutPanel pie = Tema.Barra(Tema.Etiqueta("Observación (cliente, nota):"), _observacion,
                quitar, vaciar, registrar, _total);

            TableLayoutPanel panelVenta = Tema.ColumnaVertical();
            panelVenta.Fila(selector, false);
            panelVenta.Fila(_tablaCarrito, true);
            panelVenta.Fila(pie, false);
            TabPage paginaVenta = new TabPage("Nueva venta");
            paginaVenta.Padding = new Padding(12);
            paginaVenta.BackColor = Tema.Fondo;
            paginaVenta.Controls.Add(panelVenta);

            // ---- pestana Historial ----
            Button factura = Tema.Boton("Generar factura", Tema.VerdeOscuro);
            Button detalle = Tema.Boton("Ver detalle", Tema.Gris);
            factura.Click += delegate { GenerarFactura(); };
            detalle.Click += delegate { VerDetalle(); };
            TableLayoutPanel panelHistorial = Tema.ColumnaVertical();
            panelHistorial.Fila(Tema.Barra(factura, detalle), false);
            panelHistorial.Fila(_tablaHistorial, true);
            TabPage paginaHistorial = new TabPage("Historial de ventas");
            paginaHistorial.Padding = new Padding(12);
            paginaHistorial.BackColor = Tema.Fondo;
            paginaHistorial.Controls.Add(panelHistorial);

            TabControl pestanas = new TabControl();
            pestanas.Font = Tema.Normal;
            pestanas.TabPages.Add(paginaVenta);
            pestanas.TabPages.Add(paginaHistorial);

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Ventas"), false);
            raiz.Fila(pestanas, true);
            Controls.Add(raiz);
        }

        protected override void Recargar()
        {
            _carrito.Clear();
            _observacion.Text = "";
            CargarProductos();
            PintarCarrito();
            CargarHistorial();
        }

        private void CargarProductos()
        {
            _productos.Items.Clear();
            if (Emprendimiento == null) return;
            int id = Emprendimiento.Id;
            Mensajes.Ejecutar(delegate
            {
                foreach (Producto p in Fabrica.Inventario.Listar(id))
                {
                    if (p.Stock > 0) _productos.Items.Add(new ItemProducto(p));
                }
                if (_productos.Items.Count > 0) _productos.SelectedIndex = 0;
            });
        }

        private void CargarHistorial()
        {
            _tablaHistorial.Rows.Clear();
            if (Emprendimiento == null) return;
            int id = Emprendimiento.Id;
            Mensajes.Ejecutar(delegate
            {
                foreach (Venta v in Fabrica.Ventas.Listar(id))
                {
                    _tablaHistorial.AgregarFila(v, v.Id, Formato.FechaHora(v.Fecha), v.TotalProductos,
                        Formato.Moneda(v.Total), v.Observacion);
                }
            });
        }

        private void PintarCarrito()
        {
            _tablaCarrito.Rows.Clear();
            decimal total = 0m;
            foreach (DetalleVenta d in _carrito)
            {
                _tablaCarrito.AgregarFila(d, d.ProductoNombre, d.Cantidad, Formato.Moneda(d.PrecioUnitario), Formato.Moneda(d.Subtotal));
                total += d.Subtotal;
            }
            _total.Text = "Total: " + Formato.Moneda(total);
        }

        private void Agregar()
        {
            if (!ExigirEmprendimiento()) return;
            ItemProducto? item = _productos.SelectedItem as ItemProducto;
            if (item == null) { Mensajes.Info("No hay productos con stock para vender. Revisa el inventario."); return; }

            Producto p = item.Producto;
            int cantidad = (int)_cantidad.Value;
            int enCarrito = 0;
            foreach (DetalleVenta d in _carrito)
            {
                if (d.ProductoId == p.Id) enCarrito += d.Cantidad;
            }
            if (enCarrito + cantidad > p.Stock)
            {
                Mensajes.Error("Stock insuficiente de '" + p.Nombre + "'. Disponible: " + p.Stock + " (ya tienes " + enCarrito + " en el carrito).");
                return;
            }

            bool existe = false;
            foreach (DetalleVenta d in _carrito)
            {
                if (d.ProductoId == p.Id) { d.Cantidad += cantidad; existe = true; }
            }
            if (!existe)
            {
                _carrito.Add(new DetalleVenta { ProductoId = p.Id, ProductoNombre = p.Nombre, Cantidad = cantidad, PrecioUnitario = p.PrecioVenta });
            }
            PintarCarrito();
        }

        private void Quitar()
        {
            DetalleVenta? d = _tablaCarrito.Seleccionado<DetalleVenta>();
            if (d == null) { Mensajes.Info("Selecciona una línea del carrito."); return; }
            _carrito.Remove(d);
            PintarCarrito();
        }

        private void Registrar()
        {
            if (!ExigirEmprendimiento()) return;
            Venta? venta = null;
            Emprendimiento emp = Emprendimiento!;
            bool bien = Mensajes.Ejecutar(delegate
            {
                venta = Fabrica.Ventas.Registrar(emp.Id, _observacion.Text, new List<DetalleVenta>(_carrito));
            });
            if (!bien || venta == null) return;

            Recargar();
            string mensaje = "Venta #" + venta.Id + " registrada por " + Formato.Moneda(venta.Total) + ".\n\n¿Generar la factura?";
            if (Mensajes.Confirmar(mensaje)) CrearFactura(venta, emp);

            foreach (Producto p in Fabrica.Inventario.ListarStockBajo(emp.Id))
            {
                Mensajes.Info("Atención: '" + p.Nombre + "' tiene stock bajo (" + p.Stock + ", mínimo " + p.StockMinimo + ").");
            }
        }

        private void GenerarFactura()
        {
            if (!ExigirEmprendimiento()) return;
            Venta? v = _tablaHistorial.Seleccionado<Venta>();
            if (v == null) { Mensajes.Info("Selecciona una venta del historial."); return; }
            CrearFactura(v, Emprendimiento!);
        }

        private void CrearFactura(Venta venta, Emprendimiento emp)
        {
            string ruta = "";
            if (Mensajes.Ejecutar(delegate { ruta = Fabrica.Facturas.Generar(venta, emp); }))
            {
                Mensajes.Abrir(ruta);
            }
        }

        private void VerDetalle()
        {
            Venta? v = _tablaHistorial.Seleccionado<Venta>();
            if (v == null) { Mensajes.Info("Selecciona una venta del historial."); return; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("Venta #" + v.Id + "  -  " + Formato.FechaHora(v.Fecha));
            sb.AppendLine();
            foreach (DetalleVenta d in v.Detalles)
                sb.AppendLine(d.ProductoNombre + "  x" + d.Cantidad + "  @ " + Formato.Moneda(d.PrecioUnitario) + "  =  " + Formato.Moneda(d.Subtotal));
            sb.AppendLine();
            sb.AppendLine("TOTAL: " + Formato.Moneda(v.Total));
            Mensajes.Info(sb.ToString());
        }
    }
}