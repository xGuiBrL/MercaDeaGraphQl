using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MercaDeaGraphQl.Models.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _db;

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            // 1. Intentar leer variable de entorno
            var envConnection = Environment.GetEnvironmentVariable("MONGODB_URI");

            // 2. Si existe, usarla
            var connectionString = !string.IsNullOrEmpty(envConnection)
                ? envConnection
                : settings.Value.ConnectionString; // fallback local

            // 3. Crear cliente de Mongo
            var client = new MongoClient(connectionString);

            // 4. Obtener el nombre de la BD desde settings
            _db = client.GetDatabase(settings.Value.DatabaseName);
        }

        public IMongoCollection<Usuario> Usuarios => _db.GetCollection<Usuario>("Usuarios");
        public IMongoCollection<Productor> Productores => _db.GetCollection<Productor>("Productores");
        public IMongoCollection<Producto> Productos => _db.GetCollection<Producto>("Productos");
        public IMongoCollection<Venta> Ventas => _db.GetCollection<Venta>("Ventas");
        public IMongoCollection<Comprobador> Comprobadores => _db.GetCollection<Comprobador>("Comprobador");
    }
}
