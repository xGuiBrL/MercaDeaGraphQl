using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MercaDeaGraphQl.Models
{
    public class Producto
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [Required]
        [StringLength(30, MinimumLength = 2)]
        public string Nombre { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0.")]
        public decimal PrecioActual { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El precio mayorista debe ser mayor a 0.")]
        public decimal? PrecioMayorista { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "La cantidad mínima debe ser mayor a 0.")]
        public int? CantidadMinimaMayorista { get; set; }

        public string Descripcion { get; set; }

        [Required]
        public string UnidadMedida { get; set; }
        public List<string> Atributos { get; set; } = new();

        public List<string> Imagenes { get; set; } = new();

        public string Categoria { get; set; }

        [Required]
        public string ProductorId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "El stock no puede ser negativo.")]
        public decimal Stock { get; set; }
    }
}
