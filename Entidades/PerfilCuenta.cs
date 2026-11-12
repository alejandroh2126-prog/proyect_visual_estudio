namespace Entidades
{
    public class PerfilCuenta
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Foto { get; set; } = "";
        public string ColorFondo { get; set; } = "";
        public string ColorBotones { get; set; } = "";
    }
}
