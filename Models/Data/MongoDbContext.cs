using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MercaDeaGraphQl.Models.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _db;

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            _db = client.GetDatabase(settings.Value.DatabaseName);
        }

        public IMongoCollection<Usuario> Usuarios => _db.GetCollection<Usuario>("Usuarios");
        public IMongoCollection<Productor> Productores => _db.GetCollection<Productor>("Productores");
        public IMongoCollection<Producto> Productos => _db.GetCollection<Producto>("Productos");
        public IMongoCollection<Venta> Ventas => _db.GetCollection<Venta>("Ventas");
        public IMongoCollection<Comprobador> Comprobadores => _db.GetCollection<Comprobador>("Comprobador");
        
    }
}
