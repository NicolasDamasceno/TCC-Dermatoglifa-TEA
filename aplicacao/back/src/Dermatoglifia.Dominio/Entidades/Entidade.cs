namespace Dermatoglifia.Dominio.Entidades;

/// <summary>Base das entidades identificadas por <see cref="Guid"/>.</summary>
public abstract class Entidade
{
    /// <summary>
    /// UUID versão 7 (ordenado no tempo): não é adivinhável como um número
    /// sequencial e mantém os índices do banco compactos.
    /// </summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
}
