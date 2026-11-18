/* =====================================================================
   PARTE 8. VERIFICACION Y PRUEBAS
   Las pruebas usan datos temporales y terminan con ROLLBACK: no dejan nada.
   ===================================================================== */
SET SERVEROUTPUT ON;

-- 8.1 Todos los objetos deben estar VALID
SELECT object_type, object_name, status
  FROM user_objects
 WHERE (object_name LIKE 'SG\_%' ESCAPE '\' OR object_name LIKE 'VW\_SG\_%' ESCAPE '\'
     OR object_name LIKE 'FN\_SG\_%' ESCAPE '\' OR object_name LIKE 'SP\_SG\_%' ESCAPE '\'
     OR object_name LIKE 'PKG\_SG\_%' ESCAPE '\' OR object_name LIKE 'TRG\_SG\_%' ESCAPE '\')
   AND object_type IN ('TABLE', 'VIEW', 'FUNCTION', 'PROCEDURE', 'PACKAGE', 'PACKAGE BODY', 'TRIGGER')
 ORDER BY object_type, object_name;

-- 8.2 Cantidades de los catalogos
SELECT 'departamentos' AS catalogo, COUNT(*) AS filas FROM sg_departamento UNION ALL
SELECT 'ciudades',        COUNT(*) FROM sg_ciudad UNION ALL
SELECT 'sectores',        COUNT(*) FROM sg_sector UNION ALL
SELECT 'cargos sugeridos',COUNT(*) FROM sg_cargo_plantilla UNION ALL
SELECT 'tipos documento', COUNT(*) FROM sg_tipo_documento UNION ALL
SELECT 'categorias',      COUNT(*) FROM sg_categoria_movimiento UNION ALL
SELECT 'conocimiento',    COUNT(*) FROM sg_conocimiento;

-- 8.3 Pruebas de reglas (todo se deshace al final)
DECLARE
  v_usuario  NUMBER;
  v_emp      NUMBER;
  v_cargo    NUMBER;
  v_prod     NUMBER;
  v_venta    NUMBER;
  v_cursor   SYS_REFCURSOR;
  v_nombre   VARCHAR2(150);
  v_stock    NUMBER;
  v_minimo   NUMBER;
  v_faltante NUMBER;
  v_cat      VARCHAR2(150);
  v_pid      NUMBER;
  PROCEDURE ok(p_texto VARCHAR2) IS BEGIN DBMS_OUTPUT.PUT_LINE('OK    ' || p_texto); END;
  PROCEDURE falla(p_texto VARCHAR2) IS BEGIN DBMS_OUTPUT.PUT_LINE('FALLA ' || p_texto); END;
BEGIN
  SAVEPOINT pruebas;

  INSERT INTO sg_usuario (nombre, email, clave_hash) VALUES ('  Prueba ', '  PRUEBA@SGAPE.CO ', 'x:y')
  RETURNING id INTO v_usuario;
  SELECT email INTO v_nombre FROM sg_usuario WHERE id = v_usuario;
  IF v_nombre = 'prueba@sgape.co' THEN ok('trg_sg_usuario_email normaliza el correo'); ELSE falla('correo'); END IF;

  INSERT INTO sg_emprendimiento (usuario_id, nombre, sector_id, ciudad_codigo, fecha_inicio)
  VALUES (v_usuario, 'Tienda de prueba', (SELECT id FROM sg_sector WHERE nombre = 'Comercio'), '20001', SYSDATE)
  RETURNING id INTO v_emp;

  INSERT INTO sg_cargo (emprendimiento_id, nombre, salario_base) VALUES (v_emp, 'Vendedor', 1750905)
  RETURNING id INTO v_cargo;

  -- Documento valido (con puntos: el disparador los quita)
  INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, fecha_ingreso)
  VALUES (v_emp, v_cargo, 'CC', '1.065.123.456', 'Ana', 'Pérez', SYSDATE);
  ok('trabajador con CC valida');

  -- Documento invalido para el tipo
  BEGIN
    INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, fecha_ingreso)
    VALUES (v_emp, v_cargo, 'CC', 'ABC12', 'Luis', 'Gómez', SYSDATE);
    falla('acepto una CC con letras');
  EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -20111 THEN ok('trg_sg_trabajador_valida rechaza CC con letras'); ELSE RAISE; END IF;
  END;

  IF pkg_sg_nomina.nomina_neta(v_emp) = 1750905 - ROUND(1750905 * 0.08, 2) THEN
    ok('pkg_sg_nomina.nomina_neta');
  ELSE falla('nomina neta'); END IF;

  INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo)
  VALUES (v_emp, 'Gaseosa', 'Bebidas', 1200, 2000, 5, 3) RETURNING id INTO v_prod;

  -- Venta correcta: 3 unidades (en dos lineas del JSON, se agrupan)
  sp_sg_registrar_venta(v_emp, 'Cliente de prueba',
                        '[{"producto_id":' || v_prod || ',"cantidad":2},{"producto_id":' || v_prod || ',"cantidad":1}]',
                        v_venta);
  SELECT stock INTO v_stock FROM sg_producto WHERE id = v_prod;
  IF v_stock = 2 AND fn_sg_total_venta(v_venta) = 6000 AND fn_sg_balance(v_emp) = 6000 THEN
    ok('sp_sg_registrar_venta: stock, total y balance');
  ELSE falla('venta: stock=' || v_stock); END IF;

  -- Venta con stock insuficiente: no debe cambiar nada
  BEGIN
    sp_sg_registrar_venta(v_emp, NULL, '[{"producto_id":' || v_prod || ',"cantidad":50}]', v_venta);
    falla('vendio sin stock');
  EXCEPTION WHEN OTHERS THEN
    v_minimo := SQLCODE;
    SELECT stock INTO v_stock FROM sg_producto WHERE id = v_prod;
    IF v_minimo = -20204 AND v_stock = 2 THEN ok('venta sin stock se rechaza y no deja rastro'); ELSE RAISE; END IF;
  END;

  -- Stock bajo (2 <= 3)
  sp_sg_productos_stock_bajo(v_emp, v_cursor);
  FETCH v_cursor INTO v_pid, v_nombre, v_cat, v_stock, v_minimo, v_faltante;
  IF v_cursor%FOUND AND v_nombre = 'Gaseosa' THEN ok('sp_sg_productos_stock_bajo'); ELSE falla('stock bajo'); END IF;
  CLOSE v_cursor;

  -- Presupuesto en una categoria de INGRESO: debe fallar
  BEGIN
    INSERT INTO sg_presupuesto (emprendimiento_id, categoria_id, monto_limite, mes)
    VALUES (v_emp, fn_sg_categoria_sistema('Ventas', 'Ingreso'), 100000, TO_CHAR(SYSDATE, 'YYYY-MM'));
    falla('presupuesto en categoria de ingreso');
  EXCEPTION WHEN OTHERS THEN
    IF SQLCODE = -20102 THEN ok('trg_sg_presupuesto_valida rechaza categoria de ingreso'); ELSE RAISE; END IF;
  END;

  -- Telegram: codigo y vinculo
  DECLARE
    v_codigo VARCHAR2(6);
    v_u NUMBER;
    v_n VARCHAR2(100);
  BEGIN
    v_codigo := pkg_sg_telegram.generar_codigo(v_usuario);
    pkg_sg_telegram.vincular(999000111, v_codigo, v_u, v_n);
    IF v_u = v_usuario AND pkg_sg_telegram.emprendimiento_de_chat(999000111) = v_emp THEN
      ok('pkg_sg_telegram.vincular');
    ELSE falla('vinculo telegram'); END IF;
  END;

  ROLLBACK TO pruebas;
  DBMS_OUTPUT.PUT_LINE('Pruebas terminadas. Se deshicieron todos los datos de prueba.');
EXCEPTION
  WHEN OTHERS THEN
    ROLLBACK TO pruebas;
    DBMS_OUTPUT.PUT_LINE('ERROR EN LAS PRUEBAS: ' || SQLERRM);
END;
/
