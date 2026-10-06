using Entidades;

namespace Logica.Utilidades
{
    /// <summary>Guarda quien inicio sesion mientras la aplicacion esta abierta.</summary>
    public static class Sesion
    {
        public static Usuario? UsuarioActual { get; set; }

        public static bool HayUsuario { get { return UsuarioActual != null; } }

        public static int IdUsuario
        {
            get
            {
                if (UsuarioActual == null)
                    throw new ReglaDeNegocioException("Debes iniciar sesión primero.");
                return UsuarioActual.Id;
            }
        }

        public static void Cerrar()
        {
            UsuarioActual = null;
        }
    }
}