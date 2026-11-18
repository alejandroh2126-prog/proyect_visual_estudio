using System.Collections.Generic;
using Entidades;
using Oracle.ManagedDataAccess.Client;
using static Datos.Oracle.ConexionOracle;

namespace Datos.Oracle
{
    // Los DAOs LEEN desde las vistas (vw_sg_*) y ESCRIBEN en las tablas (sg_*).
    // Las reglas que no dependen de C# (documento valido, cargo del mismo emprendimiento,
    // categoria coherente...) las vuelven a revisar los disparadores de Oracle.

    public class UsuarioDaoOracle : IUsuarioDao
    {
        private const string Sql = "SELECT id, nombre, email, clave_hash, fecha_registro FROM sg_usuario";

        private static Usuario Mapear(OracleDataReader r)
        {
            return new Usuario
            {
                Id = Int(r, "id"),
                Nombre = Txt(r, "nombre"),
                Email = Txt(r, "email"),
                ClaveHash = Txt(r, "clave_hash"),
                FechaRegistro = Fec(r, "fecha_registro")
            };
        }

        public Usuario? ObtenerPorEmail(string email)
        {
            return Uno(Sql + " WHERE email = LOWER(TRIM(:p_email))", Mapear, c => Texto(c, "email", email));
        }

        public Usuario? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public Usuario Insertar(Usuario u)
        {
            u.Id = InsertarConId(
                "INSERT INTO sg_usuario (nombre, email, clave_hash, fecha_registro) VALUES (:p_nombre, :p_email, :p_clave, :p_fecha)",
                c =>
                {
                    Texto(c, "nombre", u.Nombre);
                    Texto(c, "email", u.Email);
                    Texto(c, "clave", u.ClaveHash);
                    Fecha(c, "fecha", u.FechaRegistro);
                });
            return u;
        }
    }

    public class CatalogoDaoOracle : ICatalogoDao
    {
        public List<Sector> Sectores()
        {
            return Lista("SELECT id, nombre FROM sg_sector ORDER BY CASE WHEN nombre = 'Otro' THEN 1 ELSE 0 END, nombre",
                r => new Sector { Id = Int(r, "id"), Nombre = Txt(r, "nombre") });
        }

        public List<Ciudad> Ciudades()
        {
            return Lista("SELECT codigo, nombre, departamento, nombre_completo FROM vw_sg_ciudades ORDER BY nombre, departamento",
                r => new Ciudad
                {
                    Codigo = Txt(r, "codigo"),
                    Nombre = Txt(r, "nombre"),
                    Departamento = Txt(r, "departamento"),
                    NombreCompleto = Txt(r, "nombre_completo")
                });
        }

        public List<CargoPlantilla> PlantillasDeCargo(int sectorId)
        {
            return Lista("SELECT id, sector_id, nombre, salario_sugerido, descripcion FROM sg_cargo_plantilla " +
                         "WHERE sector_id = :p_sector ORDER BY nombre",
                r => new CargoPlantilla
                {
                    Id = Int(r, "id"),
                    SectorId = Int(r, "sector_id"),
                    Nombre = Txt(r, "nombre"),
                    SalarioSugerido = Dec(r, "salario_sugerido"),
                    Descripcion = Txt(r, "descripcion")
                },
                c => Entero(c, "sector", sectorId));
        }

        public List<TipoDocumento> TiposDocumento()
        {
            return Lista("SELECT codigo, nombre, patron, ayuda FROM sg_tipo_documento " +
                         "ORDER BY CASE codigo WHEN 'CC' THEN 0 ELSE 1 END, nombre",
                r => new TipoDocumento
                {
                    Codigo = Txt(r, "codigo"),
                    Nombre = Txt(r, "nombre"),
                    Patron = Txt(r, "patron"),
                    Ayuda = Txt(r, "ayuda")
                });
        }

        public List<CategoriaMovimiento> Categorias()
        {
            return Lista("SELECT id, nombre, tipo, sistema FROM sg_categoria_movimiento ORDER BY tipo, nombre",
                r => new CategoriaMovimiento
                {
                    Id = Int(r, "id"),
                    Nombre = Txt(r, "nombre"),
                    Tipo = Txt(r, "tipo"),
                    Sistema = SiNo(r, "sistema")
                });
        }

        public List<PreguntaFrecuente> PreguntasFrecuentes()
        {
            return Lista("SELECT tema, pregunta, respuesta FROM sg_conocimiento ORDER BY tema, id",
                r => new PreguntaFrecuente { Tema = Txt(r, "tema"), Pregunta = Txt(r, "pregunta"), Respuesta = Txt(r, "respuesta") });
        }
    }

