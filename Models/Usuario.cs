using HotChocolate.Authorization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MercaDeaGraphQl.Models
{
    public class Usuario
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string Nombre {  get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
       // [Authorize(Roles = new[] { "admin" })]
        public string Password { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public string telefono { get; set; } = string.Empty;

    }
}
