using MercaDeaGraphQl.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace MercaDeaGraphQl.Inputs
{
    public class CrearVentaInput
    {
        public string ProductorId { get; set; } = string.Empty;
        public string? ComprobadorId { get; set; }
        public List<CrearDetalleVentaInput> Detalles { get; set; } = new();
        public string NumeroTransaccion { get; set; } = string.Empty;
    }

}
