using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>Formulario para registrar o editar un trabajador.</summary>
    public class DialogoTrabajador : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly int _emprendimientoId;
        private readonly Trabajador? _editar;
        private readonly TextBox _nombre;
        private readonly TextBox _apellido;
        private readonly TextBox _cedula;
        private readonly ComboBox _cargo;
        private readonly DateTimePicker _fecha;
        private readonly ComboBox? _estado;

        public DialogoTrabajador(FabricaServicios fabrica, int emprendimientoId, List<Cargo> cargos, Trabajador? editar)
            : base(editar == null ? "Nuevo trabajador" : "Editar trabajador")
        {
            _fabrica = fabrica;
            _emprendimientoId = emprendimientoId;
            _editar = editar;

            _nombre = Campo("Nombre *", new TextBox());
            _apellido = Campo("Apellido *", new TextBox());
            _cedula = Campo("Cédula *", new TextBox());

            _cargo = Campo("Cargo *", new ComboBox());
            _cargo.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (Cargo c in cargos) _cargo.Items.Add(c);

            _fecha = Campo("Fecha de ingreso", new DateTimePicker());
            _fecha.Format = DateTimePickerFormat.Short;

            if (editar != null)
            {
                _estado = Campo("Estado", new ComboBox());
                _estado.DropDownStyle = ComboBoxStyle.DropDownList;
                _estado.Items.AddRange(new object[] { Estados.Activo, Estados.Inactivo });
                _estado.SelectedItem = editar.Estado;

                _nombre.Text = editar.Nombre;
                _apellido.Text = editar.Apellido;
                _cedula.Text = editar.Cedula;
                _fecha.Value = editar.FechaIngreso < _fecha.MinDate ? _fecha.MinDate : editar.FechaIngreso;
                foreach (object item in _cargo.Items)
                {
                    if (((Cargo)item).Id == editar.Cargo.Id) _cargo.SelectedItem = item;
                }
            }
            else if (_cargo.Items.Count > 0)
            {
                _cargo.SelectedIndex = 0;
            }
        }

        protected override void Aceptar()
        {
            Cargo? cargo = _cargo.SelectedItem as Cargo;
            if (cargo == null)
                throw new Logica.Utilidades.ReglaDeNegocioException("Selecciona un cargo.");

            if (_editar == null)
            {
                Trabajador nuevo = new Trabajador(0, _nombre.Text, _apellido.Text, _cedula.Text,
                    cargo, _fecha.Value.Date, _emprendimientoId);
                _fabrica.Nomina.GuardarTrabajador(nuevo);
                return;
            }

            _editar.Nombre = _nombre.Text;
            _editar.Apellido = _apellido.Text;
            _editar.Cedula = _cedula.Text;
            _editar.Cargo = cargo;
            _editar.FechaIngreso = _fecha.Value.Date;
            if (_estado != null && _estado.SelectedItem != null) _editar.Estado = _estado.SelectedItem.ToString()!;
            _fabrica.Nomina.GuardarTrabajador(_editar);
        }
    }
}