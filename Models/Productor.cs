using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MercaDeaGraphQl.Models
{
    public class Productor
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public string IdUsuario { get; set; }
        public string NombreUsuario { get; set; }
        public string Direccion { get; set; }
        public string Nit {  get; set; }
        public string NumeroCuenta { get; set; }
        public string Banco { get; set; }


    }
}
