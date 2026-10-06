using System;
using System.Security.Cryptography;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: registrar usuarios e iniciar sesion.
    /// BAJO ACOPLAMIENTO: solo conoce a RepositorioUsuarios (se lo entregan en el constructor).
    /// Las claves se guardan con PBKDF2 + sal aleatoria (equivalente al bcrypt del proyecto en Node).
    /// </summary>
    public class ServicioAutenticacion
    {
        private const int Iteraciones = 100000;
        private readonly RepositorioUsuarios _usuarios;

        public ServicioAutenticacion(RepositorioUsuarios usuarios)
        {
            _usuarios = usuarios;
        }

        public Usuario Registrar(string nombre, string email, string clave)
        {
            Validar.Requerido(nombre, "Nombre");
            Validar.Correo(email);
            Validar.Requerido(clave, "Contraseña");
            if (clave.Length < 6)
                throw new ReglaDeNegocioException("La contraseña debe tener al menos 6 caracteres.");

            string correo = email.Trim().ToLowerInvariant();
            foreach (Usuario u in _usuarios.Listar())
            {
                if (u.Email == correo)
                    throw new ReglaDeNegocioException("Este correo ya está registrado.");
            }

            Usuario nuevo = new Usuario
            {
                Nombre = nombre.Trim(),
                Email = correo,
                ClaveHash = GenerarHash(clave),
                FechaRegistro = DateTime.Now
            };
            _usuarios.Insertar(nuevo);
            Sesion.UsuarioActual = nuevo;
            return nuevo;
        }

        public Usuario IniciarSesion(string email, string clave)
        {
            Validar.Requerido(email, "Correo");
            Validar.Requerido(clave, "Contraseña");

            string correo = email.Trim().ToLowerInvariant();
            foreach (Usuario u in _usuarios.Listar())
            {
                if (u.Email == correo && VerificarHash(clave, u.ClaveHash))
                {
                    Sesion.UsuarioActual = u;
                    return u;
                }
            }
            throw new ReglaDeNegocioException("Correo o contraseña incorrectos.");
        }

        public void CerrarSesion()
        {
            Sesion.Cerrar();
        }

        private static string GenerarHash(string clave)
        {
            byte[] sal = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
            return Convert.ToBase64String(sal) + ":" + Convert.ToBase64String(hash);
        }

        private static bool VerificarHash(string clave, string almacenado)
        {
            string[] partes = almacenado.Split(':');
            if (partes.Length != 2) return false;
            byte[] sal = Convert.FromBase64String(partes[0]);
            byte[] esperado = Convert.FromBase64String(partes[1]);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(hash, esperado);
        }
    }
}