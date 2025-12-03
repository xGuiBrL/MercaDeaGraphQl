using System.Collections.Generic;

namespace MercaDeaGraphQl.Inputs
{
    public class CrearProductoInput
    {
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public decimal PrecioActual { get; set; }
        public decimal? PrecioMayorista { get; set; }
        public int? CantidadMinimaMayorista { get; set; }
        public string UnidadMedida { get; set; }
        public List<string> Atributo {  get; set; } = new();
        public List<string> Imagenes { get; set; } = new();
        public string Categoria { get; set; }

        public decimal Stock { get; set; }
    }
}
