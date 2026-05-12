using System.ComponentModel;
using Domain.Abstractions;

namespace Infrastructure.ComponentModel;

/// <summary>
/// Enregistrement des <see cref="TypeConverter"/> pour les types Domain, sans pollution par attributs.
/// </summary>
public static class TypeDescriptorRegistration
{
    /// <summary>
    /// Associe <see cref="IdTypeConverter"/> à <see cref="Id"/> via le registre global
    /// <see cref="TypeDescriptor"/>. Équivalent runtime de <c>[TypeConverter(typeof(IdTypeConverter))]</c>.
    /// </summary>
    /// <remarks>
    /// À appeler une fois au démarrage de l'application. Idempotent : appels multiples sont sans effet supplémentaire.
    /// </remarks>
    public static void RegisterIdTypeConverter()
        => TypeDescriptor.AddAttributes(typeof(Id), new TypeConverterAttribute(typeof(IdTypeConverter)));
}
