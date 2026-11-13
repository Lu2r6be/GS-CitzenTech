namespace Termometro.Domain.Niveis;

/// <summary>Decide se o sensor de uma casa está "mudo" (cinza, topo da fila).</summary>
public static class StatusSinal
{
    public static readonly TimeSpan ToleranciaPadrao = TimeSpan.FromMinutes(30);

    /// <param name="ultimaLeitura">Hora da última leitura recebida; null se a casa nunca enviou.</param>
    /// <param name="agora">Hora atual, recebida de fora (assim o teste controla o tempo).</param>
    public static bool EstaSemSinal(DateTimeOffset? ultimaLeitura, DateTimeOffset agora,
        TimeSpan? tolerancia = null)
    {
        if (ultimaLeitura is null)
        {
            return true;
        }
        return agora - ultimaLeitura.Value >= (tolerancia ?? ToleranciaPadrao);
    }
}
