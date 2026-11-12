using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;
namespace Presentacion.Dialogos
{
    public class DialogoFinalizarEmpleo : DialogoBase
    {
        private readonly FabricaServicios _fabrica; private readonly int _id; private readonly ComboBox _motivo;
        public DialogoFinalizarEmpleo(FabricaServicios fabrica, Postulacion p) : base("Finalizar vinculación")
        {
            _fabrica = fabrica; _id = p.Id;
            Campo("Cargo", new Label { Text = p.Cargo, AutoSize = true });
            _motivo = Campo("Motivo de finalización", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }); _motivo.Items.AddRange(MotivosRetiro.Valores); _motivo.SelectedIndex = 0;
            Campo("Privacidad", new Label { Text = "Solo visible para este empleador y el trabajador. No es una lista pública.", AutoSize = true });
        }
        protected override void Aceptar() => _fabrica.Empleos.FinalizarVinculacion(_id, _motivo.Text);
    }
}
