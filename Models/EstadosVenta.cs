namespace MercaDeaGraphQl.Models
{
    public static class EstadosVenta
    {
        public const string Solicitada = "solicitada";
        public const string AceptadaRevision = "aceptada-en revision";
        public const string CompletadaRevisionAceptada = "completada-revision aceptada";
        public const string DenegadaSupervisor = "venta denegada, contactese con el supervisor";
        public const string DenegadaProductor = "venta denegada por el productor";
    }
}
