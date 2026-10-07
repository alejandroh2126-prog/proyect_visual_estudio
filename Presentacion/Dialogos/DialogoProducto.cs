using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>Formulario para crear o editar un producto. El stock solo se escribe al crear.</summary>
    public class DialogoProducto : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly int _emprendimientoId;
        private readonly Producto? _editar;
        private readonly TextBox _nombre;
        private readonly TextBox _categoria;
        private readonly NumericUpDown _compra;
        private readonly NumericUpDown _venta;
        private readonly NumericUpDown _minimo;
        private readonly NumericUpDown? _stockInicial;

        public DialogoProducto(FabricaServicios fabrica, int emprendimientoId, Producto? editar)
            : base(editar == null ? "Nuevo producto" : "Editar producto")
        {
            _fabrica = fabrica;
            _emprendimientoId = emprendimientoId;
            _editar = editar;

            _nombre = Campo("Nombre *", new TextBox());
            _categoria = Campo("Categoría", new TextBox());
            _compra = Campo("Precio de compra", Tema.Numero(1000000000m, 0));
            _venta = Campo("Precio de venta *", Tema.Numero(1000000000m, 0));
            _minimo = Campo("Stock mínimo (alerta)", Tema.Numero(1000000m, 0));

            if (editar == null)
            {
                _stockInicial = Campo("Stock inicial", Tema.Numero(1000000m, 0));
            }
            else
            {
                _nombre.Text = editar.Nombre;
                _categoria.Text = editar.Categoria;
                _compra.Value = editar.PrecioCompra;
                _venta.Value = editar.PrecioVenta;
                _minimo.Value = editar.StockMinimo;
            }
        }

        protected override void Aceptar()
        {
            Producto p = _editar ?? new Producto { EmprendimientoId = _emprendimientoId };
            p.Nombre = _nombre.Text.Trim();
            p.Categoria = _categoria.Text.Trim();
            p.PrecioCompra = _compra.Value;
            p.PrecioVenta = _venta.Value;
            p.StockMinimo = (int)_minimo.Value;
            if (_editar == null && _stockInicial != null) p.EstablecerStock((int)_stockInicial.Value);
            _fabrica.Inventario.Guardar(p);
        }
    }
}