using System;
using System.Collections.Generic;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: el historial de movimientos y los totales de dinero.
    /// Inventario y Ventas lo usan para dejar rastro, sin conocer como se guarda.
    /// </summary>
    public class ServicioFinanzas
    {
        private readonly RepositorioMovimientos _movimientos;

        public ServicioFinanzas(RepositorioMovimientos movimientos)
        {
            _movimientos = movimientos;
        }

        public List<Movimiento> Listar(int emprendimientoId)
        {
            List<Movimiento> lista = _movimientos.Listar().FindAll(m => m.EmprendimientoId == emprendimientoId);
            lista.Sort((a, b) => b.Fecha.CompareTo(a.Fecha));
            return lista;
        }

        public Movimiento Registrar(int emprendimientoId, TipoMovimiento tipo, string categoria, string descripcion,
                                    decimal monto, string referencia)
        {
            Validar.Requerido(descripcion, "Descripción");
            Validar.NoNegativo(monto, "Monto");

            Movimiento m = new Movimiento
            {
                EmprendimientoId = emprendimientoId,
                Fecha = DateTime.Now,
                Tipo = tipo,
                Categoria = categoria ?? string.Empty,
                Descripcion = descripcion.Trim(),
                Monto = monto,
                Referencia = referencia ?? string.Empty
            };
            return _movimientos.Insertar(m);
        }

        /// <summary>Ingreso o gasto escrito a mano por el usuario.</summary>
        public Movimiento RegistrarManual(int emprendimientoId, bool esIngreso, string categoria, string descripcion, decimal monto)
        {
            Validar.Requerido(categoria, "Categoría");
            Validar.MayorQueCero(monto, "Monto");
            TipoMovimiento tipo = esIngreso ? TipoMovimiento.Ingreso : TipoMovimiento.Gasto;
            return Registrar(emprendimientoId, tipo, categoria, descripcion, monto, "manual");
        }

        /// <summary>Solo se pueden borrar los movimientos manuales; los de ventas/compras son automaticos.</summary>
        public void EliminarManual(int movimientoId)
        {
            Movimiento? m = _movimientos.ObtenerPorId(movimientoId);
            if (m == null) return;
            if (m.Tipo != TipoMovimiento.Ingreso && m.Tipo != TipoMovimiento.Gasto)
                throw new ReglaDeNegocioException("Solo se pueden eliminar ingresos y gastos manuales.");
            _movimientos.Eliminar(movimientoId);
        }

        public ResumenFinanciero CalcularResumen(int emprendimientoId)
        {
            ResumenFinanciero r = new ResumenFinanciero();
            foreach (Movimiento m in Listar(emprendimientoId))
            {
                if (m.EsIngreso) r.TotalIngresos += m.Monto;
                else if (m.EsGasto) r.TotalGastos += m.Monto;
            }
            return r;
        }
    }
}