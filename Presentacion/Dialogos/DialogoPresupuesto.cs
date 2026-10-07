using System;
using System.Globalization;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>Formulario para crear un presupuesto mensual de una categoria de gasto.</summary>
    public class DialogoPresupuesto : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly int _emprendimientoId;
        private readonly ComboBox _categoria;
        private readonly NumericUpDown _limite;
        private readonly DateTimePicker _mes;

        public DialogoPresupuesto(FabricaServicios fabrica, int emprendimientoId, DateTime mesInicial)
            : base("Nuevo presupuesto")
        {
            _fabrica = fabrica;
            _emprendimientoId = emprendimientoId;

            _categoria = Campo("Categoría de gasto *", new ComboBox());
            _categoria.DropDownStyle = ComboBoxStyle.DropDownList;
            _categoria.Items.AddRange(Categorias.Gastos);
            _categoria.SelectedIndex = 0;

            _limite = Campo("Límite mensual *", Tema.Numero(1000000000000m, 0));

            _mes = Campo("Mes", new DateTimePicker());
            _mes.Format = DateTimePickerFormat.Custom;
            _mes.CustomFormat = "yyyy-MM";
            _mes.ShowUpDown = true;
            _mes.Value = mesInicial;
        }

        protected override void Aceptar()
        {
            string categoria = _categoria.SelectedItem != null ? _categoria.SelectedItem.ToString()! : "";
            string mes = _mes.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            _fabrica.Presupuestos.Crear(_emprendimientoId, categoria, _limite.Value, mes);
        }
    }
}