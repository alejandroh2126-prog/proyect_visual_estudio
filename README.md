# SGAPE

**Sistema de Gestión y Administración de Emprendimientos** — aplicación de escritorio para Windows hecha en **C# / .NET (Windows Forms)** con **arquitectura en capas**.

Universidad Popular del Cesar · Ingeniería de Sistemas · 2026

## ¿Qué puede hacer?

| Módulo | Funciones |
|---|---|
| Cuenta | Registro e inicio de sesión (contraseñas con PBKDF2 + sal) |
| Emprendimientos | Crear, editar y eliminar (con todos sus datos) |
| Nómina | Cargos, trabajadores, deducciones (salud 4 % + pensión 4 %), nómina quincenal/mensual/neta |
| Inventario | Productos, entradas y salidas de stock, alertas de stock bajo |
| Ventas | Carrito, descuento automático de stock, historial y factura en texto |
| Finanzas | Ingresos y gastos por categoría, balance e historial de movimientos |
| Presupuestos | Límite mensual por categoría con alerta desde el 80 % |
| Reportes | Estadísticas y reporte HTML con gráficas |
| Asistente | Preguntas frecuentes sobre la plataforma y la administración de un negocio |

## Arquitectura

```
SGAPE.slnx
├── Entidades/      → Qué ES cada cosa (Persona, Trabajador, Producto, Venta...). Sin dependencias.
├── Logica/         → Qué HACE el sistema (servicios, reglas de negocio, repositorios CSV).
│   ├── Repositorios/   guardar y leer datos (RepositorioCsv<T> + uno por entidad)
│   ├── Servicios/      un servicio por tarea (Autenticación, Nómina, Inventario, Ventas...)
│   └── Utilidades/     validaciones, formatos, CSV seguro, rutas, sesión
└── Presentacion/   → Lo que VE el usuario (Windows Forms, sin Designer).
    ├── Comun/          tema, tarjetas, mensajes, VistaBase, DialogoBase
    ├── Vistas/         una vista por módulo
    └── Dialogos/       formularios emergentes
```

Referencias entre proyectos: `Presentacion → Logica → Entidades` (y `Presentacion → Entidades`).
La capa de presentación **nunca** toca archivos: solo habla con `FabricaServicios`.

### Pilares y principios aplicados

- **Abstracción**: `Persona` (clase abstracta), `RepositorioCsv<T>`, `VistaBase`, `DialogoBase`.
- **Encapsulamiento**: propiedades con validación (`Persona`), `Producto.Stock` solo cambia con `AgregarStock/ReducirStock`.
- **Herencia**: `Trabajador : Persona`, repositorios que heredan de `RepositorioCsv<T>`, vistas que heredan de `VistaBase`.
- **Polimorfismo**: `Persona.TipoPersona`, `Serializar/Deserializar` de cada repositorio, `Recargar()` de cada vista.
- **SRP (una clase, una misión)**: cada servicio y cada vista tiene una única razón para cambiar.
- **Bajo acoplamiento / alta cohesión**: dependencias entregadas por constructor y ensambladas solo en `FabricaServicios`.

## Datos

Se guardan en archivos CSV en `%LOCALAPPDATA%\SGAPE\datos`. Las facturas y reportes se crean en `Documentos\SGAPE`.
Desinstalar la aplicación **no** borra tus datos.

## Compilar y ejecutar

1. Visual Studio Community con la carga de trabajo **Desarrollo de escritorio con .NET**.
2. Abrir `SGAPE.slnx`, establecer **Presentacion** como proyecto de inicio y pulsar **F5**.

## Generar el instalador

1. Doble clic en `publicar.bat` → crea `Publicado\SGAPE.exe` (incluye .NET; no hay que instalarlo aparte).
2. Abrir `Instalador\SGAPE.iss` con [Inno Setup](https://jrsoftware.org/isinfo.php) y pulsar **Compile**.
3. El instalador queda en `Instalador\Salida\`.

## Autores

Alejandro Henríquez Quinchia y colaboradores · Universidad Popular del Cesar