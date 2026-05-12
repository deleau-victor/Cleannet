using System.Collections.Immutable;
using System.Globalization;

namespace Domain.Abstractions;

/// <summary>
/// Représente le résultat d'une opération qui peut réussir ou échouer.
/// <br />
/// En cas de succès, contient une valeur de type <typeparamref name="T"/> et un <see cref="SuccessType"/>
/// (mappable sur un code HTTP 2xx).
/// <br />
/// En cas d'échec, contient une <see cref="ResultError"/> décrivant la cause.
/// </summary>
/// <typeparam name="T">Le type de la valeur en cas de succès.</typeparam>
/// <remarks>
/// API monadique (<c>Map</c> / <c>Bind</c> / <c>Match</c> ...) inspirée des langages fonctionnels.
/// Conçu pour être chaîné sans nesting d'<c>if (result.IsSuccess)</c>.
/// </remarks>
public readonly record struct Result<T>
{
    /// <summary>
    /// Indique si le résultat est un succès.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indique si le résultat est un échec. Inverse de <see cref="IsSuccess"/>.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    private readonly T? _value;
    private readonly ResultError? _error;

    /// <summary>
    /// La valeur du résultat en cas de succès.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si le résultat est un échec.</exception>
    public T Value => IsSuccess
         ? _value!
         : throw new InvalidOperationException("No value for failure result");

    /// <summary>
    /// L'erreur du résultat en cas d'échec.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si le résultat est un succès.</exception>
    public ResultError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("No error for success result");

    /// <summary>
    /// Le type de succès, mappable sur un code HTTP 2xx (Ok, Created, Accepted, NoContent).
    /// </summary>
    /// <remarks>En cas d'échec, vaut <see cref="SuccessType.Ok"/> par défaut mais n'a pas de signification.</remarks>
    public SuccessType SuccessType { get; }

    private Result(T value, SuccessType successType)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
        SuccessType = successType;
    }

    private Result(ResultError error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
        SuccessType = default;
    }

    // ──────────────────────────────────────────────────────────────────
    // Factory methods
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Crée un résultat de succès avec la valeur et un type de succès optionnel (défaut : <see cref="SuccessType.Ok"/>).</summary>
    public static Result<T> Success(T value, SuccessType type = SuccessType.Ok) => new(value, type);

    /// <summary>Crée un résultat de succès avec <see cref="SuccessType.Created"/> (HTTP 201).</summary>
    public static Result<T> Created(T value) => new(value, SuccessType.Created);

    /// <summary>Crée un résultat de succès avec <see cref="SuccessType.Accepted"/> (HTTP 202).</summary>
    public static Result<T> Accepted(T value) => new(value, SuccessType.Accepted);

    /// <summary>Crée un résultat de succès avec <see cref="SuccessType.NoContent"/> (HTTP 204), valeur par défaut.</summary>
    public static Result<T> NoContent() => new(default!, SuccessType.NoContent);

    /// <summary>Crée un résultat d'échec à partir d'une <see cref="ResultError"/>.</summary>
    public static Result<T> Failure(ResultError error) => new(error);

    // ──────────────────────────────────────────────────────────────────
    // Implicit converters (DSL ergonomique)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Conversion implicite depuis une valeur : <c>Result&lt;int&gt; r = 42;</c></summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Conversion implicite depuis une erreur : <c>Result&lt;int&gt; r = new ResultError(...);</c></summary>
    public static implicit operator Result<T>(ResultError error) => Failure(error);

    // ──────────────────────────────────────────────────────────────────
    // Match : déconstruit le Result en une valeur unique
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applique l'une des deux fonctions selon l'état du résultat et retourne sa valeur.
    /// </summary>
    /// <typeparam name="TResult">Type de retour commun aux deux branches.</typeparam>
    /// <param name="onSuccess">Fonction appelée en cas de succès, avec la valeur.</param>
    /// <param name="onFailure">Fonction appelée en cas d'échec, avec l'erreur.</param>
    /// <example>
    /// <code>
    /// string label = result.Match(
    ///     onSuccess: u =&gt; $"Hello {u.Name}",
    ///     onFailure: e =&gt; $"Erreur: {e.Code}");
    /// </code>
    /// </example>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<ResultError, TResult> onFailure)
       => IsSuccess
          ? onSuccess(Value)
          : onFailure(Error);

    // ──────────────────────────────────────────────────────────────────
    // Map : transforme la valeur en cas de succès (functor)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transforme la valeur en cas de succès. L'échec et le <see cref="SuccessType"/> sont propagés.
    /// </summary>
    /// <typeparam name="TNew">Type de la valeur transformée.</typeparam>
    /// <param name="mapper">Fonction de transformation.</param>
    /// <example>
    /// <code>
    /// Result&lt;int&gt; r = Result.Success(42);
    /// Result&lt;string&gt; s = r.Map(i =&gt; i.ToString());
    /// </code>
    /// </example>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
       => IsSuccess
          ? Result<TNew>.Success(mapper(Value), SuccessType)
          : Result<TNew>.Failure(Error);

    /// <summary>Version asynchrone de <see cref="Map{TNew}(Func{T, TNew})"/>.</summary>
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
       => IsSuccess
          ? Result<TNew>.Success(await mapper(Value), SuccessType)
          : Result<TNew>.Failure(Error);

    // ──────────────────────────────────────────────────────────────────
    // Bind : enchaîne une opération qui retourne aussi un Result (monad)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enchaîne une opération qui produit elle-même un <see cref="Result{TNew}"/>.
    /// L'erreur est propagée si le résultat courant est en échec ; sinon le binder s'exécute et
    /// son <see cref="SuccessType"/> remplace celui du résultat courant.
    /// </summary>
    /// <typeparam name="TNew">Type du nouveau résultat.</typeparam>
    /// <param name="binder">Fonction qui retourne un nouveau Result à partir de la valeur courante.</param>
    /// <example>
    /// <code>
    /// Result&lt;User&gt; user = FindUser(id);
    /// Result&lt;Order&gt; order = user.Bind(u =&gt; CreateOrder(u));
    /// </code>
    /// </example>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
       => IsSuccess
          ? binder(Value)
          : Result<TNew>.Failure(Error);

    /// <summary>Version asynchrone de <see cref="Bind{TNew}(Func{T, Result{TNew}})"/>.</summary>
    public async Task<Result<TNew>> BindAsync<TNew>(Func<T, Task<Result<TNew>>> binder)
       => IsSuccess
          ? await binder(Value)
          : Result<TNew>.Failure(Error);

    // ──────────────────────────────────────────────────────────────────
    // Tap : side-effect en cas de succès, ne change pas la valeur
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exécute une action sans modifier le résultat — utile pour logger, émettre un event, etc.
    /// </summary>
    /// <param name="action">Action exécutée si le résultat est un succès.</param>
    /// <returns>Le résultat inchangé (chaînable).</returns>
    /// <example>
    /// <code>
    /// result.Tap(u =&gt; logger.LogInformation("Created {Id}", u.Id))
    ///       .Map(u =&gt; u.ToDto());
    /// </code>
    /// </example>
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess)
            action(Value);

        return this;
    }

    /// <summary>Version asynchrone de <see cref="Tap(Action{T})"/>.</summary>
    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        if (IsSuccess)
            await action(Value);

        return this;
    }

    // ──────────────────────────────────────────────────────────────────
    // TapError : side-effect en cas d'échec
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exécute une action sur l'erreur sans modifier le résultat — utile pour logger les échecs.
    /// </summary>
    public Result<T> TapError(Action<ResultError> action)
    {
        if (IsFailure)
            action(Error);

        return this;
    }

    /// <summary>Version asynchrone de <see cref="TapError(Action{ResultError})"/>.</summary>
    public async Task<Result<T>> TapErrorAsync(Func<ResultError, Task> action)
    {
        if (IsFailure)
            await action(Error);

        return this;
    }

    // ──────────────────────────────────────────────────────────────────
    // MapError : transforme l'erreur (réétiquetage, ajout de contexte)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Transforme l'erreur en cas d'échec. Le succès est propagé tel quel.
    /// </summary>
    /// <param name="mapper">Fonction de transformation de l'erreur.</param>
    /// <example>
    /// <code>
    /// result.MapError(e =&gt; e with { Code = $"User.{e.Code}" });
    /// </code>
    /// </example>
    public Result<T> MapError(Func<ResultError, ResultError> mapper)
       => IsFailure ? Result<T>.Failure(mapper(Error)) : this;

    // ──────────────────────────────────────────────────────────────────
    // Recover : convertit une erreur en succès (fallback)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Convertit l'erreur en valeur de succès (fallback). Le succès reste inchangé.
    /// </summary>
    /// <param name="fallback">Fonction produisant une valeur à partir de l'erreur.</param>
    /// <example>
    /// <code>
    /// var settings = LoadSettings().Recover(_ =&gt; Settings.Defaults);
    /// </code>
    /// </example>
    public Result<T> Recover(Func<ResultError, T> fallback)
       => IsFailure ? Result<T>.Success(fallback(Error)) : this;

    /// <summary>
    /// Convertit l'erreur en un nouveau <see cref="Result{T}"/> (peut rester un échec).
    /// </summary>
    /// <param name="fallback">Fonction produisant un résultat à partir de l'erreur.</param>
    public Result<T> Recover(Func<ResultError, Result<T>> fallback)
       => IsFailure ? fallback(Error) : this;

    /// <summary>Version asynchrone de <see cref="Recover(Func{ResultError, T})"/>.</summary>
    public async Task<Result<T>> RecoverAsync(Func<ResultError, Task<T>> fallback)
       => IsFailure ? Result<T>.Success(await fallback(Error)) : this;

    /// <summary>Version asynchrone de <see cref="Recover(Func{ResultError, Result{T}})"/>.</summary>
    public async Task<Result<T>> RecoverAsync(Func<ResultError, Task<Result<T>>> fallback)
       => IsFailure ? await fallback(Error) : this;

    // ──────────────────────────────────────────────────────────────────
    // Ensure : guard clause monadique (validation chaînable)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Vérifie un prédicat sur la valeur : si le prédicat retourne <c>false</c>, transforme le succès en échec.
    /// L'échec préexistant est propagé tel quel.
    /// </summary>
    /// <param name="predicate">Prédicat à satisfaire.</param>
    /// <param name="errorIfFalse">Erreur retournée si le prédicat échoue.</param>
    /// <example>
    /// <code>
    /// result.Ensure(u =&gt; u.Age &gt;= 18, new ResultError("User.TooYoung", "Doit être majeur"))
    ///       .Map(u =&gt; u.ToDto());
    /// </code>
    /// </example>
    public Result<T> Ensure(Func<T, bool> predicate, ResultError errorIfFalse)
    {
        if (IsFailure)
            return this;

        return predicate(Value) ? this : Result<T>.Failure(errorIfFalse);
    }

    /// <summary>Version asynchrone de <see cref="Ensure(Func{T, bool}, ResultError)"/>.</summary>
    public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, ResultError errorIfFalse)
    {
        if (IsFailure)
            return this;

        return await predicate(Value) ? this : Result<T>.Failure(errorIfFalse);
    }
}

