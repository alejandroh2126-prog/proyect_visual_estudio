using System;
using System.Windows.Forms;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>
    /// Pide una cantidad y ejecuta la accion que le entreguen (entrada o salida de stock).
    /// Asi el mismo formulario sirve para varias operaciones.
    /// </summary>
    public class DialogoCantidad : DialogoBase
    {
        private readonly Action<int> _accion;
        private readonly NumericUpDown _cantidad;

        public DialogoCantidad(string titulo, string detalle, Action<int> accion) : base(titulo)
        {
            _accion = accion;
            Label info = Tema.Etiqueta(detalle);
            Campo("Producto", info);
            _cantidad = Campo("Cantidad *", Tema.Numero(1000000m, 0));
            _cantidad.Value = 1;
        }

        protected override void Aceptar()
        {
            _accion((int)_cantidad.Value);
        }
    }
}