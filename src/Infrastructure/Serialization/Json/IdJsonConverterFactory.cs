using Domain.Abstractions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Serialization.Json;

/// <summary>
/// Factory <see cref="JsonConverterFactory"/> qui produit les converters pour <see cref="Id"/>
/// et tous les <see cref="Id{TEntity}"/> rencontrés au runtime.
/// </summary>
/// <remarks>
/// Sérialise les ID comme des chaînes ULID. Détecte les <see cref="Id{TEntity}"/> par leur type générique ouvert.
/// </remarks>
public sealed class IdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        if (typeToConvert == typeof(Id))
            return true;

        return typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(Id<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(Id))
            return new IdJsonConverter();

        Type entityType = typeToConvert.GetGenericArguments()[0];
        Type converterType = typeof(TypedIdJsonConverter<>).MakeGenericType(entityType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class IdJsonConverter : JsonConverter<Id>
    {
        public override Id Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? str = reader.GetString() ?? throw new JsonException("Expected non-null string for Id.");
            return Id.Parse(str, null);
        }

        public override void Write(Utf8JsonWriter writer, Id value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

    private sealed class TypedIdJsonConverter<TEntity> : JsonConverter<Id<TEntity>>
    {
        public override Id<TEntity> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string? str = reader.GetString() ?? throw new JsonException($"Expected non-null string for Id<{typeof(TEntity).Name}>.");
            return Id<TEntity>.Parse(str, null);
        }

        public override void Write(Utf8JsonWriter writer, Id<TEntity> value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}