/// <summary>
/// Helpers statiques pour construire des <see cref="Result{T}"/> sans avoir à spécifier <c>T</c>
/// explicitement (l'inférence de type fait le reste).
/// </summary>
public static class Result
{
    /// <summary>Crée un <see cref="Result{T}"/> de succès sans valeur (utilise <see cref="Unit"/>).</summary>
    public static Result<Unit> Success(SuccessType type = SuccessType.Ok)
        => Result<Unit>.Success(Unit.Value, type);

    /// <summary>Crée un <see cref="Result{T}"/> de succès — inférence de <c>T</c>.</summary>
    public static Result<T> Success<T>(T value, SuccessType type = SuccessType.Ok)
        => Result<T>.Success(value, type);

    /// <summary>Crée un <see cref="Result{Unit}"/> d'échec.</summary>
    public static Result<Unit> Failure(ResultError error) => Result<Unit>.Failure(error);

    /// <summary>Crée un <see cref="Result{T}"/> d'échec — inférence de <c>T</c>.</summary>
    public static Result<T> Failure<T>(ResultError error) => Result<T>.Failure(error);

    /// <summary>
    /// Exécute une action synchrone qui peut lever une exception et convertit l'exception en
    /// <see cref="ResultError"/>.
    /// </summary>
    /// <param name="action">Action à exécuter ; sa valeur de retour devient la valeur de succès.</param>
    /// <param name="errorMapper">Fonction qui convertit l'exception levée en <see cref="ResultError"/>.</param>
    /// <example>
    /// <code>
    /// var r = Result.Try(
    ///     () =&gt; int.Parse(input),
    ///     ex =&gt; new ResultError("Parse.Failed", ex.Message));
    /// </code>
    /// </example>
    public static Result<T> Try<T>(Func<T> action, Func<Exception, ResultError> errorMapper)
    {
        try
        {
            return Result<T>.Success(action());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(errorMapper(ex));
        }
    }

