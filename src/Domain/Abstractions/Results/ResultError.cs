using System.Collections.Immutable;
using System.Globalization;

namespace Domain.Abstractions.Results;

/// <summary>
/// Décrit une erreur métier de manière sérialisable et localisable.
/// </summary>
/// <remarks>
/// <para>
/// Le <see cref="Message"/> est un <strong>template</strong> (ex. <c>"Le pseudo doit faire entre {0} et {1} caractères"</c>).
/// Le rendu se fait à la demande via <see cref="FormattedMessage(IFormatProvider?)"/>, ce qui permet
/// la localisation en couche présentation (i18n basée sur <see cref="Code"/>).
/// </para>
/// <para>
/// Convention sur <see cref="Args"/> : <strong>uniquement des types primitifs</strong>
/// (string, int, long, bool, decimal, double, float, char...). Pas de DateTime, enum, ou objets complexes —
/// ces types ont des représentations dépendantes de la culture qui posent problème pour la sérialisation
/// et la localisation. Cette contrainte est validée à l'exécution dans l'<c>init</c> setter.
/// </para>
/// </remarks>
/// <param name="Code">Identifiant technique de l'erreur (clé i18n), format conseillé <c>{Entity}.{Operation}.{Problem}</c>.</param>
/// <param name="Message">Template du message (peut contenir des placeholders <c>{0}</c>, <c>{1}</c>, ... référant à <see cref="Args"/>).</param>
/// <param name="Type">Catégorie d'erreur, mappable sur un code HTTP via <see cref="ErrorType"/>.</param>
public record ResultError(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    private readonly ImmutableArray<object> _args = [];

    /// <summary>
    /// Arguments positionnels associés aux placeholders du template <see cref="Message"/>.
    /// </summary>
    /// <remarks>
    /// L'assignation est validée : seuls les types primitifs (string, int, bool, decimal, ...) sont autorisés.
    /// L'état <c>default(ImmutableArray)</c> est normalisé en <see cref="ImmutableArray{T}.Empty"/>.
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

    private readonly ImmutableDictionary<string, ImmutableArray<ValidationFieldError>> _validationErrors = [];

    /// <summary>
    /// Dictionnaire des erreurs par champ, utilisé pour <see cref="ErrorType.Validation"/>.
    /// La clé est le nom du champ, la valeur la liste immuable des erreurs sur ce champ.
    /// </summary>
    /// <remarks>
    /// L'assignation à <c>null</c> est normalisée en <see cref="ImmutableDictionary{TKey,TValue}.Empty"/>
    /// pour cohérence avec <see cref="Args"/>.
    /// </remarks>
    public ImmutableDictionary<string, ImmutableArray<ValidationFieldError>> ValidationErrors
    {
        get => _validationErrors;
        init => _validationErrors = value ?? [];
    }

    /// <summary>
    /// Construit une erreur avec des arguments pour le template.
    /// </summary>
    /// <param name="Code">Identifiant technique.</param>
    /// <param name="Message">Template (peut contenir <c>{0}</c>, <c>{1}</c>, ...).</param>
    /// <param name="args">Arguments positionnels pour rendre le template.</param>
    /// <param name="Type">Catégorie d'erreur.</param>
    public ResultError(string Code, string Message, ImmutableArray<object> args, ErrorType Type = ErrorType.Failure)
        : this(Code, Message, Type)
    {
        Args = args;
    }

    /// <summary>
    /// Rend le <see cref="Message"/> en substituant les <see cref="Args"/>.
    /// </summary>
    /// <param name="provider">Culture utilisée pour le formatage. <see cref="CultureInfo.InvariantCulture"/> par défaut.</param>
    /// <returns>Le message rendu, ou le template tel quel si aucun arg n'est fourni.</returns>
    public string FormattedMessage(IFormatProvider? provider = null)
    {
        if (_args.IsDefaultOrEmpty)
            return Message;

        object[] arr = new object[_args.Length];
        _args.CopyTo(arr);
        return string.Format(provider ?? CultureInfo.InvariantCulture, Message, arr);
    }

    /// <inheritdoc/>
    public virtual bool Equals(ResultError? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Code == other.Code
            && Message == other.Message
            && Type == other.Type
            && _args.SequenceEqual(other._args)
            && ValidationErrorsEqual(_validationErrors, other._validationErrors);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Code);
        hash.Add(Message);
        hash.Add(Type);

        foreach (object arg in _args)
            hash.Add(arg);

        int validationHash = 0;
        foreach (KeyValuePair<string, ImmutableArray<ValidationFieldError>> kvp in _validationErrors)
        {
            HashCode entryHash = new();
            entryHash.Add(kvp.Key);
            foreach (ValidationFieldError error in kvp.Value)
                entryHash.Add(error);
            validationHash ^= entryHash.ToHashCode();
        }

        hash.Add(validationHash);

        return hash.ToHashCode();
    }

    private static bool ValidationErrorsEqual(
       ImmutableDictionary<string, ImmutableArray<ValidationFieldError>> a,
       ImmutableDictionary<string, ImmutableArray<ValidationFieldError>> b)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a.Count != b.Count)
            return false;
        foreach (KeyValuePair<string, ImmutableArray<ValidationFieldError>> kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out ImmutableArray<ValidationFieldError> bValue))
                return false;
            if (!kvp.Value.SequenceEqual(bValue))
                return false;
        }

        return true;
    }
}

/// <summary>
/// Validation runtime du contrat « <see cref="ResultError.Args"/> = uniquement des primitifs ».
/// </summary>
/// <remarks>
/// Pour un enforcement compile-time, un Roslyn analyzer serait nécessaire. À défaut, ce validator
/// échoue rapidement en test ou en dev dès qu'un type non primitif est passé.
/// </remarks>
internal static class ResultErrorArgsValidator
{
    /// <summary>
    /// Vérifie que tous les éléments d'<paramref name="args"/> sont des types primitifs autorisés.
    /// </summary>
    /// <param name="args">Arguments à valider.</param>
    /// <param name="paramName">Nom du paramètre source pour l'exception.</param>
    /// <exception cref="ArgumentException">Si un élément non-primitif est trouvé.</exception>
    public static void Validate(ImmutableArray<object> args, string paramName)
    {
        if (args.IsDefault)
            return;

        foreach (object? arg in args)
        {
            if (arg is null)
                continue;

            Type t = arg.GetType();
            if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal))
                continue;

            throw new ArgumentException(
                $"Args may only contain primitive types (string, int, bool, decimal, ...). " +
                $"Found: {t.FullName}. " +
                $"Reason: non-primitive types have culture-dependent representations that conflict with serialization and localization.",
                paramName);
        }
    }
}
