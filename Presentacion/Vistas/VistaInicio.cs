using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Servicios;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.Vistas
{
    /// <summary>Pantalla de bienvenida: resumen rapido del emprendimiento seleccionado.</summary>
    public class VistaInicio : VistaBase
    {
        private readonly TarjetaKpi _kEmprendimientos = new TarjetaKpi("Emprendimientos", Tema.VerdeOscuro);
        private readonly TarjetaKpi _kTrabajadores = new TarjetaKpi("Trabajadores activos", Tema.Verde);
        private readonly TarjetaKpi _kProductos = new TarjetaKpi("Productos en inventario", Tema.Gris);
        private readonly TarjetaKpi _kVentasMes = new TarjetaKpi("Ventas del mes", Tema.Dorado);
        private readonly TarjetaKpi _kBalance = new TarjetaKpi("Balance", Tema.VerdeOscuro);
        private readonly Label _titulo = Tema.TituloVista("Resumen");
        private readonly DataGridView _tablaAlertas = Tema.Tabla("Producto con stock bajo", "Stock", "Mínimo");
        private readonly DataGridView _tablaMovimientos = Tema.Tabla("Fecha", "Tipo", "Descripción", "Monto");

        public VistaInicio(FabricaServicios fabrica) : base(fabrica)
        {
            _tablaAlertas.AlinearDerecha(1, 2);
            _tablaMovimientos.AlinearDerecha(3);

            TableLayoutPanel abajo = new TableLayoutPanel();
            abajo.ColumnCount = 2;
            abajo.RowCount = 1;
            abajo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            abajo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            abajo.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _tablaAlertas.Margin = new Padding(0, 0, 12, 0);
            _tablaMovimientos.Margin = new Padding(0);
            _tablaAlertas.Dock = DockStyle.Fill;
            _tablaMovimientos.Dock = DockStyle.Fill;
            abajo.Controls.Add(_tablaAlertas, 0, 0);
            abajo.Controls.Add(_tablaMovimientos, 1, 0);

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(_titulo, false);
            raiz.Fila(Tema.FilaTarjetas(_kEmprendimientos, _kTrabajadores, _kProductos, _kVentasMes, _kBalance), false);
            raiz.Fila(abajo, true);
            Controls.Add(raiz);
        }

        protected override void Recargar()
        {
            _tablaAlertas.Rows.Clear();
            _tablaMovimientos.Rows.Clear();

            Mensajes.Ejecutar(delegate
            {
                string nombre = Sesion.UsuarioActual != null ? Sesion.UsuarioActual.Nombre : "";
                _titulo.Text = "Hola, " + nombre;
                _kEmprendimientos.Valor = Fabrica.Emprendimientos.ListarDelUsuario().Count.ToString();

                if (Emprendimiento == null)
                {
                    _kTrabajadores.Valor = "0";
                    _kProductos.Valor = "0";
                    _kVentasMes.Valor = Formato.Moneda(0);
                    _kBalance.Valor = Formato.Moneda(0);
                    return;
                }

                int id = Emprendimiento!.Id;
                _kTrabajadores.Valor = Fabrica.Nomina.CalcularResumen(id).TrabajadoresActivos.ToString();
                _kProductos.Valor = Fabrica.Inventario.Listar(id).Count.ToString();
                _kVentasMes.Valor = Formato.Moneda(Fabrica.Reportes.CalcularResumenVentas(id).EsteMes);
                _kBalance.Valor = Formato.Moneda(Fabrica.Finanzas.CalcularResumen(id).Balance);

                foreach (Producto p in Fabrica.Inventario.ListarStockBajo(id))
                {
                    DataGridViewRow fila = _tablaAlertas.AgregarFila(p, p.Nombre, p.Stock, p.StockMinimo);
                    fila.DefaultCellStyle.ForeColor = Tema.Rojo;
                }

                List<Movimiento> movimientos = Fabrica.Finanzas.Listar(id);
                for (int i = 0; i < movimientos.Count && i < 10; i++)
                {
                    Movimiento m = movimientos[i];
                    DataGridViewRow fila = _tablaMovimientos.AgregarFila(m, Formato.FechaHora(m.Fecha),
                        Textos.Tipo(m.Tipo), m.Descripcion, Formato.Moneda(m.Monto));
                    fila.DefaultCellStyle.ForeColor = m.EsIngreso ? Tema.VerdeOscuro : (m.EsGasto ? Tema.Rojo : Tema.Texto);
                }
            });
        }
    }
}