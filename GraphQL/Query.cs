using HotChocolate.Authorization;
using HotChocolate.Data;
using MercaDeaGraphQl.Models;
using MercaDeaGraphQl.Models.Data;
using MongoDB.Driver;
using System.Linq;
using System.Security.Claims;

namespace MercaDeaGraphQl.GraphQL
{
    public class Query
    {
        [Authorize]
        public async Task<Usuario> GetCurrentUser(ClaimsPrincipal user, [Service] MongoDbContext db)
        {
            var userId = user.FindFirst("id")?.Value;
            if (userId == null)
                throw new Exception("Token inválido");

            var usuario = await db.Usuarios.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (usuario == null)
                throw new Exception("Usuario no encontrado");

            return usuario;
        }

        [Authorize]
        [UseFiltering]
        public IQueryable<Usuario> GetUsuarios([Service] MongoDbContext db) => db.Usuarios.AsQueryable();

        [Authorize(Roles = new[] { "admin" })]
        [UseFiltering]
        public IQueryable<Usuario> GetUsuariosAdmins([Service] MongoDbContext db) => db.Usuarios.AsQueryable();
        [Authorize]
        [UseFiltering]
        public IQueryable<Productor> GetProductores([Service] MongoDbContext db) => db.Productores.AsQueryable();
        
        [Authorize]
        [UseFiltering]
        public IQueryable<Producto> GetProductos([Service] MongoDbContext db) => db.Productos.AsQueryable();
        
        [Authorize]
        [UseFiltering]
        public IQueryable<Venta> GetVentas([Service] MongoDbContext db) => db.Ventas.AsQueryable();

        [Authorize]
        [UseFiltering]
        public IQueryable<Comprobador> GetComprobador([Service] MongoDbContext db) => db.Comprobadores.AsQueryable();


        [Authorize(Roles = new[] { "productor" })]
        public async Task<List<Venta>> VentasPorProductor(
            ClaimsPrincipal user,
            [Service] MongoDbContext db)
        {
            var usuarioId = user.FindFirst("id")?.Value;

            var productor = await db.Productores
                .Find(p => p.IdUsuario == usuarioId)
                .FirstOrDefaultAsync();

            if (productor == null)
                throw new Exception("No eres un productor válido");

            var ventas = await db.Ventas
                .Find(v => v.ProductorId == productor.Id)
                .ToListAsync();

            await EnriquecerTelefonosComprobador(db, ventas);

            return ventas;
        }
        [Authorize]
        public async Task<List<Venta>> VentasPorComprador(
           ClaimsPrincipal user,
           [Service] MongoDbContext db)
        {
            var usuarioId = user.FindFirst("id")?.Value;

            if (usuarioId == null)
                throw new Exception("Token inválido");

            var ventas = await db.Ventas
                .Find(v => v.UsuarioId == usuarioId)
                .ToListAsync();

            await EnriquecerTelefonosComprobador(db, ventas);

            return ventas;
        }

        [Authorize(Roles = new[] { "productor" })]
        public async Task<List<Producto>> Productos(ClaimsPrincipal user, [Service] MongoDbContext db)
        {
            var productorId= user.FindFirst("id")?.Value;
            if (productorId==null)
            {
                throw new Exception("no se encontro productor con ese id");
            }
            var productos= await db.Productos.Find(p=> p.ProductorId== productorId).ToListAsync();
            return productos;

        }
    




        private static async Task EnriquecerTelefonosComprobador(
            MongoDbContext db,
            List<Venta> ventas)
        {
            if (ventas == null || ventas.Count == 0)
                return;

            var comprobadorIds = ventas
                .Select(v => v.ComprobadorId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (comprobadorIds.Count == 0)
                return;

            var comprobadores = await db.Comprobadores
                .Find(c => comprobadorIds.Contains(c.Id))
                .ToListAsync();

            if (comprobadores.Count == 0)
                return;

            var usuarioIds = comprobadores
                .Select(c => c.UsuarioId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct()
                .ToList();

            if (usuarioIds.Count == 0)
                return;

            var usuarios = await db.Usuarios
                .Find(u => u.Id != null && usuarioIds.Contains(u.Id))
                .ToListAsync();

            if (usuarios.Count == 0)
                return;

            var telefonosPorComprobador = comprobadores
                .Join(
                    usuarios,
                    c => c.UsuarioId,
                    u => u.Id,
                    (c, u) => new { c.Id, Telefono = u.telefono })
                .GroupBy(x => x.Id)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Telefono);

            foreach (var venta in ventas)
            {
                if (string.IsNullOrWhiteSpace(venta.ComprobadorId))
                    continue;

                if (telefonosPorComprobador.TryGetValue(venta.ComprobadorId, out var telefono))
                {
                    venta.TelefonoComprobador = telefono;
                }
            }
        }

   }
}
