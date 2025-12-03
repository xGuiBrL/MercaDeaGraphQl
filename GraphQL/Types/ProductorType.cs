using HotChocolate.Types;
using MercaDeaGraphQl.Models;

namespace MercaDeaGraphQl.GraphQL.Types
{
    public class ProductorType : ObjectType<Productor>
    {
        protected override void Configure(IObjectTypeDescriptor<Productor> descriptor)
        {
            descriptor.Field(p => p.Id).Type<StringType>();
            descriptor.Field(p => p.IdUsuario).Type<StringType>();
            descriptor.Field(p => p.NombreUsuario).Type<StringType>();
            descriptor.Field(p => p.Direccion).Type<StringType>();
            descriptor.Field(p => p.Nit).Type<StringType>();
            descriptor.Field(p => p.NumeroCuenta).Type<StringType>();
            descriptor.Field(p => p.Banco).Type<StringType>();
        }
    }
}
