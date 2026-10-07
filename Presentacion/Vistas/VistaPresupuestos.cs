using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Servicios;
using Logica.Utilidades;
using Presentacion.Comun;
using Presentacion.Dialogos;

namespace Presentacion.Vistas
{
    /// <summary>Presupuestos mensuales por categoria: limite, gastado y alertas.</summary>
    public class VistaPresupuestos : VistaBase
    {
        private readonly DateTimePicker _mes = new DateTimePicker();
        private readonly DataGridView _tabla = Tema.Tabla("Categoría", "Límite", "Gastado", "Disponible", "Uso", "Estado");

        public VistaPresupuestos(FabricaServicios fabrica) : base(fabrica)
        {
            _tabla.AlinearDerecha(1, 2, 3, 4);

            _mes.Format = DateTimePickerFormat.Custom;
            _mes.CustomFormat = "yyyy-MM";
            _mes.ShowUpDown = true;
            _mes.Width = 110;
            _mes.Margin = new Padding(0, 4, 16, 0);
            _mes.ValueChanged += delegate { Recargar(); };

            Button nuevo = Tema.Boton("Nuevo presupuesto", Tema.Verde);
            Button eliminar = Tema.Boton("Eliminar", Tema.Rojo);
            nuevo.Click += delegate { Nuevo(); };
            eliminar.Click += delegate { Eliminar(); };

            Label nota = Tema.Etiqueta("Se cuentan los gastos y compras de la categoría en el mes elegido. Alerta desde el 80% de uso.");
            nota.ForeColor = Tema.TextoSuave;

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Presupuestos"), false);
            raiz.Fila(Tema.Barra(Tema.Etiqueta("Mes:"), _mes, nuevo, eliminar), false);
            raiz.Fila(nota, false);
            raiz.Fila(_tabla, true);
            Controls.Add(raiz);
        }

        private string MesElegido
        {
            get { return _mes.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture); }
        }

        protected override void Recargar()
        {
            _tabla.Rows.Clear();
            if (Emprendimiento == null) return;

            int id = Emprendimiento.Id;
            string mes = MesElegido;
            Mensajes.Ejecutar(delegate
            {
                foreach (EstadoPresupuesto e in Fabrica.Presupuestos.Listar(id, mes))
                {
                    string estado = e.Excedido ? "Excedido" : (e.EnAlerta ? "En alerta" : "Dentro del presupuesto");
                    DataGridViewRow fila = _tabla.AgregarFila(e.Presupuesto, e.Presupuesto.Categoria,
                        Formato.Moneda(e.Presupuesto.MontoLimite), Formato.Moneda(e.Gastado),
                        Formato.Moneda(e.Disponible), e.Porcentaje.ToString("0") + "%", estado);
                    if (e.Excedido)
                    {
                        fila.DefaultCellStyle.ForeColor = Tema.Rojo;
                        fila.DefaultCellStyle.BackColor = Tema.RojoClaro;
                    }
                    else if (e.EnAlerta)
                    {
                        fila.DefaultCellStyle.ForeColor = Tema.Dorado;
                    }
                }
            });
        }

        private void Nuevo()
        {
            if (!ExigirEmprendimiento()) return;
            using (DialogoPresupuesto d = new DialogoPresupuesto(Fabrica, Emprendimiento!.Id, _mes.Value))
            {
                if (d.ShowDialog(this) == DialogResult.OK) Recargar();
            }
        }

        private void Eliminar()
        {
            Presupuesto? p = _tabla.Seleccionado<Presupuesto>();
            if (p == null) { Mensajes.Info("Selecciona un presupuesto de la lista."); return; }
            if (!Mensajes.Confirmar("¿Eliminar el presupuesto de '" + p.Categoria + "'?")) return;
            if (Mensajes.Ejecutar(delegate { Fabrica.Presupuestos.Eliminar(p.Id); })) Recargar();
        }
    }
}