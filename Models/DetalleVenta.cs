 using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections;

namespace MercaDeaGraphQl.Models
{
    public class DetalleVenta
    {
      
        public string ProductoId { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public string NombreProducto { get; set; }
        public decimal Subtotal { get; set; }

       
    }

}
