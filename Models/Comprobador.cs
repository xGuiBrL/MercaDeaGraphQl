using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using HotChocolate;

namespace MercaDeaGraphQl.Models
{
    [BsonIgnoreExtraElements]
    public class Comprobador
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;
        
        public string UsuarioId { get; set; } = string.Empty;
        
        [BsonElement("NombreUsuario")]
        public string? NombreUsuarioBson { get; set; }
        
        [BsonElement("Nombre")]
        public string? NombreViejo { get; set; }
        
        public string NombreUsuario => NombreUsuarioBson ?? NombreViejo ?? string.Empty;
        
        [GraphQLName("ci")]
        [BsonElement("CI")]
        public string CI { get; set; } = string.Empty;
        
        public const int CuposMaximos = 10;

        public int CuposDisponibles { get; set; } = CuposMaximos;
        
        [GraphQLName("estaDisponible")]
        public bool EstaDisponible => CuposDisponibles > 0;

    }
}
