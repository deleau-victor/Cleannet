using System.Collections.Immutable;

namespace Domain.Abstractions.Results;

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
    private readonly T? _value;
    private readonly ResultError? _error;
    private readonly ResultState _state;

    /// <summary>
    /// Indique si le résultat est un succès ou un échec. Un résultat non initialisé est considéré comme invalide.
    /// </summary>
    public bool IsSuccess => _state == ResultState.Success;

    /// <summary>
    /// Indique si le résultat est un échec. Un résultat non initialisé est considéré comme invalide.
    /// </summary>
    public bool IsFailure => _state == ResultState.Failure;

    /// <summary>
    /// La valeur du résultat en cas de succès.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si le résultat est un échec.</exception>
    /// <exception cref="InvalidOperationException">Si le résultat est non initialisé.</exception>
    public T Value => _state switch
    {
        ResultState.Uninitialized => throw new InvalidOperationException("Result is uninitialized"),
        ResultState.Success => _value!,
        ResultState.Failure => throw new InvalidOperationException("No value for failure result"),
        _ => throw new InvalidOperationException("Invalid result state")
    };

    /// <summary>
    /// L'erreur du résultat en cas d'échec.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si le résultat est un succès.</exception>
    /// <exception cref="InvalidOperationException">Si le résultat est non initialisé.</exception>
    public ResultError Error => _state switch
    {
        ResultState.Uninitialized => throw new InvalidOperationException("Result is uninitialized"),
        ResultState.Success => throw new InvalidOperationException("No error for success result"),
        ResultState.Failure => _error!,
        _ => throw new InvalidOperationException("Invalid result state")
    };

    /// <summary>
    /// Le type de succès, mappable sur un code HTTP 2xx (Ok, Created, Accepted, NoContent).
    /// </summary>
    /// <remarks>En cas d'échec, vaut <see cref="SuccessType.Ok"/> par défaut mais n'a pas de signification.</remarks>
    public SuccessType SuccessType { get; }

    private Result(T value, SuccessType successType)
    {
        _state = ResultState.Success;
        _value = value;
        _error = null;
        SuccessType = successType;
    }

    private Result(ResultError error)
    {
        _state = ResultState.Failure;
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
    // Match : déconstruit le Result en une valeur unique
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applique l'une des deux fonctions selon l'état du résultat et retourne sa valeur.
    /// </summary>
    /// <typeparam name="TResult">Type de retour commun aux deux branches.</typeparam>
    /// <param name="onSuccess">Fonction appelée en cas de succès, avec la valeur.</param>
    /// <param name="onFailure">Fonction appelée en cas d'échec, avec l'erreur.</param>
    /// <example>
    ///     <code>
    ///     string label = result.Match(
    ///         onSuccess: u =&gt; $"Hello {u.Name}",
    ///         onFailure: e =&gt; $"Erreur: {e.Code}");
    ///     </code>
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
        ThrowIfUninitialized();
        if (IsSuccess)
            action(Value);

        return this;
    }

    /// <summary>Version asynchrone de <see cref="Tap(Action{T})"/>.</summary>
    public async Task<Result<T>> TapAsync(Func<T, Task> action)
    {
        ThrowIfUninitialized();
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
        ThrowIfUninitialized();
        if (IsFailure)
            action(Error);

        return this;
    }

    /// <summary>Version asynchrone de <see cref="TapError(Action{ResultError})"/>.</summary>
    public async Task<Result<T>> TapErrorAsync(Func<ResultError, Task> action)
    {
        ThrowIfUninitialized();
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
    {
        ThrowIfUninitialized();
        return IsFailure ? Result<T>.Failure(mapper(Error)) : this;
    }

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
    {
        ThrowIfUninitialized();
        return IsFailure ? Result<T>.Success(fallback(Error)) : this;
    }

    /// <summary>
    /// Convertit l'erreur en un nouveau <see cref="Result{T}"/> (peut rester un échec).
    /// </summary>
    /// <param name="fallback">Fonction produisant un résultat à partir de l'erreur.</param>
    public Result<T> Recover(Func<ResultError, Result<T>> fallback)
    {
        ThrowIfUninitialized();
        return IsFailure ? fallback(Error) : this;
    }

    /// <summary>Version asynchrone de <see cref="Recover(Func{ResultError, T})"/>.</summary>
    public async Task<Result<T>> RecoverAsync(Func<ResultError, Task<T>> fallback)
    {
        ThrowIfUninitialized();
        return IsFailure ? Result<T>.Success(await fallback(Error)) : this;
    }

    /// <summary>Version asynchrone de <see cref="Recover(Func{ResultError, Result{T}})"/>.</summary>
    public async Task<Result<T>> RecoverAsync(Func<ResultError, Task<Result<T>>> fallback)
    {
        ThrowIfUninitialized();
        return IsFailure ? await fallback(Error) : this;
    }

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

    /// <summary>
    /// Variante de <see cref="Ensure(Func{T, bool}, ResultError)"/> où l'erreur est construite à partir
    /// de la valeur, utile pour des messages d'erreur paramétrés par l'état courant.
    /// </summary>
    /// <param name="predicate">Prédicat à satisfaire.</param>
    /// <param name="errorBuilder">Fonction qui construit l'erreur à partir de la valeur lorsque le prédicat échoue.</param>
    /// <example>
    /// <code>
    /// result.Ensure(
    ///     u =&gt; u.Age &gt;= 18,
    ///     u =&gt; new ResultError("User.TooYoung", "Âge {0} insuffisant", [u.Age]));
    /// </code>
    /// </example>
    public Result<T> Ensure(Func<T, bool> predicate, Func<T, ResultError> errorBuilder)
    {
        if (IsFailure)
            return this;

        return predicate(Value) ? this : Result<T>.Failure(errorBuilder(Value));
    }

    /// <summary>Version asynchrone de <see cref="Ensure(Func{T, bool}, Func{T, ResultError})"/>.</summary>
    public async Task<Result<T>> EnsureAsync(Func<T, Task<bool>> predicate, Func<T, ResultError> errorBuilder)
    {
        if (IsFailure)
            return this;

        return await predicate(Value) ? this : Result<T>.Failure(errorBuilder(Value));
    }

    // ──────────────────────────────────────────────────────────────────
    // Implicit converters (DSL ergonomique)
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Conversion implicite depuis une valeur : <c>Result&lt;int&gt; r = 42;</c></summary>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>Conversion implicite depuis une erreur : <c>Result&lt;int&gt; r = new ResultError(...);</c></summary>
    public static implicit operator Result<T>(ResultError error) => Failure(error);

    // ──────────────────────────────────────────────────────────────────
    // Guards privés
    // ──────────────────────────────────────────────────────────────────

    private bool IsInitialized => _state != ResultState.Uninitialized;

    private void ThrowIfUninitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Result is uninitialized");
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

    // ──────────────────────────────────────────────────────────────────
    // Combine : fail-fast, premier échec gagne
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Combine N <see cref="Result{T}"/> en un <see cref="Result{TList}"/> de type <see cref="IReadOnlyList{T}"/>.
    /// Sémantique <strong>fail-fast</strong> : retourne la première erreur rencontrée sans évaluer les Results suivants
    /// (note : les Results déjà matérialisés sont quand même itérés ; seul l'agrégation s'arrête).
    /// </summary>
    /// <typeparam name="T">Type des valeurs.</typeparam>
    /// <param name="results">Séquence de Results à combiner.</param>
    /// <returns>Result de succès contenant toutes les valeurs, ou Result d'échec avec la première erreur.</returns>
    /// <example>
    /// <code>
    /// Result.Combine(loadUser, loadOrder, loadInvoice)
    ///     .Map(items =&gt; new Aggregate(items[0], items[1], items[2]));
    /// </code>
    /// </example>
    public static Result<IReadOnlyList<T>> Combine<T>(IEnumerable<Result<T>> results)
    {
        List<T> values = [];
        foreach (Result<T> r in results)
        {
            if (r.IsFailure)
                return r.Error;
            values.Add(r.Value);
        }

        return values;
    }

    /// <summary>Surcharge <c>params</c> de <see cref="Combine{T}(IEnumerable{Result{T}})"/>.</summary>
    public static Result<IReadOnlyList<T>> Combine<T>(params Result<T>[] results)
        => Combine((IEnumerable<Result<T>>)results);

    // ──────────────────────────────────────────────────────────────────
    // Aggregate : collecte toutes les erreurs en ValidationErrors indexés
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Combine N <see cref="Result{T}"/> en un <see cref="Result{TList}"/> de type <see cref="IReadOnlyList{T}"/>.
    /// Sémantique <strong>aggregate</strong> : collecte toutes les erreurs dans
    /// <see cref="ResultError.ValidationErrors"/> indexées par position (<c>"[0]"</c>, <c>"[1]"</c>, ...).
    /// </summary>
    /// <typeparam name="T">Type des valeurs.</typeparam>
    /// <param name="results">Séquence de Results à agréger.</param>
    /// <returns>
    /// Result de succès contenant toutes les valeurs si aucun échec ; sinon Result d'échec de type
    /// <see cref="ErrorType.Validation"/> avec un dictionnaire indexé des erreurs.
    /// </returns>
    /// <example>
    /// <code>
    /// Result.Aggregate(validatePseudo, validateEmail, validateAge)
    ///     .Match(
    ///         _   =&gt; "OK",
    ///         err =&gt; string.Join(", ", err.ValidationErrors.Keys));
    /// </code>
    /// </example>
    public static Result<IReadOnlyList<T>> Aggregate<T>(IEnumerable<Result<T>> results)
    {
        List<T> values = [];
        ImmutableDictionary<string, ImmutableArray<ValidationFieldError>>.Builder errors
            = ImmutableDictionary.CreateBuilder<string, ImmutableArray<ValidationFieldError>>();
        int index = 0;

        foreach (Result<T> r in results)
        {
            if (r.IsFailure)
            {
                ValidationFieldError fieldError = r.Error.Args.IsDefaultOrEmpty
                    ? new ValidationFieldError(r.Error.Code)
                    : new ValidationFieldError(r.Error.Code, r.Error.Args);
                errors.Add($"[{index}]", [fieldError]);
            }
            else
            {
                values.Add(r.Value);
            }

            index++;
        }

        if (errors.Count > 0)
        {
            return new ResultError(
                "Aggregate.ValidationFailed",
                "Une ou plusieurs validations ont échoué",
                ErrorType.Validation)
            {
                ValidationErrors = errors.ToImmutable()
            };
        }

        return values;
    }

    /// <summary>Surcharge <c>params</c> de <see cref="Aggregate{T}(IEnumerable{Result{T}})"/>.</summary>
    public static Result<IReadOnlyList<T>> Aggregate<T>(params Result<T>[] results)
        => Aggregate((IEnumerable<Result<T>>)results);
}
