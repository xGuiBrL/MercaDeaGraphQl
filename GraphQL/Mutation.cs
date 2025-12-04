using HotChocolate;
using HotChocolate.Authorization;
using MercaDeaGraphQl.Inputs;
using MercaDeaGraphQl.Models;
using MercaDeaGraphQl.Models.Data;
using MercaDeaGraphQl.Services;
using Microsoft.AspNetCore.Hosting;
using MongoDB.Driver;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using IOPath = System.IO.Path;


namespace MercaDeaGraphQl.GraphQL
{
    public class Mutation
    {
        private const int MaxNumericDigits = 15;
        private static readonly EmailAddressAttribute EmailValidator = new();
        private static readonly Regex DigitsOnlyRegex = new("^[0-9]+$", RegexOptions.Compiled);

        [AllowAnonymous]
        public async Task<string> Login(
            [Service] MongoDbContext db,
            [Service] JwtService jwt,
            string correo,
            string password)
        {
            ValidarCorreo(correo);
            if (string.IsNullOrWhiteSpace(password))
                throw new Exception("La contraseña es obligatoria.");

            var usuario = await db.Usuarios.Find(u => u.Correo == correo).FirstOrDefaultAsync();
            var mensajeError = "Correo o contraseña incorrectos.";

            if (usuario == null)
                throw new Exception(mensajeError);

            if (usuario.Password != password)
                throw new Exception(mensajeError);

            return jwt.GenerateToken(usuario);
        }

        [Authorize]
        public async Task<Usuario> ActualizarUsuario(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            ActualizarUsuarioInput input)
        {
            if (input == null)
            {
                throw new Exception("Entrada inválida.");
            }

            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
                throw new Exception("Token inválido.");

            var updates = new List<UpdateDefinition<Usuario>>();

            if (!string.IsNullOrWhiteSpace(input.Nombre))
            {
                updates.Add(Builders<Usuario>.Update.Set(u => u.Nombre, input.Nombre));
            }

            if (!string.IsNullOrWhiteSpace(input.Apellido))
            {
                updates.Add(Builders<Usuario>.Update.Set(u => u.Apellido, input.Apellido));
            }

            if (input.Telefono != null)
            {
                ValidarNumeroEnTexto(input.Telefono, "Teléfono", 7, 15, esOpcional: true);
                updates.Add(Builders<Usuario>.Update.Set(u => u.telefono, input.Telefono));
            }

            if (updates.Count == 0)
            {
                var actual = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();
                if (actual == null)
                    throw new Exception("Usuario no encontrado.");
                return actual;
            }

            var combined = Builders<Usuario>.Update.Combine(updates);
            await db.Usuarios.UpdateOneAsync(u => u.Id == usuarioId, combined);

            var actualizado = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();
            if (actualizado == null)
                throw new Exception("Usuario no encontrado.");

            return actualizado;
        }

        [Authorize(Roles = new[] {"comprobador"})]
        public async Task<Comprobador> liberarComprobador([Service] MongoDbContext db, bool cambiarEstado, ClaimsPrincipal user)
        {

            var IdComprobador = user.FindFirst("id")?.Value;
            if (IdComprobador == null) throw new Exception("Usuario no encontrado");

            var actualizado = Builders<Comprobador>.Update
                .Set(c => c.CuposDisponibles, cambiarEstado ? Comprobador.CuposMaximos : 0);


            await db.Comprobadores.UpdateOneAsync(c => c.Id == IdComprobador, actualizado);
            return await db.Comprobadores.Find(c=>c.Id==IdComprobador).FirstOrDefaultAsync();
               
        }

        [Authorize]
        public async Task<Comprobador> ConvertirseComprobador(
                        ClaimsPrincipal user,
                        [Service] MongoDbContext db,
                        string CI)
        {
            ValidarNumeroEnTexto(CI, "CI", 5, MaxNumericDigits);

            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
                throw new Exception("Token inválido.");

            var usuario = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();
            
            if (usuario == null)
                throw new Exception("Usuario no encontrado en la base de datos.");

            var existente = await db.Comprobadores
                .Find(p => p.UsuarioId == usuarioId)
                .FirstOrDefaultAsync();

            if (existente != null)
                throw new Exception("Este usuario ya es comprobador.");

            var nuevoComprobador = new Comprobador
            {
                UsuarioId = usuarioId,
                NombreUsuarioBson = usuario.Nombre,
                CI = CI,
                CuposDisponibles = Comprobador.CuposMaximos
            };

            await db.Comprobadores.InsertOneAsync(nuevoComprobador);

            var update = Builders<Usuario>.Update.AddToSet("Roles", "comprobador");
            await db.Usuarios.UpdateOneAsync(u => u.Id == usuarioId, update);

            var comprobadorCreado = await db.Comprobadores
                .Find(c => c.UsuarioId == usuarioId)
                .FirstOrDefaultAsync();

            return comprobadorCreado;
        }





        [Authorize]  
        public async Task<Productor> ConvertirEnProductor(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string direccion,
            string nit,
            string numeroCuenta,
            string banco)
        {
            ValidarCampoTexto(direccion, "Dirección");
            ValidarCampoTexto(banco, "Banco");
            ValidarNumeroEnTexto(nit, "NIT", 6, MaxNumericDigits);
            ValidarNumeroEnTexto(numeroCuenta, "Número de cuenta", 6, MaxNumericDigits);

            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
                throw new Exception("Token inválido.");

            var nombreUsuario = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();

            var existente = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (existente != null)
                throw new Exception("Este usuario ya es productor.");

            var nuevoProductor = new Productor
            {
                IdUsuario = usuarioId,
                NombreUsuario=nombreUsuario.Nombre,
                Direccion = direccion,
                Nit = nit,
                NumeroCuenta = numeroCuenta,
                Banco = banco
            };

            await db.Productores.InsertOneAsync(nuevoProductor);

            var update = Builders<Usuario>.Update.AddToSet("Roles", "productor");
            await db.Usuarios.UpdateOneAsync(u => u.Id == usuarioId, update);

            return nuevoProductor;
        }

        [Authorize(Roles = new[] { "productor" })]
        public async Task<Productor> EditarProductor(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            EditarProductorInput input)
        {
            if (input == null)
                throw new Exception("Entrada inválida.");

            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
                throw new Exception("Token inválido.");

            var productor = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (productor == null)
                throw new Exception("No eres un productor registrado.");

            var updates = new List<UpdateDefinition<Productor>>();

            if (!string.IsNullOrWhiteSpace(input.NombreUsuario))
            {
                ValidarCampoTexto(input.NombreUsuario, "Nombre del productor", 200, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.NombreUsuario, input.NombreUsuario));
            }

