using System;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.Dialogos
{
    /// <summary>Agrega a Nómina a un trabajador contratado por la bolsa de empleo.</summary>
    public class DialogoIncorporar : DialogoBase
    {
        private readonly FabricaServicios _fabrica;
        private readonly int _postulacionId;
        private readonly TextBox _nombre, _apellido, _numero;
        private readonly ComboBox _tipo;

        public DialogoIncorporar(FabricaServicios fabrica, Postulacion p) : base("Agregar a Nómina")
        {
            _fabrica = fabrica;
            _postulacionId = p.Id;
            string[] partes = p.NombreTrabajador.Trim().Split(new[] { ' ' }, 2);
            _nombre = Campo("Nombre *", new TextBox { Text = partes[0] });
            _apellido = Campo("Apellido *", new TextBox { Text = partes.Length > 1 ? partes[1] : "" });
            _tipo = Campo("Tipo de documento *", new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList });
            _tipo.Items.AddRange(TiposDocumento.Valores);
            _tipo.SelectedIndex = 0;
            _numero = Campo("Número de documento *", new TextBox());
            Campo("Cargo", new Label { Text = p.Cargo + " · " + (p.SalarioPropuesto == 0 ? "salario a convenir" : Formato.Moneda(p.SalarioPropuesto)), AutoSize = true });
            Campo("Privacidad", new Label { Text = "El documento del trabajador es privado:\npídeselo para completar su registro.", AutoSize = true });
        }

        protected override void Aceptar()
        {
            _fabrica.Incorporacion.Incorporar(_postulacionId, _nombre.Text, _apellido.Text, _tipo.Text, _numero.Text);
        }
    }
}