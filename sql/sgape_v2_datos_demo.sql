/* =====================================================================
   SGAPE v2 - DATOS DE DEMOSTRACION (opcional)
   Crea el usuario demo@sgape.co con la contrasena Demo2026 y un
   emprendimiento con datos para mostrar la aplicacion y el bot.
   Ejecutar DESPUES de sgape_v2_completo.sql (F5 en SQL Developer).
   ===================================================================== */
SET DEFINE OFF;
SET SERVEROUTPUT ON;

DECLARE
  v_usuario NUMBER;
  v_emp     NUMBER;
  v_vend    NUMBER;
  v_caj     NUMBER;
  v_admin   NUMBER;
  v_venta   NUMBER;
  v_v1      NUMBER;
  v_v2      NUMBER;
  v_vac     NUMBER;
  TYPE t_ids IS TABLE OF NUMBER INDEX BY PLS_INTEGER;
  p t_ids;
  FUNCTION cat(p_nombre VARCHAR2) RETURN NUMBER IS
    v NUMBER;
  BEGIN
    SELECT id INTO v FROM sg_categoria_movimiento WHERE nombre = p_nombre;
    RETURN v;
  END;
  FUNCTION plantilla(p_sector VARCHAR2, p_cargo VARCHAR2) RETURN NUMBER IS
    v NUMBER;
  BEGIN
    SELECT cp.id INTO v FROM sg_cargo_plantilla cp JOIN sg_sector s ON s.id = cp.sector_id
     WHERE s.nombre = p_sector AND cp.nombre = p_cargo;
    RETURN v;
  END;
