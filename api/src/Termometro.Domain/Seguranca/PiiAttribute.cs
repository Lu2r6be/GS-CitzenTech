namespace Termometro.Domain.Seguranca;

/// <summary>
/// Marca uma propriedade que guarda dado pessoal (PII). É a "fonte" (source) do taint analysis:
/// o módulo de segurança lê esta marca para saber quais campos nunca podem chegar a logs,
/// respostas HTTP ou ao prompt do LLM sem passar pelo sanitizador.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class PiiAttribute(CategoriaPii categoria) : Attribute
{
    public CategoriaPii Categoria { get; } = categoria;
}
