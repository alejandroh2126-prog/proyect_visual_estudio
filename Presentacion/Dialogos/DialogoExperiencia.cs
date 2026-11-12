using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;
namespace Presentacion.Dialogos
{
    public class DialogoExperiencia : DialogoBase
    {
        private readonly FabricaServicios _fabrica; private readonly int _id;
        private readonly TextBox _empresa, _cargo; private readonly DateTimePicker _inicio, _fin; private readonly ComboBox _motivo;
        public DialogoExperiencia(FabricaServicios fabrica, ExperienciaLaboral? e) : base(e == null ? "Agregar experiencia anterior" : "Editar experiencia")
        {
            _fabrica = fabrica; _id = e?.Id ?? 0;
            _empresa = Campo("Empresa *", new TextBox { Text = e?.Empresa ?? "" }); _cargo = Campo("Cargo *", new TextBox { Text = e?.Cargo ?? "" });
            _inicio = Campo("Desde", new DateTimePicker { Format = DateTimePickerFormat.Short, Value = e?.Inicio ?? DateTime.Today }); _fin = Campo("Hasta", new DateTimePicker { Format = DateTimePickerFormat.Short, Value = e?.Fin ?? DateTime.Today });
            _motivo = Campo("Motivo de retiro", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }); _motivo.Items.AddRange(MotivosRetiro.Valores); _motivo.SelectedIndex = 0; if (e != null) _motivo.SelectedItem = e.Motivo;
        }
        protected override void Aceptar() => _fabrica.HistorialLaboral.Guardar(new ExperienciaLaboral { Id = _id, Empresa = _empresa.Text, Cargo = _cargo.Text, Inicio = _inicio.Value, Fin = _fin.Value, Motivo = _motivo.Text });
    }
}
