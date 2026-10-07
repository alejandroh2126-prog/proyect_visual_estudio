using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>Formulario para crear o editar un emprendimiento.</summary>
    public class DialogoEmprendimiento : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly Emprendimiento? _editar;
        private readonly TextBox _nombre;
        private readonly TextBox _descripcion;
        private readonly ComboBox _sector;
        private readonly DateTimePicker _fecha;
        private readonly ComboBox? _estado;

        public DialogoEmprendimiento(FabricaServicios fabrica, Emprendimiento? editar)
            : base(editar == null ? "Nuevo emprendimiento" : "Editar emprendimiento")
        {
            _fabrica = fabrica;
            _editar = editar;

            _nombre = Campo("Nombre *", new TextBox());
            _descripcion = Campo("Descripción", new TextBox());
            _descripcion.Multiline = true;
            _descripcion.Height = 70;

            _sector = Campo("Sector", new ComboBox());
            _sector.Items.AddRange(new object[] { "Alimentos", "Tecnología", "Moda", "Servicios", "Comercio",
                "Educación", "Salud y belleza", "Turismo", "Agricultura", "Otro" });

            _fecha = Campo("Fecha de inicio", new DateTimePicker());
            _fecha.Format = DateTimePickerFormat.Short;

            if (editar != null)
            {
                _estado = Campo("Estado", new ComboBox());
                _estado.DropDownStyle = ComboBoxStyle.DropDownList;
                _estado.Items.AddRange(new object[] { Estados.Activo, Estados.Inactivo });
                _estado.SelectedItem = editar.Estado;

                _nombre.Text = editar.Nombre;
                _descripcion.Text = editar.Descripcion;
                _sector.Text = editar.Sector;
                _fecha.Value = editar.FechaInicio < _fecha.MinDate ? _fecha.MinDate : editar.FechaInicio;
            }
        }

        protected override void Aceptar()
        {
            if (_editar == null)
            {
                _fabrica.Emprendimientos.Crear(_nombre.Text, _descripcion.Text, _sector.Text, _fecha.Value.Date);
                return;
            }

            _editar.Nombre = _nombre.Text.Trim();
            _editar.Descripcion = _descripcion.Text.Trim();
            _editar.Sector = _sector.Text.Trim();
            _editar.FechaInicio = _fecha.Value.Date;
            if (_estado != null && _estado.SelectedItem != null) _editar.Estado = _estado.SelectedItem.ToString()!;
            _fabrica.Emprendimientos.Actualizar(_editar);
        }
    }
}