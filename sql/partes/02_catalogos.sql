/* =====================================================================
   PARTE 2. DATOS DE LOS CATALOGOS
   ===================================================================== */
SET DEFINE OFF;

-- 2.1 Tipos de documento (patron = expresion regular que valida el numero)
INSERT INTO sg_tipo_documento (codigo, nombre, patron, ayuda) VALUES ('CC',  'Cédula de ciudadanía',              '^[1-9][0-9]{5,9}$',  'Solo números, entre 6 y 10 dígitos, sin puntos.');
INSERT INTO sg_tipo_documento (codigo, nombre, patron, ayuda) VALUES ('CE',  'Cédula de extranjería',             '^[0-9]{6,10}$',      'Solo números, entre 6 y 10 dígitos.');
INSERT INTO sg_tipo_documento (codigo, nombre, patron, ayuda) VALUES ('TI',  'Tarjeta de identidad',              '^[0-9]{10,11}$',     'Solo números, 10 u 11 dígitos.');
INSERT INTO sg_tipo_documento (codigo, nombre, patron, ayuda) VALUES ('PA',  'Pasaporte',                         '^[A-Z0-9]{5,15}$',   'Letras mayúsculas y números, entre 5 y 15 caracteres.');
INSERT INTO sg_tipo_documento (codigo, nombre, patron, ayuda) VALUES ('PPT', 'Permiso por Protección Temporal',   '^[0-9]{6,10}$',      'Solo números, entre 6 y 10 dígitos.');
INSERT INTO sg_tipo_documento (codigo, nombre, patron, ayuda) VALUES ('PEP', 'Permiso Especial de Permanencia',   '^[0-9]{6,15}$',      'Solo números, entre 6 y 15 dígitos.');

-- 2.2 Categorias de movimientos (sistema = 'S': las usa el programa automaticamente)
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Ventas',               'Ingreso', 'S');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Servicios prestados',  'Ingreso', 'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Aportes de socios',    'Ingreso', 'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Préstamos recibidos',  'Ingreso', 'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Otros ingresos',       'Ingreso', 'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Inventario',           'Gasto',   'S');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Nómina',               'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Arriendo',             'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Servicios públicos',   'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Internet y telefonía', 'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Marketing',            'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Transporte',           'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Impuestos',            'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Mantenimiento',        'Gasto',   'N');
INSERT INTO sg_categoria_movimiento (nombre, tipo, sistema) VALUES ('Otros gastos',         'Gasto',   'N');

-- 2.3 Sectores y cargos sugeridos de cada sector
--     Los salarios son sugerencias editables: el usuario los ajusta al crear el cargo.
DECLARE
  PROCEDURE sector(p_nombre VARCHAR2, p_cargos sys.odcivarchar2list, p_salarios sys.odcinumberlist) IS
    v_id NUMBER;
  BEGIN
    INSERT INTO sg_sector (nombre) VALUES (p_nombre) RETURNING id INTO v_id;
    FOR i IN 1 .. p_cargos.COUNT LOOP
      INSERT INTO sg_cargo_plantilla (sector_id, nombre, salario_sugerido)
      VALUES (v_id, p_cargos(i), p_salarios(i));
    END LOOP;
  END;
BEGIN
  sector('Agricultura',
         sys.odcivarchar2list('Administrador de finca', 'Operario agrícola', 'Técnico agropecuario', 'Recolector', 'Conductor'),
         sys.odcinumberlist(2800000, 1750905, 2300000, 1750905, 1900000));
  sector('Alimentos y bebidas',
         sys.odcivarchar2list('Administrador', 'Cocinero', 'Auxiliar de cocina', 'Mesero', 'Cajero', 'Domiciliario', 'Panadero'),
         sys.odcinumberlist(2800000, 2100000, 1750905, 1750905, 1750905, 1750905, 1950000));
  sector('Comercio',
         sys.odcivarchar2list('Administrador de tienda', 'Vendedor', 'Cajero', 'Bodeguero', 'Auxiliar contable', 'Domiciliario'),
         sys.odcinumberlist(2700000, 1750905, 1750905, 1800000, 2100000, 1750905));
  sector('Construcción',
         sys.odcivarchar2list('Maestro de obra', 'Oficial de construcción', 'Ayudante de obra', 'Almacenista', 'Residente de obra'),
         sys.odcinumberlist(3200000, 2300000, 1750905, 1900000, 3800000));
  sector('Educación',
         sys.odcivarchar2list('Coordinador académico', 'Docente', 'Auxiliar pedagógico', 'Secretaria', 'Servicios generales'),
         sys.odcinumberlist(3200000, 2600000, 1850000, 1850000, 1750905));
  sector('Moda y textiles',
         sys.odcivarchar2list('Diseñador', 'Patronista', 'Operario de confección', 'Vendedor', 'Cajero'),
         sys.odcinumberlist(2800000, 2300000, 1750905, 1750905, 1750905));
  sector('Salud y belleza',
         sys.odcivarchar2list('Estilista', 'Manicurista', 'Esteticista', 'Recepcionista', 'Barbero'),
         sys.odcinumberlist(2000000, 1750905, 2100000, 1750905, 1900000));
  sector('Servicios',
         sys.odcivarchar2list('Coordinador de servicio', 'Técnico', 'Auxiliar administrativo', 'Recepcionista', 'Servicios generales'),
         sys.odcinumberlist(2700000, 2200000, 1850000, 1750905, 1750905));
  sector('Tecnología',
         sys.odcivarchar2list('Desarrollador de software', 'Soporte técnico', 'Diseñador gráfico', 'Community manager', 'Analista de datos'),
         sys.odcinumberlist(4200000, 2300000, 2600000, 2200000, 3800000));
  sector('Transporte y logística',
         sys.odcivarchar2list('Conductor', 'Auxiliar de bodega', 'Despachador', 'Mensajero', 'Coordinador logístico'),
         sys.odcinumberlist(2100000, 1750905, 1900000, 1750905, 3000000));
  sector('Turismo',
         sys.odcivarchar2list('Guía turístico', 'Recepcionista', 'Camarero de habitación', 'Asesor de viajes', 'Administrador de hotel'),
         sys.odcinumberlist(2100000, 1750905, 1750905, 2000000, 3200000));
  sector('Otro',
         sys.odcivarchar2list('Administrador', 'Auxiliar administrativo', 'Vendedor', 'Operario'),
         sys.odcinumberlist(2600000, 1850000, 1750905, 1750905));
END;
/
