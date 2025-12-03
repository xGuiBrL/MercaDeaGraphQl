using HotChocolate.Types;
using MercaDeaGraphQl.Models;

namespace MercaDeaGraphQl.GraphQL.Types
{
    public class ProductorType : ObjectType<Productor>
    {
        protected override void Configure(IObjectTypeDescriptor<Productor> descriptor)
        {
            descriptor.Field(p => p.Id).Type<StringType>().Name("id");
            descriptor.Field(p => p.IdUsuario).Type<StringType>().Name("idUsuario");
            descriptor.Field(p => p.NombreUsuario).Type<StringType>().Name("nombreUsuario");
            descriptor.Field(p => p.Direccion).Type<StringType>().Name("direccion");
            descriptor.Field(p => p.Nit).Type<StringType>().Name("nit");
            descriptor.Field(p => p.NumeroCuenta).Type<StringType>().Name("numeroCuenta");
            descriptor.Field(p => p.Banco).Type<StringType>().Name("banco");
        }
    }
}
