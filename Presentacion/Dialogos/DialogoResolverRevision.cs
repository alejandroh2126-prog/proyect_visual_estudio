using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;
namespace Presentacion.Dialogos
{
    public class DialogoResolverRevision : DialogoBase
    {
        private readonly FabricaServicios _fabrica; private readonly int _id; private readonly ComboBox _motivo; private readonly CheckBox _modificar;
        public DialogoResolverRevision(FabricaServicios fabrica, Postulacion p) : base("Revisar motivo de retiro")
        {
            _fabrica = fabrica; _id = p.Id;
            Campo("Solicitud del trabajador", new TextBox { Text = p.SolicitudRevision, ReadOnly = true, Multiline = true, Height = 100, ScrollBars = ScrollBars.Vertical });
            _modificar = Campo("Decisión", new CheckBox { Text = "Corregir motivo (si no, mantenerlo)", AutoSize = true });
            _motivo = Campo("Motivo", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }); _motivo.Items.AddRange(MotivosRetiro.Valores); _motivo.SelectedIndex = 0; _motivo.SelectedItem = p.MotivoRetiro;
        }
        protected override void Aceptar() => _fabrica.Empleos.ResolverRevision(_id, _motivo.Text, _modificar.Checked);
    }
}
