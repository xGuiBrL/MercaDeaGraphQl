using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MercaDeaGraphQl.Models
{
    public class Venta
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id    { get; set; }
        public string UsuarioId     { get; set; }
        public string ProductorId { get; set; }
        public DateTime Fecha{ get; set; }
        public decimal montoTotal { get; set; }
        public string? ComprobadorId { get; set; }
        public string NumeroTransaccion { get; set; }
        public string Estado { get; set; } = EstadosVenta.Solicitada;
        public List<DetalleVenta> detalles { get; set; }

        [BsonIgnore]
        public string? TelefonoComprobador { get; set; }
    }
}
