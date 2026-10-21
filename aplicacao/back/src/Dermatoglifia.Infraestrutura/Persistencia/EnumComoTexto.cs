using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Dermatoglifia.Infraestrutura.Persistencia;

/// <summary>
/// Enums são gravados como texto em MAIÚSCULAS_COM_SUBLINHADO
/// (ex.: <c>ArcoTendido</c> → <c>ARCO_TENDIDO</c>) com CHECK dos valores
/// permitidos (ADR-0003). É o mesmo formato usado na API.
/// </summary>
public static class EnumComoTexto
{
    public const int TamanhoMaximo = 30;

    public static string ParaTexto<TEnum>(TEnum valor) where TEnum : struct, Enum
        => JsonNamingPolicy.SnakeCaseUpper.ConvertName(valor.ToString());

    public static TEnum DeTexto<TEnum>(string texto) where TEnum : struct, Enum
        => Cache<TEnum>.PorTexto.TryGetValue(texto, out var valor)
            ? valor
            : throw new InvalidOperationException($"Valor \"{texto}\" inválido para {typeof(TEnum).Name}.");

    /// <summary>Expressão SQL do CHECK, ex.: <c>"mao" IN ('DIREITA', 'ESQUERDA')</c>.</summary>
    public static string Check<TEnum>(string coluna) where TEnum : struct, Enum
        => $"\"{coluna}\" IN ({string.Join(", ", Enum.GetValues<TEnum>().Select(v => $"'{ParaTexto(v)}'"))})";

    private static class Cache<TEnum> where TEnum : struct, Enum
    {
        public static readonly Dictionary<string, TEnum> PorTexto =
            Enum.GetValues<TEnum>().ToDictionary(v => ParaTexto(v), v => v, StringComparer.Ordinal);
    }
}

/// <summary>Conversor do EF Core que aplica <see cref="EnumComoTexto"/>.</summary>
public sealed class ConversorEnumTexto<TEnum>() : ValueConverter<TEnum, string>(
    valor => EnumComoTexto.ParaTexto(valor),
    texto => EnumComoTexto.DeTexto<TEnum>(texto))
    where TEnum : struct, Enum;
