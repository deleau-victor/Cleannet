using System.Collections.Immutable;

namespace Domain.Abstractions;

/// <summary>
/// Représente une erreur de validation pour un champ spécifique : un code d'erreur identifiant le problème
/// (ex. <c>User.Pseudo.TooShort</c>) et les arguments positionnels pour rendre le message localisé.
/// </summary>
/// <remarks>
/// Le <see cref="Code"/> ici est distinct de <see cref="ResultError.Code"/> :
/// <list type="bullet">
///   <item><see cref="Code"/> identifie l'erreur **au niveau d'un champ** (ex. <c>User.Pseudo.TooShort</c>).</item>
///   <item><see cref="ResultError.Code"/> identifie l'erreur **au niveau de l'opération globale** (ex. <c>User.Create.ValidationFailed</c>).</item>
/// </list>
/// <para>
/// Convention sur <see cref="Args"/> : uniquement des types primitifs (string, int, bool, decimal, etc.).
/// Pas de DateTime, enum, ou objets complexes — voir <see cref="ResultError"/> pour la motivation.
/// </para>
/// </remarks>
/// <param name="Code">Identifiant technique de l'erreur (clé i18n, format <c>{Entity}.{Field}.{Problem}</c>).</param>
public sealed record ValidationFieldError(string Code)
{
    private readonly ImmutableArray<object> _args = [];

    /// <summary>
    /// Arguments positionnels pour rendre le message localisé associé au <see cref="Code"/>.
    /// </summary>
    /// <remarks>
    /// L'assignation est validée : seuls les types primitifs (string, int, bool, decimal, etc.) sont autorisés.
    /// </remarks>
    public ImmutableArray<object> Args
    {
        get => _args;
        init
        {
            ResultErrorArgsValidator.Validate(value, nameof(Args));
            _args = value.IsDefault ? [] : value;
        }
    }

    /// <summary>
    /// Construit une erreur de validation avec des arguments.
    /// </summary>
    public ValidationFieldError(string Code, ImmutableArray<object> Args) : this(Code)
    {
        this.Args = Args;
    }

    /// <inheritdoc/>
    public bool Equals(ValidationFieldError? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Code != other.Code)
            return false;
        return _args.SequenceEqual(other._args);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => Code.GetHashCode(StringComparison.Ordinal);
}