    /// <summary>Version asynchrone de <see cref="Try{T}(Func{T}, Func{Exception, ResultError})"/>.</summary>
    public static async Task<Result<T>> TryAsync<T>(Func<Task<T>> action, Func<Exception, ResultError> errorMapper)
    {
        try
        {
            return Result<T>.Success(await action());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(errorMapper(ex));
        }
    }
}

/// <summary>
/// Catégorise une erreur métier pour permettre un mapping automatique vers un code HTTP de réponse.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Erreurs de validation : données d'entrée ne respectant pas les contraintes
    /// (champ manquant, format invalide, valeur hors plage).
    /// </summary>
    /// <remarks>Équivalent à HTTP 400 Bad Request.</remarks>
    Validation,

    /// <summary>
    /// Utilisateur non authentifié ou informations d'identification invalides.
    /// </summary>
    /// <remarks>Équivalent à HTTP 401 Unauthorized.</remarks>
    Unauthorized,

    /// <summary>
    /// Utilisateur authentifié mais sans les permissions nécessaires pour accéder à la ressource
    /// ou effectuer l'action demandée.
    /// </summary>
    /// <remarks>Équivalent à HTTP 403 Forbidden.</remarks>
    Forbidden,

    /// <summary>
    /// Ressource demandée introuvable.
    /// </summary>
    /// <remarks>Équivalent à HTTP 404 Not Found.</remarks>
    NotFound,

    /// <summary>
    /// Conflit avec l'état actuel de la ressource (ex. version stale, doublon).
    /// </summary>
    /// <remarks>Équivalent à HTTP 409 Conflict.</remarks>
    Conflict,

    /// <summary>
    /// Échec générique sans cause spécifique.
    /// </summary>
    /// <remarks>Équivalent à HTTP 500 Internal Server Error.</remarks>
    Failure,
}

