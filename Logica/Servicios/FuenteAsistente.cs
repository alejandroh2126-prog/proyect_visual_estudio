namespace Logica.Servicios
{
    /// <summary>De donde salio la ultima respuesta del asistente.</summary>
    public enum FuenteAsistente
    {
        Nube,    // Workflow de n8n (Gemini + base de conocimiento en Oracle)
        Local,   // Ollama en el mismo computador
        Basico   // Respuestas por palabras clave, sin IA
    }
}
