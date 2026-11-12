using Entidades;
using Logica;
using Logica.Utilidades;

// Pruebas de integración en una carpeta temporal, sin tocar los CSV del usuario.
internal static class Program
{
    private static int _comprobaciones;
    private static void Igual(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception("FALLO: " + mensaje);
        _comprobaciones++; Console.WriteLine("OK: " + mensaje);
    }
    private static void Rechaza(Action accion, string mensaje)
    {
        try { accion(); }
        catch (ReglaDeNegocioException) { Igual(true, mensaje); return; }
        throw new Exception("FALLO: no se rechazó " + mensaje);
    }
    private static int Main()
    {
        string ruta = Path.Combine(Path.GetTempPath(), "SGAPE-pruebas-" + Guid.NewGuid().ToString("N"));
        RutasApp.CarpetaDatosPruebas = ruta;
        try
        {
            Ejecutar(ruta); Console.WriteLine($"\n{_comprobaciones} comprobaciones correctas."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Sesion.Cerrar(); RutasApp.CarpetaDatosPruebas = null; if (Directory.Exists(ruta)) Directory.Delete(ruta, true); }
    }
    private static void Ejecutar(string ruta)
    {
        FabricaServicios f = new FabricaServicios();
        string foto = Path.Combine(ruta, "foto.png"), cv = Path.Combine(ruta, "cv.pdf"), invalido = Path.Combine(ruta, "invalido.pdf");
        File.WriteAllBytes(foto, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/j1kAAAAASUVORK5CYII="));
        File.WriteAllText(cv, "%PDF-1.4\n%%EOF\n"); File.WriteAllText(invalido, "No es un documento PDF");
        Usuario empr = f.Autenticacion.Registrar("Empresa de prueba", "empresa@example.test", "clave-segura-1");
        Igual(empr.Rol == RolUsuario.Emprendedor, "rol predeterminado");
        // Simula un CSV de la versión original, sin la columna Rol.
        f.Autenticacion.CerrarSesion();
        string usuarios = Path.Combine(ruta, "usuarios.csv");
        string[] registro = CsvUtil.Dividir(File.ReadAllLines(usuarios)[0]); File.WriteAllLines(usuarios, new[] { CsvUtil.Unir(registro.Take(5).ToArray()) });
        Igual(f.Autenticacion.IniciarSesion("empresa@example.test", "clave-segura-1").Rol == RolUsuario.Emprendedor, "compatibilidad con cuentas antiguas");
        f.PerfilCuenta.Guardar("#EEEEEE", "#123456", foto, false);
        Igual(f.PerfilCuenta.Obtener().ColorBotones == "#123456" && File.Exists(f.PerfilCuenta.RutaFoto()), "foto y colores persistentes");
        Rechaza(() => f.PerfilCuenta.Guardar("#000000", "#123456", null, false), "contraste de fondo inválido");
        Emprendimiento emp = f.Emprendimientos.Crear("Tienda", "", "Comercio", DateTime.Today);
        Cargo cargo = f.Nomina.GuardarCargo(new Cargo { EmprendimientoId = emp.Id, Nombre = "Vendedor", SalarioBase = 2000000 });
        Trabajador t = f.Nomina.GuardarTrabajador(new Trabajador(0, "Ana", "Prueba", "123456", cargo, DateTime.Today, emp.Id) { TipoDocumento = "CE" });
        Igual(f.Nomina.ListarTrabajadores(emp.Id)[0].TipoDocumento == "CE", "tipo de documento persistente");
        f.FichasTrabajador.Guardar(t.Id, new FichaTrabajador { Ciudad = "Valledupar", Telefono = "3000000000", TipoContrato = "Fijo" });
        f.FichasTrabajador.CambiarAdjunto(t.Id, AdjuntoTrabajador.HojaVida, cv);
        Igual(f.FichasTrabajador.Obtener(t.Id).Ciudad == "Valledupar" && File.Exists(f.FichasTrabajador.RutaAdjunto(t.Id, AdjuntoTrabajador.HojaVida)), "ficha y CV de empleado");
        Rechaza(() => f.FichasTrabajador.CambiarAdjunto(t.Id, AdjuntoTrabajador.HojaVida, invalido), "rechazar archivo con firma inválida");
        Igual(File.Exists(f.FichasTrabajador.RutaAdjunto(t.Id, AdjuntoTrabajador.HojaVida)), "conservar adjunto anterior ante error");
        Vacante v = f.Empleos.CrearVacante(emp.Id, "Vendedor", "Valledupar", "Atención al cliente", 2000000);
        f.Autenticacion.CerrarSesion();
        Usuario candidato = f.Autenticacion.Registrar("Candidato de prueba", "trabajador@example.test", "clave-segura-2", RolUsuario.Trabajador);
        Igual(f.PerfilCuenta.Obtener().Foto == "" && f.PerfilCuenta.Obtener().ColorBotones == "", "preferencias aisladas por usuario");
        Rechaza(() => f.FichasTrabajador.Obtener(t.Id), "trabajador sin acceso a ficha ajena");
        Rechaza(() => f.Empleos.CrearVacante(emp.Id, "X", "Y", "Z", 1), "cuenta trabajador no puede publicar vacantes");
        Rechaza(() => f.Empleos.Postular(v.Id), "consentimiento previo a postularse");
        f.Empleos.GuardarMiPerfil(new PerfilCandidato { Ciudad = "Valledupar", Profesion = "Vendedor", NumeroDocumento = "999999", Telefono = "3111111111", Visible = true });
        f.Empleos.CambiarMiCv(cv);
        f.Empleos.CambiarMiIdentificacion(false, foto);
        Igual(File.Exists(f.Empleos.RutaMiIdentificacion(false)), "identificación del candidato privada");
        Igual(f.Empleos.ListarVacantesPublicas().Exists(x => x.Id == v.Id), "vacante visible al trabajador");
        f.Empleos.Postular(v.Id); Rechaza(() => f.Empleos.Postular(v.Id), "no duplicar postulación");
        int p = f.Empleos.ListarMisPostulaciones()[0].Id;
        f.HistorialLaboral.Guardar(new ExperienciaLaboral { Empresa = "Antes", Cargo = "Auxiliar", Inicio = DateTime.Today.AddYears(-1), Fin = DateTime.Today.AddDays(-1), Motivo = "Renuncia voluntaria" });
        Igual(f.HistorialLaboral.ListarDeclaradas().Count == 1, "experiencia declarada");
        f.Autenticacion.CerrarSesion();
        f.Autenticacion.IniciarSesion("empresa@example.test", "clave-segura-1");
        CandidatoPublico publico = f.Empleos.ListarCandidatos().Single(x => x.UsuarioId == candidato.Id);
        Igual(publico.TieneHojaVida && File.Exists(f.Empleos.RutaCvCandidato(candidato.Id)), "empleador autorizado puede consultar CV");
        Igual(typeof(CandidatoPublico).GetProperty("NumeroDocumento") == null && typeof(CandidatoPublico).GetProperty("Telefono") == null, "datos privados fuera del directorio");
        f.Empleos.Ofertar(p); Rechaza(() => f.Empleos.Ofertar(p), "oferta sin duplicar");
        f.Autenticacion.CerrarSesion();
        f.Autenticacion.IniciarSesion("trabajador@example.test", "clave-segura-2");
        f.Empleos.ResponderOferta(p, true);
        Igual(f.HistorialLaboral.ListarRegistradas().Single().Estado == EstadoPostulacion.Contratada, "aceptar oferta crea vinculación");
        f.Autenticacion.CerrarSesion();
        f.Autenticacion.IniciarSesion("empresa@example.test", "clave-segura-1");
        Rechaza(() => f.Emprendimientos.Eliminar(emp.Id), "no eliminar negocio con vínculo activo");
        f.Empleos.FinalizarVinculacion(p, "Renuncia voluntaria");
        f.Autenticacion.CerrarSesion();
        f.Autenticacion.IniciarSesion("trabajador@example.test", "clave-segura-2");
        Igual(f.HistorialLaboral.ListarRegistradas().Single().MotivoRetiro == "Renuncia voluntaria", "motivo privado disponible para trabajador");
        f.Empleos.SolicitarRevision(p, "Solicito actualizar el motivo.");
        f.Empleos.GuardarMiPerfil(new PerfilCandidato { Ciudad = "Valledupar", Profesion = "Vendedor", Visible = false });
        f.Autenticacion.CerrarSesion();
        f.Autenticacion.IniciarSesion("empresa@example.test", "clave-segura-1");
        Igual(f.Empleos.ListarCandidatos().Count == 0, "retirar visibilidad del perfil");
        Rechaza(() => f.Empleos.RutaCvCandidato(candidato.Id), "no acceder al CV retirado");
        f.Empleos.ResolverRevision(p, "Mutuo acuerdo", true);
        f.Emprendimientos.Eliminar(emp.Id);
        Igual(f.Empleos.ListarHistorialEmprendedor().Single().MotivoRetiro == "Mutuo acuerdo", "historial conserva motivo corregido tras eliminar negocio");
        f.Autenticacion.CerrarSesion();
        f.Autenticacion.Registrar("Otro empleador", "otro@example.test", "clave-segura-3");
        Rechaza(() => f.Empleos.ResolverRevision(p, "Otro / no especificado", true), "otro empleador no puede cambiar historial ajeno");
        Igual(f.Empleos.ListarHistorialEmprendedor().Count == 0, "no hay historial privado de otros negocios");
        f.Autenticacion.CerrarSesion();
        FabricaServicios reinicio = new FabricaServicios(); reinicio.Autenticacion.IniciarSesion("trabajador@example.test", "clave-segura-2");
        Igual(reinicio.HistorialLaboral.ListarRegistradas().Single().MotivoRetiro == "Mutuo acuerdo", "historial persistente después de reiniciar");
        Igual(reinicio.HistorialLaboral.ListarDeclaradas().Count == 1, "experiencia anterior persistente");
    }
}
