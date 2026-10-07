using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Servicios;
using Logica.Utilidades;
using Presentacion.Comun;
using Presentacion.Dialogos;

namespace Presentacion.Vistas
{
    /// <summary>Historial de movimientos, ingresos/gastos manuales y balance.</summary>
    public class VistaFinanzas : VistaBase
    {
        private readonly TarjetaKpi _kIngresos = new TarjetaKpi("Ingresos", Tema.Verde);
        private readonly TarjetaKpi _kGastos = new TarjetaKpi("Gastos", Tema.Rojo);
        private readonly TarjetaKpi _kBalance = new TarjetaKpi("Balance", Tema.VerdeOscuro);
        private readonly DataGridView _tabla = Tema.Tabla("Fecha", "Tipo", "Categoría", "Descripción", "Monto");
        private readonly ComboBox _filtro = new ComboBox();

        public VistaFinanzas(FabricaServicios fabrica) : base(fabrica)
        {
            _tabla.AlinearDerecha(4);

            Button ingreso = Tema.Boton("+ Nuevo ingreso", Tema.Verde);
            Button gasto = Tema.Boton("- Nuevo gasto", Tema.Rojo);
            Button eliminar = Tema.Boton("Eliminar (manual)", Tema.Gris);
            ingreso.Click += delegate { Nuevo(true); };
            gasto.Click += delegate { Nuevo(false); };
            eliminar.Click += delegate { Eliminar(); };

            _filtro.DropDownStyle = ComboBoxStyle.DropDownList;
            _filtro.Items.AddRange(new object[] { "Todos", "Solo ingresos", "Solo gastos y compras" });
            _filtro.SelectedIndex = 0;
            _filtro.Margin = new System.Windows.Forms.Padding(12, 4, 0, 0);
            _filtro.SelectedIndexChanged += delegate { Recargar(); };

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Finanzas"), false);
            raiz.Fila(Tema.FilaTarjetas(_kIngresos, _kGastos, _kBalance), false);
            raiz.Fila(Tema.Barra(ingreso, gasto, eliminar, Tema.Etiqueta("   Ver:"), _filtro), false);
            raiz.Fila(_tabla, true);
            Controls.Add(raiz);
        }

        protected override void Recargar()
        {
            _tabla.Rows.Clear();
            _kIngresos.Valor = Formato.Moneda(0);
            _kGastos.Valor = Formato.Moneda(0);
            _kBalance.Valor = Formato.Moneda(0);
            if (Emprendimiento == null) return;

            int id = Emprendimiento.Id;
            int filtro = _filtro.SelectedIndex;
            Mensajes.Ejecutar(delegate
            {
                ResumenFinanciero r = Fabrica.Finanzas.CalcularResumen(id);
                _kIngresos.Valor = Formato.Moneda(r.TotalIngresos);
                _kGastos.Valor = Formato.Moneda(r.TotalGastos);
                _kBalance.Valor = Formato.Moneda(r.Balance);

                foreach (Movimiento m in Fabrica.Finanzas.Listar(id))
                {
                    if (filtro == 1 && !m.EsIngreso) continue;
                    if (filtro == 2 && !m.EsGasto) continue;
                    DataGridViewRow fila = _tabla.AgregarFila(m, Formato.FechaHora(m.Fecha), Textos.Tipo(m.Tipo),
                        m.Categoria, m.Descripcion, Formato.Moneda(m.Monto));
                    fila.DefaultCellStyle.ForeColor = m.EsIngreso ? Tema.VerdeOscuro : (m.EsGasto ? Tema.Rojo : Tema.Texto);
                }
            });
        }

        private void Nuevo(bool esIngreso)
        {
            if (!ExigirEmprendimiento()) return;
            using (DialogoMovimiento d = new DialogoMovimiento(Fabrica, Emprendimiento!.Id, esIngreso))
            {
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    Recargar();
                    if (!esIngreso) AvisarPresupuestos();
                }
            }
        }

        /// <summary>Despues de registrar un gasto, avisa si algun presupuesto del mes quedo excedido.</summary>
        private void AvisarPresupuestos()
        {
            int id = Emprendimiento!.Id;
            Mensajes.Ejecutar(delegate
            {
                foreach (EstadoPresupuesto e in Fabrica.Presupuestos.ListarExcedidos(id, ServicioPresupuestos.MesActual()))
                {
                    Mensajes.Info("¡Presupuesto excedido en '" + e.Presupuesto.Categoria + "'!\nLímite: " +
                        Formato.Moneda(e.Presupuesto.MontoLimite) + "  |  Gastado: " + Formato.Moneda(e.Gastado));
                }
            });
        }

        private void Eliminar()
        {
            Movimiento? m = _tabla.Seleccionado<Movimiento>();
            if (m == null) { Mensajes.Info("Selecciona un movimiento de la lista."); return; }
            if (!Mensajes.Confirmar("¿Eliminar '" + m.Descripcion + "'?")) return;
            if (Mensajes.Ejecutar(delegate { Fabrica.Finanzas.EliminarManual(m.Id); })) Recargar();
        }
    }
}