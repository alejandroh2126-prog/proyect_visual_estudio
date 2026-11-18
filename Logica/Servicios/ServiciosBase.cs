using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Datos;
using Entidades;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: registrar usuarios e iniciar sesion.
    /// La clave se guarda con PBKDF2 (SHA-256, 100.000 iteraciones) + sal aleatoria.
    /// </summary>
    public class ServicioAutenticacion
    {
        private const int Iteraciones = 100000;
        private readonly IUsuarioDao _usuarios;

        public ServicioAutenticacion(IUsuarioDao usuarios)
        {
            _usuarios = usuarios;
        }

        public Usuario Registrar(string nombre, string email, string clave, string confirmacion)
        {
            string n = Validar.NombrePersona(nombre, "Nombre");
            string correo = Validar.Correo(email);
            Validar.Clave(clave);
            if (clave != confirmacion)
                throw new ReglaDeNegocioException("Las contraseñas no coinciden.");

            if (_usuarios.ObtenerPorEmail(correo) != null)
                throw new ReglaDeNegocioException("Este correo ya está registrado. Inicia sesión.");

            Usuario nuevo = new Usuario
            {
                Nombre = n,
                Email = correo,
                ClaveHash = GenerarHash(clave),
                FechaRegistro = DateTime.Now
            };
            return _usuarios.Insertar(nuevo);
        }

        /// <summary>Busca el usuario por correo (una sola consulta) y compara el hash.</summary>
        public Usuario IniciarSesion(string email, string clave)
        {
            string correo = Validar.Correo(email);
            if (string.IsNullOrEmpty(clave))
                throw new ReglaDeNegocioException("Escribe tu contraseña.");

            Usuario? u = _usuarios.ObtenerPorEmail(correo);
            if (u == null || !VerificarHash(clave, u.ClaveHash))
                throw new ReglaDeNegocioException("Correo o contraseña incorrectos.");
            return u;
        }

        private static string GenerarHash(string clave)
        {
            byte[] sal = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, 32);
            return Convert.ToBase64String(sal) + ":" + Convert.ToBase64String(hash);
        }

        private static bool VerificarHash(string clave, string almacenado)
        {
            string[] partes = (almacenado ?? string.Empty).Split(':');
            if (partes.Length != 2) return false;
            try
            {
                byte[] sal = Convert.FromBase64String(partes[0]);
                byte[] esperado = Convert.FromBase64String(partes[1]);
                byte[] hash = Rfc2898DeriveBytes.Pbkdf2(clave, sal, Iteraciones, HashAlgorithmName.SHA256, esperado.Length);
                return CryptographicOperations.FixedTimeEquals(hash, esperado);
            }
            catch (FormatException)
            {
                return false;   // hash dañado: se trata como clave incorrecta, no como error del programa
            }
        }
    }

    /// <summary>
    /// UNA SOLA MISION: entregar los catalogos (diccionarios de datos) para las listas desplegables
    /// y el autocompletado. Se leen una vez y se guardan en memoria porque casi nunca cambian.
    /// </summary>
    public class ServicioCatalogos
    {
        private readonly ICatalogoDao _catalogos;
        private readonly IProductoDao _productos;
        private readonly object _candado = new object();
        private List<Sector>? _sectores;
        private List<Ciudad>? _ciudades;
        private List<TipoDocumento>? _tipos;
        private List<CategoriaMovimiento>? _categorias;
        private readonly Dictionary<int, List<CargoPlantilla>> _plantillas = new Dictionary<int, List<CargoPlantilla>>();

        public ServicioCatalogos(ICatalogoDao catalogos, IProductoDao productos)
        {
            _catalogos = catalogos;
            _productos = productos;
        }

        public List<Sector> Sectores() { lock (_candado) { return _sectores ??= _catalogos.Sectores(); } }
        public List<Ciudad> Ciudades() { lock (_candado) { return _ciudades ??= _catalogos.Ciudades(); } }
        public List<TipoDocumento> TiposDocumento() { lock (_candado) { return _tipos ??= _catalogos.TiposDocumento(); } }

        public List<CategoriaMovimiento> Categorias()
        {
            lock (_candado) { return _categorias ??= _catalogos.Categorias(); }
        }

        /// <summary>Categorias que el usuario puede elegir a mano (sin las de sistema).</summary>
        public List<CategoriaMovimiento> CategoriasManuales(string tipo)
        {
            return Categorias().FindAll(c => c.Tipo == tipo && !c.Sistema);
        }

        /// <summary>Categorias de gasto para presupuestos (incluye Inventario, que se llena con las compras).</summary>
        public List<CategoriaMovimiento> CategoriasDeGasto()
        {
            return Categorias().FindAll(c => c.Tipo == CategoriaMovimiento.TipoGasto);
        }

        public List<CargoPlantilla> PlantillasDeCargo(int sectorId)
        {
            lock (_candado)
            {
                if (!_plantillas.TryGetValue(sectorId, out List<CargoPlantilla>? lista))
                {
                    lista = _catalogos.PlantillasDeCargo(sectorId);
                    _plantillas[sectorId] = lista;
                }
                return lista;
            }
        }

        public List<string> CategoriasDeProducto(int emprendimientoId)
        {
            return _productos.CategoriasUsadas(emprendimientoId);
        }

        public List<PreguntaFrecuente> PreguntasFrecuentes() { return _catalogos.PreguntasFrecuentes(); }

        public TipoDocumento? TipoDocumento(string codigo)
        {
            return TiposDocumento().Find(t => t.Codigo == codigo);
        }

        public Ciudad? Ciudad(string codigo)
        {
            return Ciudades().Find(c => c.Codigo == codigo);
        }

        /// <summary>Busca ciudades por texto sin importar tildes ("valle" -> Valledupar). Para el bot.</summary>
        public List<Ciudad> BuscarCiudades(string texto, int maximo = 8)
        {
            string t = Formato.Normalizar(texto).Trim();
            List<Ciudad> exactas = new List<Ciudad>();
            List<Ciudad> parecidas = new List<Ciudad>();
            foreach (Ciudad c in Ciudades())
            {
                string nombre = Formato.Normalizar(c.Nombre);
                if (nombre == t) exactas.Add(c);
                else if (nombre.StartsWith(t) || Formato.Normalizar(c.NombreCompleto).Contains(t)) parecidas.Add(c);
            }
            exactas.AddRange(parecidas);
            return exactas.Count > maximo ? exactas.GetRange(0, maximo) : exactas;
        }
    }

    /// <summary>
    /// UNA SOLA MISION: emprendimientos del usuario. Tambien comprueba que un emprendimiento
    /// pertenezca a quien lo pide (la app y el bot llaman a Verificar antes de mostrar datos).
    /// </summary>
    public class ServicioEmprendimientos
    {
        private readonly IEmprendimientoDao _emprendimientos;
        private readonly IReporteDao _reportes;

        public ServicioEmprendimientos(IEmprendimientoDao emprendimientos, IReporteDao reportes)
        {
            _emprendimientos = emprendimientos;
            _reportes = reportes;
        }

        public List<Emprendimiento> Listar(int usuarioId)
        {
            return _emprendimientos.ListarPorUsuario(usuarioId);
        }

        public List<ResumenEmprendimiento> Resumenes(int usuarioId)
        {
            return _reportes.ResumenesDelUsuario(usuarioId);
        }

        public Emprendimiento Verificar(int usuarioId, int emprendimientoId)
        {
            Emprendimiento? e = _emprendimientos.ObtenerPorId(emprendimientoId);
            if (e == null || e.UsuarioId != usuarioId)
                throw new ReglaDeNegocioException("Emprendimiento no encontrado.");
            return e;
        }

        /// <summary>Crea (Id = 0) o actualiza.</summary>
        public Emprendimiento Guardar(int usuarioId, Emprendimiento e)
        {
            e.Nombre = Validar.Requerido(e.Nombre, "Nombre", 150);
            e.Descripcion = Validar.Opcional(e.Descripcion, "Descripción", 500);
            Validar.Seleccionado(e.SectorId, "Sector");
            Validar.Seleccionado(e.CiudadCodigo, "Ciudad");
            Validar.FechaNoFutura(e.FechaInicio, "Fecha de inicio");
            if (e.Estado != Estados.Activo && e.Estado != Estados.Inactivo)
                throw new ReglaDeNegocioException("El estado debe ser activo o inactivo.");

            foreach (Emprendimiento otro in _emprendimientos.ListarPorUsuario(usuarioId))
                if (otro.Id != e.Id && string.Equals(otro.Nombre, e.Nombre, StringComparison.OrdinalIgnoreCase))
                    throw new ReglaDeNegocioException("Ya tienes un emprendimiento con ese nombre.");

            if (e.Id == 0)
            {
                e.UsuarioId = usuarioId;
                return _emprendimientos.Insertar(e);
            }

            Verificar(usuarioId, e.Id);
            e.UsuarioId = usuarioId;
            _emprendimientos.Actualizar(e);
            return e;
        }

        /// <summary>Oracle elimina en cascada todo lo del emprendimiento (ON DELETE CASCADE).</summary>
        public void Eliminar(int usuarioId, int emprendimientoId)
        {
            Verificar(usuarioId, emprendimientoId);
            _emprendimientos.Eliminar(emprendimientoId);
        }
    }

    /// <summary>
    /// UNA SOLA MISION: configuracion compartida en Oracle (sg_configuracion).
    /// Asi la clave de Groq se escribe UNA vez y sirve en todos los computadores y en el bot.
    /// </summary>
    public class ServicioConfiguracion
    {
        public const string ClaveGroq = "GROQ_API_KEY";
        public const string ModeloGroq = "GROQ_MODELO";

        private readonly IConfiguracionDao _dao;
        public ServicioConfiguracion(IConfiguracionDao dao) { _dao = dao; }

        /// <summary>Devuelve null si no existe o si la tabla aun no se creo (script sin ejecutar).</summary>
        public string? Leer(string clave)
        {
            try
            {
                string? v = _dao.Leer(clave);
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            catch (ReglaDeNegocioException) { return null; }
        }

        public void Guardar(string clave, string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) throw new ReglaDeNegocioException("El valor no puede quedar vacío.");
            try { _dao.Guardar(clave, valor.Trim().Trim('"')); }
            catch (ReglaDeNegocioException ex) when (ex.Message.Contains("ORA-06550") || ex.Message.Contains("PLS-00201") || ex.Message.Contains("ORA-00942"))
            {
                throw new ReglaDeNegocioException("Falta ejecutar sql/sgape_v2_configuracion.sql en Oracle (crea la tabla de configuración).");
            }
        }
    }

    /// <summary>UNA SOLA MISION: vincular Telegram con la cuenta (paquete pkg_sg_telegram).</summary>
    public class ServicioTelegram
    {
        private readonly ITelegramDao _telegram;

        public ServicioTelegram(ITelegramDao telegram)
        {
            _telegram = telegram;
        }

        public string GenerarCodigo(int usuarioId) { return _telegram.GenerarCodigo(usuarioId); }
        public int ChatsVinculados(int usuarioId) { return _telegram.ChatsVinculados(usuarioId); }

        public VinculoTelegram Vincular(long chatId, string codigo)
        {
            string c = (codigo ?? string.Empty).Trim();
            if (c.Length != 6 || !int.TryParse(c, out _))
                throw new ReglaDeNegocioException("El código tiene 6 números. Genéralo en la aplicación, sección Telegram.");
            return _telegram.Vincular(chatId, c);
        }

        public int? UsuarioDeChat(long chatId) { return _telegram.UsuarioDeChat(chatId); }
        public int? EmprendimientoDeChat(long chatId) { return _telegram.EmprendimientoDeChat(chatId); }
        public void FijarEmprendimiento(long chatId, int emprendimientoId) { _telegram.FijarEmprendimiento(chatId, emprendimientoId); }
        public void Desvincular(long chatId) { _telegram.Desvincular(chatId); }
        public List<ChatAlerta> ChatsConAlertas() { return _telegram.ChatsConAlertas(); }
        public bool CambiarAlertas(long chatId, bool? activar) { return _telegram.CambiarAlertas(chatId, activar); }
    }
}
