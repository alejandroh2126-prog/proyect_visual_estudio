using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Entidades;
using Logica.Servicios;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    // =====================================================================
    // INICIO
    // =====================================================================
    public partial class InicioViewModel : ViewModelBase
    {
        [ObservableProperty] private ResumenEmprendimiento? _resumen;
        [ObservableProperty] private ResumenVentas? _ventas;
        [ObservableProperty] private bool _hayEmprendimiento;

        public ObservableCollection<ProductoStockBajo> StockBajo { get; } = new ObservableCollection<ProductoStockBajo>();
        public ObservableCollection<Presupuesto> Alertas { get; } = new ObservableCollection<Presupuesto>();
        public ObservableCollection<ResumenEmprendimiento> Todos { get; } = new ObservableCollection<ResumenEmprendimiento>();

        public string Saludo { get { return "Hola, " + (Sesion.Usuario?.Nombre ?? string.Empty); } }

        public override async Task Cargar()
        {
            await Ejecutar(async () =>
            {
                HayEmprendimiento = Sesion.HayEmprendimiento;
                int usuario = Sesion.IdUsuario;
                List<ResumenEmprendimiento> todos = await Fondo.Run(() => App.Servicios.Emprendimientos.Resumenes(usuario));
                Todos.Clear();
                foreach (ResumenEmprendimiento r in todos) Todos.Add(r);

                StockBajo.Clear();
                Alertas.Clear();
                if (!HayEmprendimiento) { Resumen = null; Ventas = null; return; }

                int emp = Sesion.IdEmprendimiento;
                Resumen = await Fondo.Run(() => App.Servicios.Reportes.Resumen(emp));
                Ventas = await Fondo.Run(() => App.Servicios.Ventas.Resumen(emp));
                foreach (ProductoStockBajo p in await Fondo.Run(() => App.Servicios.Inventario.StockBajo(emp))) StockBajo.Add(p);
                foreach (Presupuesto p in await Fondo.Run(() => App.Servicios.Presupuestos.Listar(emp, Validar.MesActual())))
                    if (p.Estado != Presupuesto.EstadoOk) Alertas.Add(p);
            });
        }

        [RelayCommand]
        private Task Recargar() { return Cargar(); }
    }

    // =====================================================================
    // EMPRENDIMIENTOS
    // =====================================================================
    public partial class EmprendimientosViewModel : CrudViewModel<Emprendimiento>
    {
        private readonly Func<Task> _alCambiar;

        public EmprendimientosViewModel(Func<Task> alCambiar)
        {
            _alCambiar = alCambiar;
        }

        protected override string Nombre { get { return "emprendimiento"; } }

        protected override Task<List<Emprendimiento>> Consultar()
        {
            int usuario = Sesion.IdUsuario;
            return Fondo.Run(() => App.Servicios.Emprendimientos.Listar(usuario));
        }

        protected override bool Coincide(Emprendimiento e, string f)
        {
            return Formato.Contiene(e.Nombre, f) || Formato.Contiene(e.Sector, f) || Formato.Contiene(e.Ciudad, f);
        }

        protected override string DescribirParaEliminar(Emprendimiento e)
        {
            return "\"" + e.Nombre + "\" con TODOS sus cargos, trabajadores, productos, ventas, movimientos, presupuestos y vacantes";
        }

        protected override async Task DespuesDeCargar() { await _alCambiar(); }

        protected override Task<FormularioViewModel?> CrearFormulario(Emprendimiento? e)
        {
            bool nuevo = e == null;
            List<Sector> sectores = App.Servicios.Catalogos.Sectores();
            List<Ciudad> ciudades = App.Servicios.Catalogos.Ciudades();

            FormularioViewModel f = new FormularioViewModel(nuevo ? "Nuevo emprendimiento" : "Editar emprendimiento", async form =>
            {
                Emprendimiento datos = new Emprendimiento
                {
                    Id = e?.Id ?? 0,
                    Nombre = form["nombre"].ValorTexto,
                    Descripcion = form["descripcion"].ValorTexto,
                    SectorId = form["sector"].Elegido<Sector>()?.Id ?? 0,
                    SectorOtro = form["sectorOtro"].ValorTexto,
                    CiudadCodigo = form["ciudad"].Elegido<Ciudad>()?.Codigo ?? string.Empty,
                    FechaInicio = form["fecha"].Fecha ?? DateTime.Today,
                    Estado = nuevo ? Estados.Activo : (form["estado"].Seleccion as string ?? Estados.Activo)
                };
                int usuario = Sesion.IdUsuario;
                await Fondo.Run(() => App.Servicios.Emprendimientos.Guardar(usuario, datos));
            });

            Sector? sectorActual = e == null ? null : sectores.Find(s => s.Id == e.SectorId);
            bool esOtro = sectorActual?.Nombre == ServicioCatalogos.SectorOtro;
            // Si eligen "Otro" aparece este campo para escribir a que se dedica el negocio
            CampoFormulario sectorOtro = new CampoFormulario("sectorOtro", "¿Cuál es el sector?", TipoCampo.Texto)
            {
                MaxLongitud = 80,
                Visible = esOtro,
                Texto = esOtro ? e!.SectorOtro : string.Empty,
                Ayuda = "Por ejemplo: Mascotas, Eventos, Reciclaje."
            };

            f.Agregar(new CampoFormulario("nombre", "Nombre del emprendimiento", TipoCampo.Texto) { MaxLongitud = 150, Texto = e?.Nombre ?? string.Empty });
            f.Agregar(new CampoFormulario("sector", "Sector", TipoCampo.Lista)
            {
                Opciones = sectores,
                Seleccion = sectorActual,
                Ayuda = "Define los cargos sugeridos en Nómina. Si no está el tuyo, elige \"Otro\" y escríbelo.",
                AlSeleccionar = o => sectorOtro.Visible = (o as Sector)?.Nombre == ServicioCatalogos.SectorOtro
            });
            f.Agregar(sectorOtro);
            f.Agregar(new CampoFormulario("ciudad", "Ciudad", TipoCampo.ListaPredictiva)
            {
                Opciones = ciudades,
                Seleccion = e == null ? null : ciudades.Find(c => c.Codigo == e.CiudadCodigo),
                Ayuda = "Empieza a escribir: \"Valle\" sugiere Valledupar (Cesar)."
            });
            f.Agregar(new CampoFormulario("fecha", "Fecha de inicio", TipoCampo.Fecha) { Fecha = e?.FechaInicio ?? DateTime.Today });
            f.Agregar(new CampoFormulario("descripcion", "Descripción", TipoCampo.TextoLargo) { Requerido = false, MaxLongitud = 500, Texto = e?.Descripcion ?? string.Empty });
            if (!nuevo)
                f.Agregar(new CampoFormulario("estado", "Estado", TipoCampo.Lista)
                {
                    Opciones = new[] { Estados.Activo, Estados.Inactivo },
                    Seleccion = e!.Estado
                });
            return Task.FromResult<FormularioViewModel?>(f);
        }

        protected override Task EliminarElemento(Emprendimiento e)
        {
            int usuario = Sesion.IdUsuario;
            return Fondo.Run(() => App.Servicios.Emprendimientos.Eliminar(usuario, e.Id));
        }
    }

    // =====================================================================
    // NOMINA: cargos, trabajadores y resumen
    // =====================================================================
    public partial class CargosViewModel : CrudViewModel<Cargo>
    {
        private readonly Func<Task> _alCambiar;

        public CargosViewModel(Func<Task> alCambiar) { _alCambiar = alCambiar; }

        protected override string Nombre { get { return "cargo"; } }

        protected override Task<List<Cargo>> Consultar()
        {
            int emp = Sesion.IdEmprendimiento;
            return Fondo.Run(() => App.Servicios.Nomina.Cargos(emp));
        }

        protected override bool Coincide(Cargo c, string f) { return Formato.Contiene(c.Nombre, f); }

        protected override async Task<FormularioViewModel?> CrearFormulario(Cargo? c)
        {
            bool nuevo = c == null;
            int sector = Sesion.Emprendimiento?.SectorId ?? 0;
            // Los del sector y, despues, los cargos mas comunes de cualquier negocio
            List<CargoPlantilla> plantillas = await Fondo.Run(() => App.Servicios.Catalogos.CargosSugeridos(sector));
            int emp = Sesion.IdEmprendimiento;
            List<Cargo> existentes = nuevo ? await Fondo.Run(() => App.Servicios.Nomina.Cargos(emp)) : new List<Cargo>();

            FormularioViewModel f = new FormularioViewModel(nuevo ? "Nuevo cargo" : "Editar cargo: " + c!.Nombre, async form =>
            {
                Cargo datos = new Cargo
                {
                    Id = c?.Id ?? 0,
                    PlantillaId = nuevo ? form["plantilla"].Elegido<CargoPlantilla>()?.Id : c!.PlantillaId,
                    Nombre = form["nombre"].ValorTexto,
                    SalarioBase = form["salario"].ValorMoneda,
                    TipoHorario = form["horario"].Seleccion as string ?? Horarios.TiempoCompleto,
                    Descripcion = form["descripcion"].ValorTexto
                };
                await Fondo.Run(() => App.Servicios.Nomina.GuardarCargo(emp, datos));
            });

            CampoFormulario horario = new CampoFormulario("horario", "Tipo de horario", TipoCampo.Lista)
            {
                Opciones = Horarios.Todos,
                Seleccion = c?.TipoHorario ?? Horarios.TiempoCompleto,
                Ayuda = "Tiempo completo: mínimo " + Formato.Moneda(SalarioMinimo.Para(Horarios.TiempoCompleto)) +
                        ". Medio tiempo o por turnos: mínimo " + Formato.Moneda(SalarioMinimo.Para(Horarios.MedioTiempo)) + "."
            };
            CampoFormulario salario = new CampoFormulario("salario", "Salario base mensual", TipoCampo.Moneda)
            {
                MaxLongitud = 15,
                Texto = c == null ? string.Empty : Math.Round(c.SalarioBase, 0).ToString(),
                Ayuda = "Se descuenta 4 % salud + 4 % pensión."
            };
            CampoFormulario nombre = new CampoFormulario("nombre", "Nombre del cargo", TipoCampo.Texto)
            {
                MaxLongitud = 100,
                Texto = c?.Nombre ?? string.Empty,
                // Al editar, el nombre queda fijo: los trabajadores y la nómina ya lo usan
                Habilitado = nuevo,
                Ayuda = nuevo ? null : "El nombre no se cambia. Si necesitas otro, crea un cargo nuevo."
            };
            CampoFormulario descripcion = new CampoFormulario("descripcion", "Descripción", TipoCampo.TextoLargo) { Requerido = false, MaxLongitud = 300, Texto = c?.Descripcion ?? string.Empty };

            // Reglas que se revisan mientras se escribe (el campo se pone en rojo al instante)
            salario.Regla = () => ServicioNomina.ErrorSalario(salario.ValorMoneda, horario.Seleccion as string);
            nombre.Regla = () =>
            {
                Cargo? igual = existentes.Find(x => string.Equals(x.Nombre, nombre.ValorTexto, StringComparison.OrdinalIgnoreCase));
                return igual == null ? null : "Ya existe el cargo \"" + igual.Nombre + "\".";
            };
            // Si cambia el horario, cambia el minimo: se revisa el salario otra vez
            horario.Cambio += () => salario.Revalidar();

            if (nuevo)
            {
                f.Agregar(new CampoFormulario("plantilla", "Cargo sugerido (los más comunes)", TipoCampo.ListaPredictiva)
                {
                    Requerido = false,
                    Opciones = plantillas,
                    Ayuda = "Elige uno y se llenan el nombre y el salario sugerido. También puedes escribir el tuyo abajo.",
                    AlSeleccionar = o =>
                    {
                        if (o is CargoPlantilla p)
                        {
                            nombre.Texto = p.Nombre;
                            decimal minimo = SalarioMinimo.Para(horario.Seleccion as string);
                            salario.Texto = Math.Round(Math.Max(p.SalarioSugerido, minimo), 0).ToString();
                            if (descripcion.ValorTexto.Length == 0) descripcion.Texto = p.Descripcion;
                        }
                    }
                });
            }
            f.Agregar(nombre);
            f.Agregar(horario);
            f.Agregar(salario);
            f.Agregar(descripcion);
            return f;
        }

        protected override async Task EliminarElemento(Cargo c)
        {
            int emp = Sesion.IdEmprendimiento;
            await Fondo.Run(() => App.Servicios.Nomina.EliminarCargo(emp, c.Id));
        }

        protected override async Task DespuesDeCargar() { await _alCambiar(); }
    }

    public partial class TrabajadoresViewModel : CrudViewModel<Trabajador>
    {
        private readonly Func<Task> _alCambiar;

        public TrabajadoresViewModel(Func<Task> alCambiar) { _alCambiar = alCambiar; }

        protected override string Nombre { get { return "trabajador"; } }

        protected override Task<List<Trabajador>> Consultar()
        {
            int emp = Sesion.IdEmprendimiento;
            return Fondo.Run(() => App.Servicios.Nomina.Trabajadores(emp));
        }

        protected override bool Coincide(Trabajador t, string f)
        {
            return Formato.Contiene(t.NombreCompleto, f) || t.NumeroDocumento.Contains(f) || Formato.Contiene(t.Cargo, f);
        }

        protected override string DescribirParaEliminar(Trabajador t) { return "a " + t.NombreCompleto + " (si dejó de trabajar, mejor márcalo como inactivo)"; }

        protected override async Task<FormularioViewModel?> CrearFormulario(Trabajador? t)
        {
            int emp = Sesion.IdEmprendimiento;
            List<Cargo> cargos = await Fondo.Run(() => App.Servicios.Nomina.Cargos(emp));
            if (cargos.Count == 0)
                throw new ReglaDeNegocioException("Primero crea al menos un cargo en la pestaña Cargos.");
            List<TipoDocumento> tipos = App.Servicios.Catalogos.TiposDocumento();
            List<Ciudad> ciudades = App.Servicios.Catalogos.Ciudades();

            FormularioViewModel f = new FormularioViewModel(t == null ? "Nuevo trabajador" : "Editar trabajador", async form =>
            {
                Trabajador datos = new Trabajador
                {
                    Id = t?.Id ?? 0,
                    TipoDocumento = form["tipo"].Elegido<TipoDocumento>()?.Codigo ?? string.Empty,
                    NumeroDocumento = form["numero"].ValorTexto,
                    Nombre = form["nombre"].ValorTexto,
                    Apellido = form["apellido"].ValorTexto,
                    CargoId = form["cargo"].Elegido<Cargo>()?.Id ?? 0,
                    Telefono = form["telefono"].ValorTexto,
                    Email = t?.Email ?? string.Empty,   // ya no se pide correo al trabajador
                    CiudadCodigo = form["ciudad"].Elegido<Ciudad>()?.Codigo ?? string.Empty,
                    FechaIngreso = form["fecha"].Fecha ?? DateTime.Today,
                    Estado = t?.Estado ?? Estados.Activo
                };
                await Fondo.Run(() => App.Servicios.Nomina.GuardarTrabajador(emp, datos));
            });

            List<Trabajador> todos = await Fondo.Run(() => App.Servicios.Nomina.Trabajadores(emp));
            TipoDocumento? tipoActual = tipos.Find(x => x.Codigo == (t?.TipoDocumento ?? "CC"));
            CampoFormulario tipo = new CampoFormulario("tipo", "Tipo de documento", TipoCampo.Lista)
            {
                Opciones = tipos,
                Seleccion = tipoActual,
                Ayuda = "El formato del número depende del tipo."
            };
            CampoFormulario numero = new CampoFormulario("numero", "Número de documento", TipoCampo.Texto)
            {
                MaxLongitud = 20,
                Texto = t?.NumeroDocumento ?? string.Empty,
                Ayuda = FormatoDocumento(tipoActual)
            };
            // Formato del tipo elegido y numero que ya tenga otro trabajador (aunque sea con otro tipo de documento)
            numero.Regla = () =>
            {
                TipoDocumento? td = tipo.Elegido<TipoDocumento>();
                string n = numero.ValorTexto.Replace(".", "").Replace(" ", "").ToUpperInvariant();
                if (td != null && !System.Text.RegularExpressions.Regex.IsMatch(n, td.Patron))
                    return "No es un número válido de " + td.Nombre.ToLowerInvariant() + ". " + td.Ayuda;
                Trabajador? repetido = ServicioNomina.DocumentoRepetido(todos, t?.Id ?? 0, n);
                return repetido == null ? null
                    : "Ese número ya es de " + repetido.NombreCompleto + " (" + repetido.TipoDocumento + "). No se puede repetir aunque el tipo sea otro.";
            };
            tipo.Cambio += () =>
            {
                numero.Ayuda = FormatoDocumento(tipo.Elegido<TipoDocumento>());
                numero.Revalidar();
            };
            f.Agregar(tipo);
            f.Agregar(numero);
            f.Agregar(new CampoFormulario("nombre", "Nombres", TipoCampo.Texto) { MaxLongitud = 100, Texto = t?.Nombre ?? string.Empty });
            f.Agregar(new CampoFormulario("apellido", "Apellidos", TipoCampo.Texto) { MaxLongitud = 100, Texto = t?.Apellido ?? string.Empty });
            f.Agregar(new CampoFormulario("cargo", "Cargo", TipoCampo.Lista)
            {
                Opciones = cargos,
                Seleccion = t == null ? (cargos.Count == 1 ? cargos[0] : null) : cargos.Find(x => x.Id == t.CargoId)
            });
            f.Agregar(new CampoFormulario("telefono", "Celular", TipoCampo.Entero) { Requerido = false, MaxLongitud = 10, Texto = t?.Telefono ?? string.Empty, Ayuda = "10 números, sin espacios." });
            f.Agregar(new CampoFormulario("ciudad", "Ciudad de residencia", TipoCampo.ListaPredictiva)
            {
                Requerido = false,
                Opciones = ciudades,
                Seleccion = ciudades.Find(c => c.Codigo == (t?.CiudadCodigo ?? Sesion.Emprendimiento?.CiudadCodigo))
            });
            f.Agregar(new CampoFormulario("fecha", "Fecha de ingreso", TipoCampo.Fecha) { Fecha = t?.FechaIngreso ?? DateTime.Today });
            return f;
        }

        /// <summary>Texto de ayuda con el formato de cada tipo de documento (CC, CE, TI, PA, PPT, PEP).</summary>
        private static string? FormatoDocumento(TipoDocumento? td)
        {
            return td == null ? null : td.Codigo + ": " + td.Ayuda;
        }

        protected override async Task EliminarElemento(Trabajador t)
        {
            int emp = Sesion.IdEmprendimiento;
            await Fondo.Run(() => App.Servicios.Nomina.EliminarTrabajador(emp, t.Id));
        }

        protected override async Task DespuesDeCargar() { await _alCambiar(); }

        [RelayCommand]
        private async Task CambiarEstado()
        {
            if (Seleccionado == null) { Notificador.Error("Selecciona un trabajador."); return; }
            Trabajador t = Seleccionado;
            int emp = Sesion.IdEmprendimiento;
            await Ejecutar(() => Fondo.Run(() => App.Servicios.Nomina.CambiarEstado(emp, t.Id)));
            await Recargar();
        }
    }

    public partial class NominaViewModel : ViewModelBase
    {
        public CargosViewModel Cargos { get; }
        public TrabajadoresViewModel Trabajadores { get; }
        public ObservableCollection<FilaNomina> Detalle { get; } = new ObservableCollection<FilaNomina>();

        [ObservableProperty] private ResumenNomina? _resumen;

        public NominaViewModel()
        {
            Cargos = new CargosViewModel(CargarResumen);
            Trabajadores = new TrabajadoresViewModel(CargarResumen);
        }

        public override async Task Cargar()
        {
            await Cargos.Cargar();
            await Trabajadores.Cargar();
        }

        /// <summary>pkg_sg_nomina: los cinco totales y la lista de nomina.</summary>
        private async Task CargarResumen()
        {
            int emp = Sesion.IdEmprendimiento;
            Resumen = await Fondo.Run(() => App.Servicios.Nomina.Resumen(emp));
            List<FilaNomina> filas = await Fondo.Run(() => App.Servicios.Nomina.Detalle(emp));
            Detalle.Clear();
            foreach (FilaNomina f in filas) Detalle.Add(f);
        }
    }
}
