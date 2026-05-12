using System.Text.Json;

namespace Infrastructure.Serialization.Json;

/// <summary>
/// Extensions <see cref="JsonSerializerOptions"/> pour enregistrer les converters
/// des identifiants typés du Domain.
/// </summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>
    /// Enregistre <see cref="IdJsonConverterFactory"/> dans les <see cref="JsonSerializerOptions"/>,
    /// activant la sérialisation/désérialisation de <see cref="Domain.Abstractions.Id"/>
    /// et de tous les <see cref="Domain.Abstractions.Id{TEntity}"/> comme chaînes ULID.
    /// </summary>
    public static JsonSerializerOptions AddTypedIdConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new IdJsonConverterFactory());
        return options;
    }
}
