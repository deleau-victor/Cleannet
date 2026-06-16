namespace Domain.Abstractions.Results;

/// <summary>
/// État interne d'un Result, utilisé pour différencier un Result non initialisé d'un Result avec une valeur ou une erreur.
/// </summary>
internal enum ResultState : byte
{
    /// <summary>
    /// État par défaut d'un Result non initialisé. Un Result dans cet état est considéré comme invalide et ne doit pas être utilisé.
    /// </summary>
    Uninitialized = 0,
    /// <summary>
    /// Indique que le Result contient une valeur de succès valide. Le champ <see cref="_value"/> est défini, et <see cref="_error"/> est null.
    /// </summary>
    Success = 1,
    /// <summary>
    /// Indique que le Result contient une erreur métier. Le champ <see cref="_error"/> est défini, et <see cref="_value"/> est à default(T).
    /// </summary>
    Failure = 2,
}
