using System;
using Entidades;
using Logica.Utilidades;
namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: convertir una contratación aceptada en un trabajador de Nómina.
    /// El número de documento NO viaja desde el perfil del candidato (es privado): lo escribe el empleador.
    /// </summary>
    public class ServicioIncorporacion
    {
        private readonly ServicioEmpleos _empleos;
        private readonly ServicioNomina _nomina;
        public ServicioIncorporacion(ServicioEmpleos empleos, ServicioNomina nomina) { _empleos = empleos; _nomina = nomina; }
        public Trabajador Incorporar(int postulacionId, string nombre, string apellido, string tipoDocumento, string numeroDocumento)
        {
            Postulacion p = _empleos.ObtenerContratacion(postulacionId);
            Cargo? cargo = _nomina.ListarCargos(p.EmprendimientoId).Find(c => string.Equals(c.Nombre, p.Cargo, StringComparison.OrdinalIgnoreCase));
            if (cargo == null)
            {
                if (p.SalarioPropuesto <= 0) throw new ReglaDeNegocioException("La vacante era \"a convenir\": crea primero el cargo \"" + p.Cargo + "\" en Nómina con su salario.");
                cargo = _nomina.GuardarCargo(new Cargo { EmprendimientoId = p.EmprendimientoId, Nombre = p.Cargo, SalarioBase = p.SalarioPropuesto, Descripcion = "Creado desde una contratación" });
            }
            Trabajador t = new Trabajador(0, nombre, apellido, numeroDocumento, cargo, p.InicioLaboral ?? DateTime.Today, p.EmprendimientoId) { TipoDocumento = tipoDocumento };
            return _nomina.GuardarTrabajador(t);
        }
    }
}