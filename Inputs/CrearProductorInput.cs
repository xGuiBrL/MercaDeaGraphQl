using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MercaDeaGraphQl.Inputs
{
    public class CrearProductorInput
    {
        public string IdUsuario { get; set; }
        public string NombreUsuario { get; set; }
        public string Direccion { get; set; }
        public string Nit { get; set; }
        public string NumeroCuenta { get; set; }
        public string Banco { get; set; }

    }
}
