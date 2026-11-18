/* =====================================================================
   PARTE 3. FUNCIONES (devuelven un valor; las usan las vistas, los paquetes,
   los procedimientos y los disparadores)
   ===================================================================== */

-- 3.1 Deducciones de nomina: 4 % salud + 4 % pension
CREATE OR REPLACE FUNCTION fn_sg_deducciones (
  p_salario IN NUMBER
) RETURN NUMBER
IS
BEGIN
  RETURN ROUND(NVL(p_salario, 0) * 0.08, 2);
END fn_sg_deducciones;
/

-- 3.2 Salario neto = salario - deducciones
CREATE OR REPLACE FUNCTION fn_sg_salario_neto (
  p_salario IN NUMBER
) RETURN NUMBER
IS
BEGIN
  RETURN NVL(p_salario, 0) - fn_sg_deducciones(p_salario);
END fn_sg_salario_neto;
/

-- 3.3 Porcentaje de margen = (venta - compra) / compra * 100
CREATE OR REPLACE FUNCTION fn_sg_porcentaje_margen (
  p_precio_compra IN NUMBER,
  p_precio_venta  IN NUMBER
) RETURN NUMBER
IS
BEGIN
  IF NVL(p_precio_compra, 0) = 0 THEN
    RETURN 0;
  END IF;
  RETURN ROUND((p_precio_venta - p_precio_compra) / p_precio_compra * 100, 2);
END fn_sg_porcentaje_margen;
/

-- 3.4 Total de una venta = suma de cantidad * precio de cada detalle
CREATE OR REPLACE FUNCTION fn_sg_total_venta (
  p_venta_id IN NUMBER
) RETURN NUMBER
IS
  v_total NUMBER;
BEGIN
  SELECT NVL(SUM(cantidad * precio_unitario), 0)
    INTO v_total
    FROM sg_detalle_venta
   WHERE venta_id = p_venta_id;
  RETURN v_total;
END fn_sg_total_venta;
/

-- 3.5 Balance = ingresos (Venta, Ingreso) - gastos (Compra, Gasto)
CREATE OR REPLACE FUNCTION fn_sg_balance (
  p_emprendimiento_id IN NUMBER
) RETURN NUMBER
IS
  v_balance NUMBER;
BEGIN
  SELECT NVL(SUM(CASE WHEN tipo IN ('Venta', 'Ingreso') THEN monto
                      WHEN tipo IN ('Compra', 'Gasto')  THEN -monto
                      ELSE 0 END), 0)
    INTO v_balance
    FROM sg_movimiento
   WHERE emprendimiento_id = p_emprendimiento_id;
  RETURN v_balance;
END fn_sg_balance;
/

-- 3.6 Valida un numero de documento con el patron de su tipo (1 = valido, 0 = no valido)
CREATE OR REPLACE FUNCTION fn_sg_documento_valido (
  p_tipo   IN VARCHAR2,
  p_numero IN VARCHAR2
) RETURN NUMBER
IS
  v_patron sg_tipo_documento.patron%TYPE;
BEGIN
  SELECT patron INTO v_patron FROM sg_tipo_documento WHERE codigo = p_tipo;
  IF REGEXP_LIKE(p_numero, v_patron) THEN
    RETURN 1;
  END IF;
  RETURN 0;
EXCEPTION
  WHEN NO_DATA_FOUND THEN
    RETURN 0;
END fn_sg_documento_valido;
/

-- 3.7 Id de una categoria de sistema (Ventas / Inventario)
CREATE OR REPLACE FUNCTION fn_sg_categoria_sistema (
  p_nombre IN VARCHAR2,
  p_tipo   IN VARCHAR2
) RETURN NUMBER
IS
  v_id sg_categoria_movimiento.id%TYPE;
BEGIN
  SELECT id INTO v_id
    FROM sg_categoria_movimiento
   WHERE nombre = p_nombre AND tipo = p_tipo AND sistema = 'S';
  RETURN v_id;
EXCEPTION
  WHEN NO_DATA_FOUND THEN
    RAISE_APPLICATION_ERROR(-20010, 'Falta la categoría de sistema "' || p_nombre || '". Ejecuta de nuevo los catálogos.');
END fn_sg_categoria_sistema;
/
