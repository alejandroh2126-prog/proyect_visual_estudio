using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>Formulario para registrar un ingreso o un gasto manual (con su categoria).</summary>
    public class DialogoMovimiento : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly int _emprendimientoId;
        private readonly bool _esIngreso;
        private readonly ComboBox _categoria;
        private readonly TextBox _descripcion;
        private readonly NumericUpDown _monto;

        public DialogoMovimiento(FabricaServicios fabrica, int emprendimientoId, bool esIngreso)
            : base(esIngreso ? "Nuevo ingreso" : "Nuevo gasto")
        {
            _fabrica = fabrica;
            _emprendimientoId = emprendimientoId;
            _esIngreso = esIngreso;

            _categoria = Campo("Categoría *", new ComboBox());
            _categoria.DropDownStyle = ComboBoxStyle.DropDownList;
            _categoria.Items.AddRange(esIngreso ? Categorias.Ingresos : Categorias.Gastos);
            _categoria.SelectedIndex = 0;

            _descripcion = Campo("Descripción *", new TextBox());
            _monto = Campo("Monto *", Tema.Numero(1000000000000m, 0));
        }

        protected override void Aceptar()
        {
            string categoria = _categoria.SelectedItem != null ? _categoria.SelectedItem.ToString()! : "";
            _fabrica.Finanzas.RegistrarManual(_emprendimientoId, _esIngreso, categoria, _descripcion.Text, _monto.Value);
        }
    }
}