using System.Windows.Forms;
using Entidades;
using Logica;
using Presentacion.Comun;
namespace Presentacion.Dialogos
{
    public class DialogoRevisionHistorial : DialogoBase
    {
        private readonly FabricaServicios _fabrica; private readonly int _id; private readonly TextBox _texto;
        public DialogoRevisionHistorial(FabricaServicios fabrica, Postulacion p) : base("Solicitar corrección")
        {
            _fabrica = fabrica; _id = p.Id;
            Campo("Motivo registrado", new Label { Text = p.MotivoRetiro, AutoSize = true });
            _texto = Campo("Tu solicitud *", new TextBox { Multiline = true, Height = 95, MaxLength = 1000, Text = p.SolicitudRevision });
        }
        protected override void Aceptar() => _fabrica.Empleos.SolicitarRevision(_id, _texto.Text);
    }
}
