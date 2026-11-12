using System.Windows.Forms;
using Logica;
using Presentacion.Comun;
namespace Presentacion.Dialogos
{
    public class DialogoVacante : DialogoBase
    {
        private readonly FabricaServicios _fabrica; private readonly int _empresa;
        private readonly TextBox _cargo, _ciudad, _descripcion; private readonly NumericUpDown _salario;
        public DialogoVacante(FabricaServicios fabrica, int empresa) : base("Publicar vacante")
        {
            _fabrica = fabrica; _empresa = empresa;
            _cargo = Campo("Cargo *", new TextBox()); _ciudad = Campo("Ciudad *", new TextBox());
            _descripcion = Campo("Descripción *", new TextBox { Multiline = true, Height = 70 }); _salario = Campo("Salario mensual (0: a convenir)", Tema.Numero(1000000000m, 0));
        }
        protected override void Aceptar() => _fabrica.Empleos.CrearVacante(_empresa, _cargo.Text, _ciudad.Text, _descripcion.Text, _salario.Value);
    }
}
