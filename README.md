# Validaciones agregadas

Este ajuste introduce una capa de validaciones en la API GraphQL para proteger los datos ingresados por usuarios, productores, administradores y comprobadores.

## Reglas generales
- Todos los correos se validan con formato y son obligatorios cuando corresponde (registro, login, ediciones).
- Cualquier campo que represente un número (teléfonos, CI, NIT, número de cuenta, número de transacción, etc.) solo acepta dígitos y tiene límites de longitud.
- Precios, cantidades y stock no aceptan valores negativos y su parte entera se limita a 15 dígitos para evitar números irreales.
- Listas críticas, como los detalles de una venta, deben llegar con al menos un elemento válido.

## Mutaciones impactadas
1. `Login` y `CrearUsuario`: correo obligatorio, formato validado y teléfonos solo numéricos (7-15 dígitos).
2. `ActualizarUsuario` y `EditarUsuarioAdmin`: cuando se actualiza el teléfono o el correo se vuelve a validar el dato.
3. `ConvertirseComprobador`, `CrearComprobadorAdmin` y `EditarComprobadorAdmin`: el CI debe contener solo números y respetar el límite de dígitos.
4. `ConvertirEnProductor`: dirección/banco obligatorios y datos numéricos (NIT y cuenta) restringidos a dígitos.
5. `CrearProducto` y `EditarProducto`: textos obligatorios y controles de rango para precios, stock y cantidades mayoristas.
6. `CrearDetalleVenta`, `CrearVenta` y `EditarVentaAdmin`: se fuerza que los detalles tengan cantidades positivas y que el número de transacción sea numérico y razonable.
7. Mutaciones admin (`EditarProductoAdmin`, etc.) reutilizan las mismas reglas antes de persistir cambios.

## Pruebas sugeridas
- Intentar registrar un usuario sin correo o con correo inválido para confirmar que se rechaza.
- Crear/editar productos con precios negativos o stocks enormes para validar el bloqueo.
- Confirmar que una venta no se crea con cantidades 0 o con número de transacción alfanumérico.
- Probar que los teléfonos, CI, NIT o cuentas con letras o más de 15 dígitos son rechazados.

Estas validaciones se implementan en `GraphQL/Mutation.cs`, por lo que cualquier llamada GraphQL que use las mutaciones mencionadas contará con las nuevas protecciones sin cambios adicionales en los clientes.