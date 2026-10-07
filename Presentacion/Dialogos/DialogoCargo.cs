using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>Formulario para crear o editar un cargo (con su salario base).</summary>
    public class DialogoCargo : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly int _emprendimientoId;
        private readonly Cargo? _editar;
        private readonly TextBox _nombre;
        private readonly NumericUpDown _salario;
        private readonly TextBox _descripcion;

        public DialogoCargo(FabricaServicios fabrica, int emprendimientoId, Cargo? editar)
            : base(editar == null ? "Nuevo cargo" : "Editar cargo")
        {
            _fabrica = fabrica;
            _emprendimientoId = emprendimientoId;
            _editar = editar;

            _nombre = Campo("Nombre del cargo *", new TextBox());
            _salario = Campo("Salario base mensual *", Tema.Numero(1000000000m, 0));
            _descripcion = Campo("Descripción", new TextBox());

            if (editar != null)
            {
                _nombre.Text = editar.Nombre;
                _salario.Value = editar.SalarioBase;
                _descripcion.Text = editar.Descripcion;
            }
        }

        protected override void Aceptar()
        {
            Cargo cargo = _editar ?? new Cargo { EmprendimientoId = _emprendimientoId };
            cargo.Nombre = _nombre.Text.Trim();
            cargo.SalarioBase = _salario.Value;
            cargo.Descripcion = _descripcion.Text.Trim();
            _fabrica.Nomina.GuardarCargo(cargo);
        }
    }
}