BEGIN
  INSERT INTO sg_usuario (nombre, email, clave_hash)
  VALUES ('Usuario Demo', 'demo@sgape.co', 'PuHLxOdJR5zGO67N4awp8g==:Wthw1fONZ6ZYyikfm8Y590vKgzIPX4mfoYvy+RKbMh0=')
  RETURNING id INTO v_usuario;

  INSERT INTO sg_emprendimiento (usuario_id, nombre, descripcion, sector_id, ciudad_codigo, fecha_inicio)
  VALUES (v_usuario, 'Tienda La Esquina', 'Víveres y bebidas del barrio',
          (SELECT id FROM sg_sector WHERE nombre = 'Comercio'), '20001', DATE '2025-03-01')
  RETURNING id INTO v_emp;

  INSERT INTO sg_cargo (emprendimiento_id, plantilla_id, nombre, salario_base)
  VALUES (v_emp, plantilla('Comercio', 'Vendedor'), 'Vendedor', 1750905) RETURNING id INTO v_vend;
  INSERT INTO sg_cargo (emprendimiento_id, plantilla_id, nombre, salario_base)
  VALUES (v_emp, plantilla('Comercio', 'Cajero'), 'Cajero', 1800000) RETURNING id INTO v_caj;
  INSERT INTO sg_cargo (emprendimiento_id, plantilla_id, nombre, salario_base)
  VALUES (v_emp, plantilla('Comercio', 'Administrador de tienda'), 'Administrador de tienda', 2700000) RETURNING id INTO v_admin;

  INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, telefono, ciudad_codigo, fecha_ingreso)
  VALUES (v_emp, v_admin, 'CC', '1065123456', 'Laura', 'Martínez', '3001234567', '20001', DATE '2025-03-01');
  INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, telefono, ciudad_codigo, fecha_ingreso)
  VALUES (v_emp, v_vend, 'CC', '1065987654', 'Carlos', 'Gómez', '3109876543', '20001', DATE '2025-05-10');
  INSERT INTO sg_trabajador (emprendimiento_id, cargo_id, tipo_documento, numero_documento, nombre, apellido, telefono, ciudad_codigo, fecha_ingreso)
  VALUES (v_emp, v_caj, 'PPT', '4567891', 'María', 'Rodríguez', '3205554433', '20001', DATE '2025-08-01');

  INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo)
  VALUES (v_emp, 'Gaseosa 400 ml', 'Bebidas', 1200, 2000, 60, 15) RETURNING id INTO p(1);
  INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo)
  VALUES (v_emp, 'Agua 600 ml', 'Bebidas', 800, 1500, 40, 10) RETURNING id INTO p(2);
  INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo)
  VALUES (v_emp, 'Arroz 1 kg', 'Granos', 3200, 4500, 30, 10) RETURNING id INTO p(3);
  INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo)
  VALUES (v_emp, 'Aceite 1 L', 'Abarrotes', 7800, 10500, 6, 8) RETURNING id INTO p(4);
  INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo)
  VALUES (v_emp, 'Jabón en barra', 'Aseo', 900, 1600, 25, 10) RETURNING id INTO p(5);
  INSERT INTO sg_producto (emprendimiento_id, nombre, categoria, precio_compra, precio_venta, stock, stock_minimo)
  VALUES (v_emp, 'Café 250 g', 'Abarrotes', 6500, 9000, 4, 5) RETURNING id INTO p(6);

  -- Ventas por el procedimiento (descuenta stock y registra el ingreso)
  sp_sg_registrar_venta(v_emp, 'Cliente frecuente', '[{"producto_id":' || p(1) || ',"cantidad":6},{"producto_id":' || p(3) || ',"cantidad":2}]', v_v2);
  sp_sg_registrar_venta(v_emp, NULL, '[{"producto_id":' || p(2) || ',"cantidad":4},{"producto_id":' || p(5) || ',"cantidad":3}]', v_v1);
  sp_sg_registrar_venta(v_emp, 'Domicilio', '[{"producto_id":' || p(1) || ',"cantidad":10},{"producto_id":' || p(4) || ',"cantidad":1}]', v_venta);
  -- Ventas de meses anteriores para la grafica
  UPDATE sg_venta SET fecha = ADD_MONTHS(SYSDATE, -1) WHERE id = v_v1;
  UPDATE sg_venta SET fecha = ADD_MONTHS(SYSDATE, -2) WHERE id = v_v2;
  UPDATE sg_movimiento SET fecha = ADD_MONTHS(SYSDATE, -1) WHERE referencia = 'venta-' || v_v1;
  UPDATE sg_movimiento SET fecha = ADD_MONTHS(SYSDATE, -2) WHERE referencia = 'venta-' || v_v2;

  INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto)
  VALUES (v_emp, TRUNC(SYSDATE, 'MM') + 1, 'Gasto', cat('Arriendo'), 'Arriendo del local', 900000);
  INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto)
  VALUES (v_emp, TRUNC(SYSDATE, 'MM') + 4, 'Gasto', cat('Servicios públicos'), 'Energía y agua', 210000);
  INSERT INTO sg_movimiento (emprendimiento_id, fecha, tipo, categoria_id, descripcion, monto)
  VALUES (v_emp, TRUNC(SYSDATE, 'MM') + 2, 'Ingreso', cat('Aportes de socios'), 'Aporte inicial del mes', 500000);

  INSERT INTO sg_presupuesto (emprendimiento_id, categoria_id, monto_limite, mes)
  VALUES (v_emp, cat('Servicios públicos'), 250000, TO_CHAR(SYSDATE, 'YYYY-MM'));
  INSERT INTO sg_presupuesto (emprendimiento_id, categoria_id, monto_limite, mes)
  VALUES (v_emp, cat('Arriendo'), 900000, TO_CHAR(SYSDATE, 'YYYY-MM'));
  INSERT INTO sg_presupuesto (emprendimiento_id, categoria_id, monto_limite, mes)
  VALUES (v_emp, cat('Marketing'), 150000, TO_CHAR(SYSDATE, 'YYYY-MM'));

  INSERT INTO sg_vacante (emprendimiento_id, cargo_id, titulo, descripcion, ciudad_codigo, fecha_cierre)
  VALUES (v_emp, v_vend, 'Vendedor de mostrador', 'Atención al cliente y manejo de caja. Medio tiempo los fines de semana.',
          '20001', TRUNC(SYSDATE) + 30)
  RETURNING id INTO v_vac;

  COMMIT;
  DBMS_OUTPUT.PUT_LINE('Listo. Inicia sesión con demo@sgape.co / Demo2026');
END;
/
