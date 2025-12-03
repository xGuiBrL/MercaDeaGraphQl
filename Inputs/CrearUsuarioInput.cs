namespace MercaDeaGraphQl.Inputs
{
    public class CrearUsuarioInput
    {
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Correo { get; set; }
        public string Password { get; set; }
        public string telefono { get; set; }
        public List<string> Roles { get; set; }
    }
}