            if (!string.IsNullOrWhiteSpace(input.Direccion))
            {
                ValidarCampoTexto(input.Direccion, "Dirección", 200, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.Direccion, input.Direccion));
            }

            if (!string.IsNullOrWhiteSpace(input.Nit))
            {
                ValidarNumeroEnTexto(input.Nit, "NIT", 6, MaxNumericDigits, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.Nit, input.Nit));
            }

            if (!string.IsNullOrWhiteSpace(input.NumeroCuenta))
            {
                ValidarNumeroEnTexto(input.NumeroCuenta, "Número de cuenta", 6, MaxNumericDigits, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.NumeroCuenta, input.NumeroCuenta));
            }

            if (!string.IsNullOrWhiteSpace(input.Banco))
            {
                ValidarCampoTexto(input.Banco, "Banco", 200, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.Banco, input.Banco));
            }

            if (updates.Count == 0)
                return productor;

            var combined = Builders<Productor>.Update.Combine(updates);

            var updated = await db.Productores.FindOneAndUpdateAsync(
                Builders<Productor>.Filter.Eq(p => p.Id, productor.Id),
                combined,
                new FindOneAndUpdateOptions<Productor> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
                throw new Exception("No se pudo actualizar al productor.");

            return updated;
        }

        [Authorize(Roles = new[] { "productor" })]
        public async Task<Producto> CrearProducto(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            CrearProductoInput input)
        {
            ValidarProductoInput(input);
           
            var usuarioId = user.FindFirst("id")?.Value;

            var productor = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (productor == null)
                throw new Exception("No eres un productor registrado.");

            var imagenesLimitadas = (input.Imagenes ?? new List<string>()).Take(4).ToList();

            var nuevo = new Producto
            {
                ProductorId = productor.Id!,
                Nombre = input.Nombre,
                Descripcion = input.Descripcion,
                Atributos = input.Atributo ?? new List<string>(),
                Imagenes = imagenesLimitadas,
                PrecioActual = input.PrecioActual,
                PrecioMayorista = input.PrecioMayorista,
                CantidadMinimaMayorista = input.CantidadMinimaMayorista,
                UnidadMedida = input.UnidadMedida,
                Categoria = input.Categoria,
                Stock = input.Stock
            };

            await db.Productos.InsertOneAsync(nuevo);

            return nuevo;
        }

        [Authorize(Roles = new[] { "productor" })]
        public async Task<Producto> EditarProducto(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string productoId,
            CrearProductoInput input)
        {
            ValidarProductoInput(input);

            var usuarioId = user.FindFirst("id")?.Value;

            var productor = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (productor == null)
                throw new Exception("No eres un productor.");

            var producto = await db.Productos
                .Find(p => p.Id == productoId)
                .FirstOrDefaultAsync();

            if (producto == null)
                throw new Exception("Producto no encontrado.");

            if (producto.ProductorId != productor.Id)
                throw new Exception("No puedes modificar productos de otro productor.");

            var update = Builders<Producto>.Update
                .Set(p => p.Nombre, input.Nombre)
                .Set(p => p.Descripcion, input.Descripcion)
                .Set(p => p.Atributos, input.Atributo ?? new List<string>())
                .Set(p => p.PrecioActual, input.PrecioActual)
                .Set(p => p.PrecioMayorista, input.PrecioMayorista)
                .Set(p => p.CantidadMinimaMayorista, input.CantidadMinimaMayorista)
                .Set(p => p.UnidadMedida, input.UnidadMedida)
                .Set(p => p.Categoria, input.Categoria)
                .Set(p => p.Stock, input.Stock);

            await db.Productos.UpdateOneAsync(p => p.Id == productoId, update);

            producto.Nombre = input.Nombre;
            producto.Descripcion = input.Descripcion;
            producto.Atributos = input.Atributo ?? new List<string>();
            producto.PrecioActual = input.PrecioActual;
            producto.PrecioMayorista = input.PrecioMayorista;
            producto.CantidadMinimaMayorista = input.CantidadMinimaMayorista;
            producto.UnidadMedida = input.UnidadMedida;
            producto.Categoria = input.Categoria;
            producto.Stock = input.Stock;

            return producto;
        }

        [Authorize(Roles = new[] { "productor" })]
        public async Task<Venta> AceptarVenta(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string idVenta)
        {
            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
            {
                throw new Exception("Token inválido");
            }

            var productor = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (productor == null)
            {
                throw new Exception("No eres un productor válido");
            }

            var filter = Builders<Venta>.Filter.Where(v => v.Id == idVenta && v.ProductorId == productor.Id);
            var update = Builders<Venta>.Update.Set(p => p.Estado, EstadosVenta.AceptadaRevision);

            var updated = await db.Ventas.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Venta> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
            {
                throw new Exception("Venta no encontrada.");
            }

            return updated;
        }

        [Authorize(Roles = new[] { "productor" })]
        public async Task<Venta> DenegarVentaProductor(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string idVenta)
        {
            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
            {
                throw new Exception("Token inválido");
            }

            var productor = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (productor == null)
            {
                throw new Exception("No eres un productor válido");
            }

            var filter = Builders<Venta>.Filter.Where(v =>
                v.Id == idVenta &&
                v.ProductorId == productor.Id &&
                v.Estado == EstadosVenta.Solicitada);

            var update = Builders<Venta>.Update.Set(p => p.Estado, EstadosVenta.DenegadaProductor);

            var updated = await db.Ventas.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Venta> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
            {
                throw new Exception("Venta no encontrada o no se puede denegar.");
            }

            foreach (var detalle in updated.detalles)
            {
                var producto = await db.Productos
                    .Find(p => p.Id == detalle.ProductoId)
                    .FirstOrDefaultAsync();

                if (producto != null)
                {
                    await db.Productos.UpdateOneAsync(
                        p => p.Id == producto.Id,
                        Builders<Producto>.Update.Set(p => p.Stock, producto.Stock + detalle.Cantidad));
                }
            }

            var comprobador = await db.Comprobadores
                .Find(c => c.Id == updated.ComprobadorId)
                .FirstOrDefaultAsync();

            if (comprobador != null)
            {
                var cuposLiberados = Math.Min(comprobador.CuposDisponibles + 1, Comprobador.CuposMaximos);

                await db.Comprobadores.UpdateOneAsync(
                    c => c.Id == comprobador.Id,
                    Builders<Comprobador>.Update.Set(c => c.CuposDisponibles, cuposLiberados));

                var usuarioComprobador = await db.Usuarios
                    .Find(u => u.Id == comprobador.UsuarioId)
                    .FirstOrDefaultAsync();
                updated.TelefonoComprobador = usuarioComprobador?.telefono;
            }

            return updated;
        }

        [Authorize(Roles = new[] { "comprobador" })]
        public async Task<Venta> ConfirmarVenta(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string idVenta)
        {
            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
            {
                throw new Exception("Token inválido");
            }

            var comprobador = await db.Comprobadores
                .Find(c => c.UsuarioId == usuarioId)
                .FirstOrDefaultAsync();

            if (comprobador == null)
            {
                throw new Exception("No eres un comprobador válido");
            }

            var filter = Builders<Venta>.Filter.Where(v => v.Id == idVenta && v.ComprobadorId == comprobador.Id);
            var update = Builders<Venta>.Update.Set(p => p.Estado, EstadosVenta.CompletadaRevisionAceptada);

            var updated = await db.Ventas.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Venta> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
            {
                throw new Exception("Venta no encontrada.");
            }

            var cuposLiberados = Math.Min(comprobador.CuposDisponibles + 1, Comprobador.CuposMaximos);

            await db.Comprobadores.UpdateOneAsync(
                c => c.Id == comprobador.Id,
                Builders<Comprobador>.Update.Set(c => c.CuposDisponibles, cuposLiberados));

            var usuarioComprobador = await db.Usuarios
                .Find(u => u.Id == comprobador.UsuarioId)
                .FirstOrDefaultAsync();
            updated.TelefonoComprobador = usuarioComprobador?.telefono;

            return updated;
        }

        [Authorize(Roles = new[] { "comprobador" })]
        public async Task<Venta> DenegarVenta(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string idVenta)
        {
            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
            {
                throw new Exception("Token inválido");
            }

            var comprobador = await db.Comprobadores
                .Find(c => c.UsuarioId == usuarioId)
                .FirstOrDefaultAsync();

            if (comprobador == null)
            {
                throw new Exception("No eres un comprobador válido");
            }

            var filter = Builders<Venta>.Filter.Where(v => v.Id == idVenta && v.ComprobadorId == comprobador.Id);
            var update = Builders<Venta>.Update.Set(p => p.Estado, EstadosVenta.DenegadaSupervisor);

            var updated = await db.Ventas.FindOneAndUpdateAsync(
                filter,
                update,
                new FindOneAndUpdateOptions<Venta> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
            {
                throw new Exception("Venta no encontrada.");
            }

            var cuposLiberados = Math.Min(comprobador.CuposDisponibles + 1, Comprobador.CuposMaximos);

            await db.Comprobadores.UpdateOneAsync(
                c => c.Id == comprobador.Id,
                Builders<Comprobador>.Update.Set(c => c.CuposDisponibles, cuposLiberados));

            var usuarioComprobador = await db.Usuarios
                .Find(u => u.Id == comprobador.UsuarioId)
                .FirstOrDefaultAsync();
            updated.TelefonoComprobador = usuarioComprobador?.telefono;

            return updated;
        }

        [Authorize(Roles = new[] { "productor" })]
        public async Task<bool> EliminarProducto(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            [Service] IWebHostEnvironment env,
            string productoId)
        {
            var usuarioId = user.FindFirst("id")?.Value;

            var productor = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (productor == null)
                throw new Exception("No eres un productor.");

            var producto = await db.Productos
                .Find(p => p.Id == productoId)
                .FirstOrDefaultAsync();

            if (producto == null)
                throw new Exception("Producto no encontrado.");
            var usuarioAdmin = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();

            //if (usuarioAdmin.Roles ==new[] { "admin" })
            //{

            //}
            //else
            //{
                if (producto.ProductorId != productor.Id)
                    throw new Exception("No puedes eliminar productos de otro productor.");

            //}

            var imagenes = producto.Imagenes?.ToList() ?? new List<string>();

            var result = await db.Productos.DeleteOneAsync(p => p.Id == productoId);

            if (result.DeletedCount == 1)
            {
                EliminarArchivosImagen(imagenes, env);
            }

            return result.DeletedCount == 1;
        }

        

        public async Task<DetalleVenta> CrearDetalleVenta(
            [Service] MongoDbContext db,
            CrearDetalleVentaInput input)
        {
            ValidarDetalleVentaInput(input);

            var producto = await db.Productos.Find(p => p.Id == input.ProductoId).FirstOrDefaultAsync();
            if (producto == null)
                throw new Exception("No se encontró producto con ese id");

            if (input.Cantidad > producto.Stock)
                throw new Exception("Stock insuficiente");

            decimal precioUnitario = ObtenerPrecioAplicable(producto, input.Cantidad);

            decimal subtotal = precioUnitario * input.Cantidad;

            return new DetalleVenta
            {
                ProductoId = producto.Id!,
                Cantidad = input.Cantidad,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotal,
           
            };
        }
        [AllowAnonymous]
        public async Task<Usuario> CrearUsuario(
                            [Service] MongoDbContext db,
                            CrearUsuarioInput input)
        {
            try
            {
                ValidarUsuarioInput(input);
                await ValidarDuplicadosUsuario(db, input);
            }
            catch (GraphQLException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CrearGraphQLException(ex.Message, "VALIDACION_USUARIO");
            }

            var nuevo = new Usuario
            {
                Nombre = input.Nombre,
                Apellido = input.Apellido,
                Correo = input.Correo.Trim(),
                Password = input.Password,
                Roles = new List<string> { "usuario" },
                telefono = input.telefono.Trim()
            };

            try
            {
                await db.Usuarios.InsertOneAsync(nuevo);
            }
            catch (MongoWriteException ex) when (ex.WriteError?.Code == 11000)
            {
                throw CrearGraphQLException("Ya existe un usuario registrado con ese correo o teléfono.", "USUARIO_DUPLICADO");
            }
            catch (MongoWriteException ex)
            {
                throw CrearGraphQLException($"No se pudo registrar al usuario: {ex.Message}", "ERROR_REGISTRO");
            }
            catch (Exception ex)
            {
                throw CrearGraphQLException($"Error inesperado al crear usuario: {ex.Message}", "ERROR_REGISTRO");
            }

            return nuevo;
        }



       







        [Authorize]
        public async Task<Venta> CrearVenta(
                ClaimsPrincipal user,
                [Service] MongoDbContext db,
                CrearVentaInput input)
        {
            ValidarVentaInput(input);

            var usuarioId = user.FindFirst("id")?.Value;

            if (usuarioId is null)
                throw new Exception("Token inválido");

            var productor = await db.Productores
                .Find(p => p.Id == input.ProductorId)
                .FirstOrDefaultAsync();

            if (productor is null)
                throw new Exception("El productor no existe");

            if (productor.IdUsuario == usuarioId)
                throw new Exception("No puedes comprar tus propios productos.");

            var comprobadorId = string.IsNullOrWhiteSpace(input.ComprobadorId)
                ? null
                : input.ComprobadorId;

            Comprobador? comprobador = null;
            if (!string.IsNullOrWhiteSpace(comprobadorId))
            {
                comprobador = await db.Comprobadores
                    .Find(c => c.Id == comprobadorId)
                    .FirstOrDefaultAsync();

                if (comprobador == null)
                    throw new Exception("No se encontró comprobador con ese id");

                if (comprobador.CuposDisponibles <= 0)
                {
                    throw new Exception("El comprobador no tiene cupos disponibles");
                }
            }

            if (string.IsNullOrWhiteSpace(input.NumeroTransaccion))
            {
                throw new Exception("Debes ingresar el número de transacción.");
            }

            var venta = new Venta
            {
                UsuarioId = usuarioId,
                ProductorId = productor.Id!,
                Fecha = DateTime.UtcNow,
                ComprobadorId = comprobadorId,
                NumeroTransaccion = input.NumeroTransaccion,
                Estado = EstadosVenta.Solicitada,
                detalles = new List<DetalleVenta>()
            };

            if (comprobador != null)
            {
                var filterCupo = Builders<Comprobador>.Filter.Where(c => c.Id == comprobador.Id && c.CuposDisponibles > 0);
                var actualizarCupo = Builders<Comprobador>.Update.Inc(c => c.CuposDisponibles, -1);
                var resultadoCupo = await db.Comprobadores.UpdateOneAsync(filterCupo, actualizarCupo);

                if (resultadoCupo.ModifiedCount == 0)
                {
                    throw new Exception("El comprobador no tiene cupos disponibles en este momento");
                }
            }
            decimal total = 0;

            foreach (var det in input.Detalles)
            {
                var producto = await db.Productos
                    .Find(p => p.Id == det.ProductoId && p.ProductorId == productor.Id)
                    .FirstOrDefaultAsync();

                if (producto is null)
                    throw new Exception("El producto no existe o no pertenece al productor");

                if (producto.Stock < det.Cantidad)
                    throw new Exception($"Stock insuficiente para el producto {producto.Nombre}");

              

                var precioUnitario = ObtenerPrecioAplicable(producto, det.Cantidad);
                decimal subtotal = precioUnitario * det.Cantidad;

                var detalle = new DetalleVenta
                {
                    ProductoId = producto.Id!,
                    NombreProducto=producto.Nombre,
                    Cantidad = det.Cantidad,
                    PrecioUnitario = precioUnitario,
                    Subtotal = subtotal,
                };

                await db.Productos.UpdateOneAsync(
                    p => p.Id == producto.Id!,
                    Builders<Producto>.Update.Set(p => p.Stock, producto.Stock - det.Cantidad)
                );

                total += subtotal;
                venta.detalles.Add(detalle);
            }

            venta.montoTotal = total;

            if (comprobador != null)
            {
                var usuarioComprobador = await db.Usuarios
                    .Find(u => u.Id == comprobador.UsuarioId)
                    .FirstOrDefaultAsync();
                venta.TelefonoComprobador = usuarioComprobador?.telefono;
            }

            await db.Ventas.InsertOneAsync(venta);

            return venta;
        }

        [Authorize(Roles = new[] { "comprobador" })]
        public async Task<Venta> AsignarComprobador(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string ventaId)
        {
            var usuarioId = user.FindFirst("id")?.Value;
            if (usuarioId == null)
            {
                throw new Exception("Token inválido");
            }

            var comprobador = await db.Comprobadores
                .Find(c => c.UsuarioId == usuarioId)
                .FirstOrDefaultAsync();

            if (comprobador == null)
            {
                throw new Exception("No eres un comprobador válido");
            }

            var venta = await db.Ventas
                .Find(v => v.Id == ventaId)
                .FirstOrDefaultAsync();

            if (venta == null)
            {
                throw new Exception("Venta no encontrada.");
            }

            if (!string.IsNullOrWhiteSpace(venta.ComprobadorId))
            {
                throw new Exception("La venta ya cuenta con un comprobador asignado.");
            }

            if (comprobador.CuposDisponibles <= 0)
            {
                throw new Exception("No tienes cupos disponibles.");
            }

            var filterCupo = Builders<Comprobador>.Filter.Where(c => c.Id == comprobador.Id && c.CuposDisponibles > 0);
            var actualizarCupo = Builders<Comprobador>.Update.Inc(c => c.CuposDisponibles, -1);
            var resultadoCupo = await db.Comprobadores.UpdateOneAsync(filterCupo, actualizarCupo);

            if (resultadoCupo.ModifiedCount == 0)
            {
                throw new Exception("No tienes cupos disponibles en este momento.");
            }

            var filterVenta = Builders<Venta>.Filter.Where(v => v.Id == ventaId && (v.ComprobadorId == null || v.ComprobadorId == ""));
            var updateVenta = Builders<Venta>.Update.Set(v => v.ComprobadorId, comprobador.Id);

            var ventaActualizada = await db.Ventas.FindOneAndUpdateAsync(
                filterVenta,
                updateVenta,
                new FindOneAndUpdateOptions<Venta> { ReturnDocument = ReturnDocument.After });

            if (ventaActualizada == null)
            {
                await db.Comprobadores.UpdateOneAsync(
                    c => c.Id == comprobador.Id,
                    Builders<Comprobador>.Update.Inc(c => c.CuposDisponibles, 1));

                throw new Exception("No se pudo asignar el comprobador a la venta.");
            }

            var usuarioComprobador = await db.Usuarios
                .Find(u => u.Id == comprobador.UsuarioId)
                .FirstOrDefaultAsync();
            ventaActualizada.TelefonoComprobador = usuarioComprobador?.telefono;

            return ventaActualizada;
        }
        [Authorize]
        public async Task<bool> EliminarVenta(
            ClaimsPrincipal user,
            [Service] MongoDbContext db,
            string ventaId)
        {
            var usuarioId = user.FindFirst("id")?.Value;

            var venta = await db.Ventas.Find(v => v.Id == ventaId).FirstOrDefaultAsync();

            if (venta is null)
                throw new Exception("Venta no encontrada");

            var roles = user.FindAll(ClaimTypes.Role).Select(r => r.Value).ToList();
            bool esAdmin = roles.Contains("admin");

            if (venta.UsuarioId != usuarioId && !esAdmin)
                throw new Exception("No tienes permiso para eliminar esta venta");

            foreach (var det in venta.detalles)
            {
                var producto = await db.Productos
                    .Find(p => p.Id == det.ProductoId)
                    .FirstOrDefaultAsync();

                if (producto != null)
                {
                    await db.Productos.UpdateOneAsync(
                        p => p.Id == producto.Id!,
                        Builders<Producto>.Update.Set(p => p.Stock, producto.Stock + det.Cantidad)
                    );
                }
            }

            var result = await db.Ventas.DeleteOneAsync(v => v.Id == ventaId);

            return result.DeletedCount == 1;
        }

        private static void ValidarUsuarioInput(CrearUsuarioInput? input)
        {
            if (input == null)
                throw new Exception("La información del usuario es obligatoria.");

            ValidarCampoTexto(input.Nombre, "Nombre");
            ValidarCampoTexto(input.Apellido, "Apellido");
            ValidarCorreo(input.Correo);

            if (string.IsNullOrWhiteSpace(input.Password))
                throw new Exception("La contraseña es obligatoria.");

            if (input.Password.Length < 6)
                throw new Exception("La contraseña debe tener al menos 6 caracteres.");

            ValidarNumeroEnTexto(input.telefono, "Teléfono", 7, 15);
        }

        private static async Task ValidarDuplicadosUsuario(MongoDbContext db, CrearUsuarioInput input)
        {
            var correo = input.Correo.Trim();
            var telefono = input.telefono.Trim();

            if (!string.IsNullOrWhiteSpace(correo))
            {
                var filtroCorreo = Builders<Usuario>.Filter.Eq(u => u.Correo, correo);
                var existeCorreo = await db.Usuarios.Find(filtroCorreo).FirstOrDefaultAsync();
                if (existeCorreo != null)
                {
                    throw CrearGraphQLException("Este correo ya está en uso.", "USUARIO_DUPLICADO");
                }
            }

            if (!string.IsNullOrWhiteSpace(telefono))
            {
                var filtroTelefono = Builders<Usuario>.Filter.Eq(u => u.telefono, telefono);
                var existeTelefono = await db.Usuarios.Find(filtroTelefono).FirstOrDefaultAsync();
                if (existeTelefono != null)
                {
                    throw CrearGraphQLException("Este teléfono ya está registrado.", "USUARIO_DUPLICADO");
                }
            }
        }

        private static GraphQLException CrearGraphQLException(string mensaje, string codigo)
        {
            return new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(mensaje)
                    .SetCode(codigo)
                    .Build());
        }

        private static void ValidarProductoInput(CrearProductoInput? input)
        {
            if (input == null)
                throw new Exception("La información del producto es obligatoria.");

            ValidarCampoTexto(input.Nombre, "Nombre del producto");
            ValidarCampoTexto(input.Descripcion, "Descripción", 500);
            ValidarCampoTexto(input.UnidadMedida, "Unidad de medida", 50);
            ValidarCampoTexto(input.Categoria, "Categoría", 100);
            ValidarDecimal(input.PrecioActual, "Precio actual");

            if (input.PrecioMayorista.HasValue)
                ValidarDecimal(input.PrecioMayorista.Value, "Precio mayorista");

            if (input.CantidadMinimaMayorista.HasValue)
                ValidarEnteroPositivo(input.CantidadMinimaMayorista.Value, "Cantidad mínima mayorista");

            ValidarDecimal(input.Stock, "Stock", permitirCero: true);
        }

        private static void ValidarDetalleVentaInput(CrearDetalleVentaInput? input)
        {
            if (input == null)
                throw new Exception("Cada detalle de venta es obligatorio.");

            ValidarCampoTexto(input.ProductoId, "ProductoId");

            if (input.Cantidad <= 0)
                throw new Exception("La cantidad debe ser mayor a cero.");

            ValidarEnteroPositivo(input.Cantidad, "Cantidad");
        }

        private static void ValidarVentaInput(CrearVentaInput? input)
        {
            if (input == null)
                throw new Exception("La información de la venta es obligatoria.");

            ValidarCampoTexto(input.ProductorId, "ProductorId");
            ValidarCampoTexto(input.ComprobadorId, "ComprobadorId", esOpcional: true);
            ValidarNumeroEnTexto(input.NumeroTransaccion, "Número de transacción", 1, 30);

            if (input.Detalles == null || input.Detalles.Count == 0)
                throw new Exception("La venta debe contener al menos un detalle.");

            foreach (var detalle in input.Detalles)
            {
                ValidarDetalleVentaInput(detalle);
            }
        }

        private static void ValidarCampoTexto(string? valor, string nombreCampo, int maxLength = 200, bool esOpcional = false)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                if (esOpcional)
                    return;

                throw new Exception($"El campo {nombreCampo} es obligatorio.");
            }

            if (valor.Length > maxLength)
                throw new Exception($"El campo {nombreCampo} no puede superar los {maxLength} caracteres.");
        }

        private static void ValidarNumeroEnTexto(string? valor, string nombreCampo, int minLength, int maxLength, bool esOpcional = false)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                if (esOpcional)
                    return;

                throw new Exception($"El campo {nombreCampo} es obligatorio.");
            }

            if (!DigitsOnlyRegex.IsMatch(valor))
                throw new Exception($"El campo {nombreCampo} solo debe contener números.");

            if (valor.Length < minLength || valor.Length > maxLength)
                throw new Exception($"El campo {nombreCampo} debe tener entre {minLength} y {maxLength} dígitos.");
        }

        private static void ValidarCorreo(string? correo, bool esOpcional = false)
        {
            if (string.IsNullOrWhiteSpace(correo))
            {
                if (esOpcional)
                    return;

                throw new Exception("El correo es obligatorio.");
            }

            if (!EmailValidator.IsValid(correo))
                throw new Exception("El correo no tiene un formato válido.");
        }

        private static void ValidarDecimal(decimal valor, string nombreCampo, bool permitirCero = false)
        {
            if (!permitirCero && valor <= 0)
                throw new Exception($"El campo {nombreCampo} debe ser mayor que cero.");

            if (permitirCero && valor < 0)
                throw new Exception($"El campo {nombreCampo} no puede ser negativo.");

            if (ContarDigitosEnteros(valor) > MaxNumericDigits)
                throw new Exception($"El campo {nombreCampo} no puede tener más de {MaxNumericDigits} dígitos en la parte entera.");
        }

        private static void ValidarEnteroPositivo(int valor, string nombreCampo, bool permitirCero = false)
        {
            if (!permitirCero && valor <= 0)
                throw new Exception($"El campo {nombreCampo} debe ser mayor que cero.");

            if (permitirCero && valor < 0)
                throw new Exception($"El campo {nombreCampo} no puede ser negativo.");

            if (ContarDigitosEnteros(valor) > MaxNumericDigits)
                throw new Exception($"El campo {nombreCampo} no puede tener más de {MaxNumericDigits} dígitos.");
        }

        private static int ContarDigitosEnteros(decimal valor)
        {
            var entero = decimal.Truncate(Math.Abs(valor));

            if (entero == 0)
                return 1;

            var digitos = 0;
            while (entero >= 1)
            {
                entero = Math.Floor(entero / 10);
                digitos++;
            }

            return digitos;
        }

        private static decimal ObtenerPrecioAplicable(Producto producto, int cantidad)
        {
            if (producto.CantidadMinimaMayorista.HasValue &&
                producto.PrecioMayorista.HasValue &&
                cantidad >= producto.CantidadMinimaMayorista.Value)
            {
                return producto.PrecioMayorista.Value;
            }

            return producto.PrecioActual;
        }

        private static void EliminarArchivosImagen(List<string> urls, IWebHostEnvironment env)
        {
            if (urls == null || urls.Count == 0)
                return;

            var raiz = string.IsNullOrWhiteSpace(env.WebRootPath)
                ? IOPath.Combine(env.ContentRootPath, "wwwroot")
                : env.WebRootPath;

            var carpetaUploads = IOPath.Combine(raiz, "uploads");
            if (!Directory.Exists(carpetaUploads))
                return;

            foreach (var url in urls)
            {
                try
                {
                    var nombreArchivo = ObtenerNombreArchivo(url);
                    if (string.IsNullOrWhiteSpace(nombreArchivo))
                        continue;

                    var ruta = IOPath.Combine(carpetaUploads, nombreArchivo);
                    if (File.Exists(ruta))
                    {
                        File.Delete(ruta);
                    }
                }
                catch
                {
                    // No interrumpir la eliminación del producto si una imagen falla.
                }
            }
        }

        private static string ObtenerNombreArchivo(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluta))
            {
                return IOPath.GetFileName(absoluta.LocalPath);
            }

            var normalizado = url.Replace('\\', '/');
            return IOPath.GetFileName(normalizado);
        }


        //Mutations admin................

        [Authorize(Roles = new[] { "admin" })]
        public async Task<bool> EliminarProductoAdmin(
            [Service] MongoDbContext db,
            [Service] IWebHostEnvironment env,
            string productoId)
        {
            var producto = await db.Productos
                .Find(p => p.Id == productoId)
                .FirstOrDefaultAsync();

            if (producto == null)
                throw new Exception("Producto no encontrado.");

            var imagenes = producto.Imagenes?.ToList() ?? new List<string>();

            var result = await db.Productos.DeleteOneAsync(p => p.Id == productoId);

            if (result.DeletedCount == 1)
            {
                EliminarArchivosImagen(imagenes, env);
            }

            return result.DeletedCount == 1;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<bool> EliminarUsuarioAdmin(
            [Service] MongoDbContext db,
            string usuarioId)
        {
            var result = await db.Usuarios.DeleteOneAsync(u => u.Id == usuarioId);
            return result.DeletedCount == 1;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<bool> EliminarVentaAdmin(
            [Service] MongoDbContext db,
            string ventaId)
        {
            var result = await db.Ventas.DeleteOneAsync(v => v.Id == ventaId);
            return result.DeletedCount == 1;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<bool> EliminarComprobadorAdmin(
            [Service] MongoDbContext db,
            string comprobadorId)
        {
            var result = await db.Comprobadores.DeleteOneAsync(c => c.Id == comprobadorId);
            return result.DeletedCount == 1;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<bool> EliminarProductorAdmin(
            [Service] MongoDbContext db,
            [Service] IWebHostEnvironment env,
            string productorId)
        {
            // Obtener el productor antes de eliminarlo
            var productor = await db.Productores
                .Find(p => p.Id == productorId)
                .FirstOrDefaultAsync();

            if (productor == null)
                throw new Exception("Productor no encontrado.");

            var usuarioId = productor.IdUsuario;

            // Obtener todos los productos relacionados para eliminar sus imágenes
            var productos = await db.Productos
                .Find(p => p.ProductorId == productorId)
                .ToListAsync();

            // Eliminar las imágenes de todos los productos relacionados
            foreach (var producto in productos)
            {
                if (producto.Imagenes != null && producto.Imagenes.Count > 0)
                {
                    EliminarArchivosImagen(producto.Imagenes.ToList(), env);
                }
            }

            // Eliminar todos los productos relacionados al productor
            await db.Productos.DeleteManyAsync(p => p.ProductorId == productorId);

            // Eliminar el rol "productor" del usuario
            if (!string.IsNullOrWhiteSpace(usuarioId))
            {
                var updateRoles = Builders<Usuario>.Update.PullFilter(u => u.Roles, r => r == "productor");
                await db.Usuarios.UpdateOneAsync(u => u.Id == usuarioId, updateRoles);
            }

            // Finalmente, eliminar el productor
            var result = await db.Productores.DeleteOneAsync(p => p.Id == productorId);
            return result.DeletedCount == 1;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<Productor> CrearProductorAdmin(
            [Service] MongoDbContext db,
            string usuarioId,
            string nombreUsuario,
            string direccion,
            string nit,
            string telefono)
        {
            // Verificar que el usuario exista
            var usuario = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();
            if (usuario == null)
                throw new Exception("Usuario no encontrado");

            // Verificar que el usuario no ya sea productor
            var productorExistente = await db.Productores.Find(p => p.IdUsuario == usuarioId).FirstOrDefaultAsync();
            if (productorExistente != null)
                throw new Exception("El usuario ya es un productor");

            // Crear nuevo productor
            var nuevoProductor = new Productor
            {
                IdUsuario = usuarioId,
                NombreUsuario = nombreUsuario,
                Direccion = direccion,
                Nit = nit
            };

            await db.Productores.InsertOneAsync(nuevoProductor);
            return nuevoProductor;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<Usuario> EditarUsuarioAdmin(
            [Service] MongoDbContext db,
            string usuarioId,
            string? nombre,
            string? apellido,
            string? correo,
            string? password,
            string? telefono,
            List<string>? roles)
        {
            var updates = new List<UpdateDefinition<Usuario>>();

            if (!string.IsNullOrWhiteSpace(nombre))
                updates.Add(Builders<Usuario>.Update.Set(u => u.Nombre, nombre));

            if (!string.IsNullOrWhiteSpace(apellido))
                updates.Add(Builders<Usuario>.Update.Set(u => u.Apellido, apellido));

            if (!string.IsNullOrWhiteSpace(correo))
            {
                ValidarCorreo(correo, esOpcional: true);
                updates.Add(Builders<Usuario>.Update.Set(u => u.Correo, correo));
            }

            if (!string.IsNullOrWhiteSpace(telefono))
            {
                ValidarNumeroEnTexto(telefono, "Teléfono", 7, 15, esOpcional: true);
                updates.Add(Builders<Usuario>.Update.Set(u => u.telefono, telefono));
            }

            if (roles != null)
                updates.Add(Builders<Usuario>.Update.Set(u => u.Roles, roles));
            if (!string.IsNullOrWhiteSpace(password))
            {
                if (password.Length < 6)
                    throw new Exception("La contraseña debe tener al menos 6 caracteres.");
                updates.Add(Builders<Usuario>.Update.Set(u => u.Password, password));
            }
            if (updates.Count == 0)
                throw new Exception("No hay campos para actualizar.");

            var combined = Builders<Usuario>.Update.Combine(updates);
            var filter = Builders<Usuario>.Filter.Eq(u => u.Id, usuarioId);
            var updated = await db.Usuarios.FindOneAndUpdateAsync(filter, combined,
                new FindOneAndUpdateOptions<Usuario> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
                throw new Exception("Usuario no encontrado.");

            return updated;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<Producto> EditarProductoAdmin(
            [Service] MongoDbContext db,
            string productoId,
            string? nombre,
            string? descripcion,
            decimal? precioActual,
            decimal? precioMayorista,
            int? cantidadMinimaMayorista,
            string? unidadMedida,
            string? categoria,
            decimal? stock,
            List<string>? atributos,
            List<string>? imagenes,
            string? productorId)
        {
            var updates = new List<UpdateDefinition<Producto>>();

            if (!string.IsNullOrWhiteSpace(nombre))
                updates.Add(Builders<Producto>.Update.Set(p => p.Nombre, nombre));
            if (!string.IsNullOrWhiteSpace(descripcion))
                updates.Add(Builders<Producto>.Update.Set(p => p.Descripcion, descripcion));
            if (precioActual.HasValue)
            {
                ValidarDecimal(precioActual.Value, "Precio actual");
                updates.Add(Builders<Producto>.Update.Set(p => p.PrecioActual, precioActual.Value));
            }
            if (precioMayorista.HasValue)
            {
                ValidarDecimal(precioMayorista.Value, "Precio mayorista");
                updates.Add(Builders<Producto>.Update.Set(p => p.PrecioMayorista, precioMayorista));
            }
            if (cantidadMinimaMayorista.HasValue)
            {
                ValidarEnteroPositivo(cantidadMinimaMayorista.Value, "Cantidad mínima mayorista");
                updates.Add(Builders<Producto>.Update.Set(p => p.CantidadMinimaMayorista, cantidadMinimaMayorista));
            }
            if (!string.IsNullOrWhiteSpace(unidadMedida))
                updates.Add(Builders<Producto>.Update.Set(p => p.UnidadMedida, unidadMedida));
            if (!string.IsNullOrWhiteSpace(categoria))
                updates.Add(Builders<Producto>.Update.Set(p => p.Categoria, categoria));
            if (stock.HasValue)
            {
                ValidarDecimal(stock.Value, "Stock", permitirCero: true);
                updates.Add(Builders<Producto>.Update.Set(p => p.Stock, stock.Value));
            }
            if (atributos != null)
                updates.Add(Builders<Producto>.Update.Set(p => p.Atributos, atributos));
            if (imagenes != null)
                updates.Add(Builders<Producto>.Update.Set(p => p.Imagenes, imagenes.Take(4).ToList()));
            if (!string.IsNullOrWhiteSpace(productorId))
                updates.Add(Builders<Producto>.Update.Set(p => p.ProductorId, productorId));

            if (updates.Count == 0)
                throw new Exception("No hay campos para actualizar.");

            var combined = Builders<Producto>.Update.Combine(updates);
            var filter = Builders<Producto>.Filter.Eq(p => p.Id, productoId);
            var updated = await db.Productos.FindOneAndUpdateAsync(filter, combined,
                new FindOneAndUpdateOptions<Producto> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
                throw new Exception("Producto no encontrado.");

            return updated;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<Productor> EditarProductorAdmin(
            [Service] MongoDbContext db,
            string productorId,
            string? nombreUsuario,
            string? direccion,
            string? nit,
            string? numeroCuenta,
            string? banco,
            string? usuarioId)
        {
            var updates = new List<UpdateDefinition<Productor>>();

            if (!string.IsNullOrWhiteSpace(nombreUsuario))
            {
                ValidarCampoTexto(nombreUsuario, "Nombre del productor", 200, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.NombreUsuario, nombreUsuario));
            }

            if (!string.IsNullOrWhiteSpace(direccion))
            {
                ValidarCampoTexto(direccion, "Dirección", 200, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.Direccion, direccion));
            }

            if (!string.IsNullOrWhiteSpace(nit))
            {
                ValidarNumeroEnTexto(nit, "NIT", 6, MaxNumericDigits, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.Nit, nit));
            }

            if (!string.IsNullOrWhiteSpace(numeroCuenta))
            {
                ValidarNumeroEnTexto(numeroCuenta, "Número de cuenta", 6, MaxNumericDigits, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.NumeroCuenta, numeroCuenta));
            }

            if (!string.IsNullOrWhiteSpace(banco))
            {
                ValidarCampoTexto(banco, "Banco", 200, esOpcional: true);
                updates.Add(Builders<Productor>.Update.Set(p => p.Banco, banco));
            }

            string? nombreDesdeUsuario = null;

            if (!string.IsNullOrWhiteSpace(usuarioId))
            {
                var usuario = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();
                if (usuario == null)
                    throw new Exception("Usuario no encontrado.");

                var productorExistente = await db.Productores
                    .Find(p => p.IdUsuario == usuarioId && p.Id != productorId)
                    .FirstOrDefaultAsync();

                if (productorExistente != null)
                    throw new Exception("Este usuario ya tiene un productor asociado.");

                updates.Add(Builders<Productor>.Update.Set(p => p.IdUsuario, usuarioId));

                if (string.IsNullOrWhiteSpace(nombreUsuario))
                {
                    nombreDesdeUsuario = usuario.Nombre;
                }
            }

            if (!string.IsNullOrWhiteSpace(nombreDesdeUsuario))
            {
                updates.Add(Builders<Productor>.Update.Set(p => p.NombreUsuario, nombreDesdeUsuario));
            }

            if (updates.Count == 0)
                throw new Exception("No hay campos para actualizar.");

            var combined = Builders<Productor>.Update.Combine(updates);
            var filter = Builders<Productor>.Filter.Eq(p => p.Id, productorId);
            var updated = await db.Productores.FindOneAndUpdateAsync(filter, combined,
                new FindOneAndUpdateOptions<Productor> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
                throw new Exception("Productor no encontrado.");

            return updated;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<Venta> EditarVentaAdmin(
            [Service] MongoDbContext db,
            string ventaId,
            string? estado,
            string? numeroTransaccion)
        {
            var updates = new List<UpdateDefinition<Venta>>();

            if (!string.IsNullOrWhiteSpace(estado))
                updates.Add(Builders<Venta>.Update.Set(v => v.Estado, estado));
            if (!string.IsNullOrWhiteSpace(numeroTransaccion))
            {
                ValidarNumeroEnTexto(numeroTransaccion, "Número de transacción", 1, 30, esOpcional: true);
                updates.Add(Builders<Venta>.Update.Set(v => v.NumeroTransaccion, numeroTransaccion));
            }

            if (updates.Count == 0)
                throw new Exception("No hay campos para actualizar.");

            var combined = Builders<Venta>.Update.Combine(updates);
            var filter = Builders<Venta>.Filter.Eq(v => v.Id, ventaId);
            var updated = await db.Ventas.FindOneAndUpdateAsync(filter, combined,
                new FindOneAndUpdateOptions<Venta> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
                throw new Exception("Venta no encontrada.");

            return updated;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<Comprobador> EditarComprobadorAdmin(
            [Service] MongoDbContext db,
            string comprobadorId,
            string? nombreUsuario,
            string? ci,
            int? cuposDisponibles,
            string? usuarioId)
        {
            var updates = new List<UpdateDefinition<Comprobador>>();

            if (!string.IsNullOrWhiteSpace(nombreUsuario))
                updates.Add(Builders<Comprobador>.Update.Set(c => c.NombreUsuarioBson, nombreUsuario));

            if (!string.IsNullOrWhiteSpace(ci))
            {
                ValidarNumeroEnTexto(ci, "CI", 5, MaxNumericDigits, esOpcional: true);
                updates.Add(Builders<Comprobador>.Update.Set(c => c.CI, ci));
            }

            if (cuposDisponibles.HasValue)
            {
                var cupos = Math.Max(0, Math.Min(Comprobador.CuposMaximos, cuposDisponibles.Value));
                updates.Add(Builders<Comprobador>.Update.Set(c => c.CuposDisponibles, cupos));
            }

            if (!string.IsNullOrWhiteSpace(usuarioId))
                updates.Add(Builders<Comprobador>.Update.Set(c => c.UsuarioId, usuarioId));

            if (updates.Count == 0)
                throw new Exception("No hay campos para actualizar.");

            var combined = Builders<Comprobador>.Update.Combine(updates);
            var filter = Builders<Comprobador>.Filter.Eq(c => c.Id, comprobadorId);
            var updated = await db.Comprobadores.FindOneAndUpdateAsync(filter, combined,
                new FindOneAndUpdateOptions<Comprobador> { ReturnDocument = ReturnDocument.After });

            if (updated == null)
                throw new Exception("Comprobador no encontrado.");

            return updated;
        }

        [Authorize(Roles = new[] { "admin" })]
        public async Task<Comprobador> CrearComprobadorAdmin(
            [Service] MongoDbContext db,
            string usuarioId,
            string ci,
            string? nombreUsuario = null,
            int? cuposDisponibles = null)
        {
            ValidarCampoTexto(usuarioId, "UsuarioId");
            ValidarNumeroEnTexto(ci, "CI", 5, MaxNumericDigits);

            var usuario = await db.Usuarios.Find(u => u.Id == usuarioId).FirstOrDefaultAsync();
            if (usuario == null)
                throw new Exception("Usuario no encontrado.");

            var existente = await db.Comprobadores.Find(c => c.UsuarioId == usuarioId).FirstOrDefaultAsync();
            if (existente != null)
                throw new Exception("Este usuario ya es comprobador.");

            var cupos = Math.Max(0, Math.Min(Comprobador.CuposMaximos, cuposDisponibles ?? Comprobador.CuposMaximos));

            var nuevo = new Comprobador
            {
                UsuarioId = usuarioId,
                NombreUsuarioBson = string.IsNullOrWhiteSpace(nombreUsuario) ? usuario.Nombre : nombreUsuario,
                CI = ci,
                CuposDisponibles = cupos
            };

            await db.Comprobadores.InsertOneAsync(nuevo);

            if (!(usuario.Roles?.Contains("comprobador") ?? false))
            {
                var updateRoles = Builders<Usuario>.Update.AddToSet(u => u.Roles, "comprobador");
                await db.Usuarios.UpdateOneAsync(u => u.Id == usuarioId, updateRoles);
            }

            return nuevo;
        }
    }
}