/// <summary>
/// Catégorise un succès pour permettre un mapping automatique vers un code HTTP 2xx.
/// </summary>
public enum SuccessType
{
    /// <summary>Réponse standard avec données. HTTP 200 OK.</summary>
    Ok,
    /// <summary>Nouvelle ressource créée. HTTP 201 Created.</summary>
    Created,
    /// <summary>Requête acceptée pour traitement asynchrone. HTTP 202 Accepted.</summary>
    Accepted,
    /// <summary>Succès sans contenu à retourner. HTTP 204 No Content.</summary>
    NoContent,
}

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

    /// <summary>
    /// Dictionnaire des erreurs par champ, utilisé pour <see cref="ErrorType.Validation"/>.
    /// La clé est le nom du champ, la valeur la liste immuable des erreurs sur ce champ.
    /// </summary>
    public ImmutableDictionary<string, ImmutableArray<ValidationFieldError>>? ValidationErrors { get; init; }

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
            && ValidationErrorsEqual(ValidationErrors, other.ValidationErrors);
    }

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Code, Message, Type, _args.Length);

    private static bool ValidationErrorsEqual(
       ImmutableDictionary<string, ImmutableArray<ValidationFieldError>>? a,
       ImmutableDictionary<string, ImmutableArray<ValidationFieldError>>? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
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