    public class EmprendimientoDaoOracle : IEmprendimientoDao
    {
        private const string Sql =
            "SELECT id, usuario_id, nombre, descripcion, sector_id, sector, ciudad_codigo, ciudad, fecha_inicio, estado " +
            "FROM vw_sg_emprendimientos";

        private static Emprendimiento Mapear(OracleDataReader r)
        {
            return new Emprendimiento
            {
                Id = Int(r, "id"),
                UsuarioId = Int(r, "usuario_id"),
                Nombre = Txt(r, "nombre"),
                Descripcion = Txt(r, "descripcion"),
                SectorId = Int(r, "sector_id"),
                Sector = Txt(r, "sector"),
                CiudadCodigo = Txt(r, "ciudad_codigo"),
                Ciudad = Txt(r, "ciudad"),
                FechaInicio = Fec(r, "fecha_inicio"),
                Estado = Txt(r, "estado")
            };
        }

        private static void Parametros(OracleCommand c, Emprendimiento e)
        {
            Texto(c, "nombre", e.Nombre);
            Texto(c, "descripcion", e.Descripcion);
            Entero(c, "sector", e.SectorId);
            Texto(c, "ciudad", e.CiudadCodigo);
            Fecha(c, "fecha", e.FechaInicio);
            Texto(c, "estado", e.Estado);
        }

        public List<Emprendimiento> ListarPorUsuario(int usuarioId)
        {
            return Lista(Sql + " WHERE usuario_id = :p_usuario ORDER BY nombre", Mapear, c => Entero(c, "usuario", usuarioId));
        }

        public Emprendimiento? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public Emprendimiento Insertar(Emprendimiento e)
        {
            e.Id = InsertarConId(
                "INSERT INTO sg_emprendimiento (usuario_id, nombre, descripcion, sector_id, ciudad_codigo, fecha_inicio, estado) " +
                "VALUES (:p_usuario, :p_nombre, :p_descripcion, :p_sector, :p_ciudad, :p_fecha, :p_estado)",
                c => { Entero(c, "usuario", e.UsuarioId); Parametros(c, e); });
            return e;
        }

        public void Actualizar(Emprendimiento e)
        {
            EjecutarSql(
                "UPDATE sg_emprendimiento SET nombre = :p_nombre, descripcion = :p_descripcion, sector_id = :p_sector, " +
                "ciudad_codigo = :p_ciudad, fecha_inicio = :p_fecha, estado = :p_estado WHERE id = :p_id",
                c => { Parametros(c, e); Entero(c, "id", e.Id); });
        }

        public bool Eliminar(int id)
        {
            return EjecutarSql("DELETE FROM sg_emprendimiento WHERE id = :p_id", c => Entero(c, "id", id)) > 0;
        }
    }

    public class CargoDaoOracle : ICargoDao
    {
        private const string Sql =
            "SELECT id, emprendimiento_id, plantilla_id, nombre, salario_base, descripcion, salario_quincenal, " +
            "deducciones, salario_neto, trabajadores_activos FROM vw_sg_cargos";

        private static Cargo Mapear(OracleDataReader r)
        {
            return new Cargo
            {
                Id = Int(r, "id"),
                EmprendimientoId = Int(r, "emprendimiento_id"),
                PlantillaId = IntNulo(r, "plantilla_id"),
                Nombre = Txt(r, "nombre"),
                SalarioBase = Dec(r, "salario_base"),
                Descripcion = Txt(r, "descripcion"),
                SalarioQuincenal = Dec(r, "salario_quincenal"),
                Deducciones = Dec(r, "deducciones"),
                SalarioNeto = Dec(r, "salario_neto"),
                TrabajadoresActivos = Int(r, "trabajadores_activos")
            };
        }

        public List<Cargo> ListarPorEmprendimiento(int emprendimientoId)
        {
            return Lista(Sql + " WHERE emprendimiento_id = :p_emp ORDER BY nombre", Mapear, c => Entero(c, "emp", emprendimientoId));
        }

        public Cargo? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public Cargo Insertar(Cargo x)
        {
            x.Id = InsertarConId(
                "INSERT INTO sg_cargo (emprendimiento_id, plantilla_id, nombre, salario_base, descripcion) " +
                "VALUES (:p_emp, :p_plantilla, :p_nombre, :p_salario, :p_descripcion)",
                c =>
                {
                    Entero(c, "emp", x.EmprendimientoId);
                    Entero(c, "plantilla", x.PlantillaId);
                    Texto(c, "nombre", x.Nombre);
                    Numero(c, "salario", x.SalarioBase);
                    Texto(c, "descripcion", x.Descripcion);
                });
            return x;
        }

        public void Actualizar(Cargo x)
        {
            EjecutarSql("UPDATE sg_cargo SET plantilla_id = :p_plantilla, nombre = :p_nombre, salario_base = :p_salario, " +
                        "descripcion = :p_descripcion WHERE id = :p_id",
                c =>
                {
                    Entero(c, "plantilla", x.PlantillaId);
                    Texto(c, "nombre", x.Nombre);
                    Numero(c, "salario", x.SalarioBase);
                    Texto(c, "descripcion", x.Descripcion);
                    Entero(c, "id", x.Id);
                });
        }

        public bool Eliminar(int id)
        {
            return EjecutarSql("DELETE FROM sg_cargo WHERE id = :p_id", c => Entero(c, "id", id)) > 0;
        }
    }

    public class TrabajadorDaoOracle : ITrabajadorDao
    {
        private const string Sql =
            "SELECT id, emprendimiento_id, cargo_id, cargo, salario_base, tipo_documento, tipo_documento_nombre, " +
            "numero_documento, nombre, apellido, telefono, email, ciudad_codigo, ciudad, fecha_ingreso, estado " +
            "FROM vw_sg_trabajadores";

        private static Trabajador Mapear(OracleDataReader r)
        {
            return new Trabajador
            {
                Id = Int(r, "id"),
                EmprendimientoId = Int(r, "emprendimiento_id"),
                CargoId = Int(r, "cargo_id"),
                Cargo = Txt(r, "cargo"),
                SalarioBase = Dec(r, "salario_base"),
                TipoDocumento = Txt(r, "tipo_documento"),
                TipoDocumentoNombre = Txt(r, "tipo_documento_nombre"),
                NumeroDocumento = Txt(r, "numero_documento"),
                Nombre = Txt(r, "nombre"),
                Apellido = Txt(r, "apellido"),
                Telefono = Txt(r, "telefono"),
                Email = Txt(r, "email"),
                CiudadCodigo = Txt(r, "ciudad_codigo"),
                Ciudad = Txt(r, "ciudad"),
                FechaIngreso = Fec(r, "fecha_ingreso"),
                Estado = Txt(r, "estado")
            };
        }

        private static void Parametros(OracleCommand c, Trabajador t)
        {
            Entero(c, "cargo", t.CargoId);
            Texto(c, "tipo", t.TipoDocumento);
            Texto(c, "numero", t.NumeroDocumento);
            Texto(c, "nombre", t.Nombre);
            Texto(c, "apellido", t.Apellido);
            Texto(c, "telefono", t.Telefono);
            Texto(c, "email", t.Email);
            Texto(c, "ciudad", t.CiudadCodigo);
            Fecha(c, "fecha", t.FechaIngreso);
            Texto(c, "estado", t.Estado);
        }

        public List<Trabajador> ListarPorEmprendimiento(int emprendimientoId)
        {
            return Lista(Sql + " WHERE emprendimiento_id = :p_emp ORDER BY estado, nombre, apellido", Mapear,
                         c => Entero(c, "emp", emprendimientoId));
        }

        public Trabajador? ObtenerPorId(int id)
        {
            return Uno(Sql + " WHERE id = :p_id", Mapear, c => Entero(c, "id", id));
        }

        public Trabajador Insertar(Trabajador t)
        {
            t.Id = InsertarConId(
                "INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, " +
                "telefono, email, ciudad_codigo, fecha_ingreso, estado) VALUES (:p_emp, :p_cargo, :p_tipo, :p_numero, " +
                ":p_nombre, :p_apellido, :p_telefono, :p_email, :p_ciudad, :p_fecha, :p_estado)",
                c => { Entero(c, "emp", t.EmprendimientoId); Parametros(c, t); });
            return t;
        }

        public void Actualizar(Trabajador t)
        {
            EjecutarSql(
                "UPDATE sg_trabajador SET cargo_id = :p_cargo, tipo_documento = :p_tipo, numero_documento = :p_numero, " +
                "nombre = :p_nombre, apellido = :p_apellido, telefono = :p_telefono, email = :p_email, " +
                "ciudad_codigo = :p_ciudad, fecha_ingreso = :p_fecha, estado = :p_estado WHERE id = :p_id",
                c => { Parametros(c, t); Entero(c, "id", t.Id); });
        }

        public bool Eliminar(int id)
        {
            return EjecutarSql("DELETE FROM sg_trabajador WHERE id = :p_id", c => Entero(c, "id", id)) > 0;
        }
    }
}
