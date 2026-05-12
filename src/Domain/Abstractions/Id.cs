namespace Domain.Abstractions;

/// <summary>
/// Représente un identifiant unique.
/// <br/>
/// Exemple: "01F8MECHZX3TBDSZ7XRADM79XV".
/// </summary>
/// <remarks>
/// Basé sur ULID, cet identifiant peut être ordonné temporellement.
/// </remarks>
public readonly record struct Id : ISpanParsable<Id>, ISpanFormattable, IComparable, IComparable<Id>
{
    private readonly Ulid _value;

    private Id(Ulid value) => _value = value;

    /// <summary>
    /// Retourne un ID vide (ULID avec tous les bits à zéro).
    /// </summary>
    public static Id Empty => new(Ulid.Empty);

    /// <summary>
    /// Génère un nouvel ID.
    /// </summary>
    public static Id New() => new(Ulid.NewUlid());

    /// <summary>
    /// Indique si l'ID est vide (ULID avec tous les bits à zéro).
    /// </summary>
    public bool IsEmpty => _value == Ulid.Empty;

    /// <summary>
    /// Convertit en <see cref="Ulid"/> pour accéder à l'API ULID.
    /// </summary>
    public Ulid ToUlid() => _value;

    /// <summary>
    /// Convertit en <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// Perte du hash temporel permettant la sortabilité, mais compatible avec les systèmes utilisant des GUIDs.
    /// </remarks>
    public Guid ToGuid() => _value.ToGuid();

    /// <summary>
    /// Crée un <see cref="Id"/> à partir d'un <see cref="Ulid"/>.
    /// </summary>
    public static Id FromUlid(Ulid ulid) => new(ulid);

    /// <summary>
    /// Crée un <see cref="Id"/> à partir d'un <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// Les 48 premiers bits du GUID sont interprétés comme le timestamp ULID ; ils ne reflètent donc pas
    /// le moment de création réel. L'ID résultant n'est pas temporellement ordonnable comme un ULID natif.
    /// </remarks>
    public static Id FromGuid(Guid value) => new(new Ulid(value));

    /// <summary>
    /// Conversion explicite de <see cref="Id"/> à <see cref="Ulid"/> ou <see cref="Guid"/>.
    /// </summary>
    public static explicit operator Ulid(Id id) => id._value;

    /// <summary>
    /// Conversion explicite de <see cref="Id"/> à <see cref="Guid"/>.
    /// </summary>
    public static explicit operator Guid(Id id) => id.ToGuid();

    /// <summary>
    /// Conversion explicite de <see cref="Ulid"/> à <see cref="Id"/>.
    /// </summary>
    public static explicit operator Id(Ulid ulid) => FromUlid(ulid);

    /// <summary>
    /// Conversion explicite de <see cref="Guid"/> à <see cref="Id"/>.
    /// </summary>
    public static explicit operator Id(Guid value) => FromGuid(value);

    /// <inheritdoc/>
    public override string ToString() => _value.ToString();

    /// <inheritdoc cref="Id.ToString()"/>
    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => _value.TryFormat(destination, out charsWritten, format, provider);

    /// <summary>
    /// Parse une chaîne de caractères représentant un ULID ou GUID en un <see cref="Id"/>.
    /// </summary>
    /// <exception cref="FormatException">Si la chaîne n'est pas un ULID ou GUID valide.</exception>
    public static Id Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    /// <summary>
    /// Tente de parser une chaîne de caractères représentant un ULID ou GUID en un <see cref="Id"/>.
    /// </summary>
    public static bool TryParse(string? s, IFormatProvider? provider, out Id result)
        => TryParse(s.AsSpan(), provider, out result);

    /// <summary>
    /// Méthode de parsing optimisée pour les ULID ou GUID à partir d'un <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static Id Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (Ulid.TryParse(s, out Ulid ulid))
            return new Id(ulid);
        if (Guid.TryParse(s, out Guid guid))
            return FromGuid(guid);
        throw new FormatException($"Not a ULID or GUID: {s}");
    }

    /// <summary>
    /// Méthode de parsing optimisée pour les ULID ou GUID à partir d'un <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Id result)
    {
        if (Ulid.TryParse(s, out Ulid ulid))
        { result = new Id(ulid); return true; }

        if (Guid.TryParse(s, out Guid guid))
        { result = FromGuid(guid); return true; }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj)
    {
        return obj switch
        {
            null => 1,
            Id other => CompareTo(other),
            _ => throw new ArgumentException($"Object must be of type {nameof(Id)}", nameof(obj))
        };
    }

    /// <inheritdoc/>
    public int CompareTo(Id other) => _value.CompareTo(other._value);

    public static bool operator <(Id left, Id right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Id left, Id right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Id left, Id right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Id left, Id right)
    {
        return left.CompareTo(right) >= 0;
    }
}

/// <summary>
/// Identifiant unique typé par entité.
/// <br/>
/// Wrapper sur <see cref="Id"/> ajoutant la sécurité de typage au compile-time :
/// un <c>Id&lt;User&gt;</c> n'est pas assignable à un <c>Id&lt;Order&gt;</c>.
/// </summary>
/// <typeparam name="TEntity">
/// Type marqueur identifiant l'entité (phantom type, sans représentation runtime).
/// </typeparam>
public readonly record struct Id<TEntity> : ISpanParsable<Id<TEntity>>, ISpanFormattable, IComparable, IComparable<Id<TEntity>>
{
    private readonly Id _value;

    private Id(Id value) => _value = value;

    /// <inheritdoc cref="Id.Empty"/>
    public static Id<TEntity> Empty => new(Id.Empty);

    /// <inheritdoc cref="Id.New"/>
    public static Id<TEntity> New() => new(Id.New());

    /// <inheritdoc cref="Id.IsEmpty"/>
    public bool IsEmpty => _value.IsEmpty;

    /// <summary>
    /// Retourne le <see cref="Id"/> sous-jacent (échappatoire vers le type non-typé, pour les contextes type-erased).
    /// </summary>
    public Id ToId() => _value;

    /// <inheritdoc cref="Id.ToUlid"/>
    public Ulid ToUlid() => _value.ToUlid();

    /// <inheritdoc cref="Id.ToGuid"/>
    public Guid ToGuid() => _value.ToGuid();

    /// <summary>
    /// Crée un <see cref="Id{TEntity}"/> à partir d'un <see cref="Id"/> non-typé.
    /// </summary>
    public static Id<TEntity> FromId(Id id) => new(id);

    /// <inheritdoc cref="Id.FromUlid"/>
    public static Id<TEntity> FromUlid(Ulid ulid) => new(Id.FromUlid(ulid));

    /// <inheritdoc cref="Id.FromGuid"/>
    public static Id<TEntity> FromGuid(Guid value) => new(Id.FromGuid(value));

    /// <summary>
    /// Conversion explicite vers le <see cref="Id"/> non-typé.
    /// </summary>
    public static explicit operator Id(Id<TEntity> id) => id._value;

    /// <summary>
    /// Conversion explicite depuis un <see cref="Id"/> non-typé.
    /// </summary>
    public static explicit operator Id<TEntity>(Id id) => FromId(id);

    /// <inheritdoc/>
    public override string ToString() => _value.ToString();

    /// <inheritdoc cref="Id.ToString(string?, IFormatProvider?)"/>
    public string ToString(string? format, IFormatProvider? formatProvider) => _value.ToString(format, formatProvider);

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => _value.TryFormat(destination, out charsWritten, format, provider);

    /// <inheritdoc cref="Id.Parse(string, IFormatProvider?)"/>
    public static Id<TEntity> Parse(string s, IFormatProvider? provider) => new(Id.Parse(s, provider));

    /// <inheritdoc cref="Id.Parse(ReadOnlySpan{char}, IFormatProvider?)"/>
    public static Id<TEntity> Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => new(Id.Parse(s, provider));

    /// <inheritdoc cref="Id.TryParse(string?, IFormatProvider?, out Id)"/>
    public static bool TryParse(string? s, IFormatProvider? provider, out Id<TEntity> result)
    {
        if (Id.TryParse(s, provider, out Id id))
        {
            result = new(id);
            return true;
        }

        result = default;
        return false;
    }

    /// <inheritdoc cref="Id.TryParse(ReadOnlySpan{char}, IFormatProvider?, out Id)"/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out Id<TEntity> result)
    {
        if (Id.TryParse(s, provider, out Id id))
        {
            result = new(id);
            return true;
        }

        result = default;
        return false;
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj)
    {
        return obj switch
        {
            null => 1,
            Id<TEntity> other => CompareTo(other),
            _ => throw new ArgumentException($"Object must be of type Id<{typeof(TEntity).Name}>", nameof(obj))
        };
    }

    /// <inheritdoc/>
    public int CompareTo(Id<TEntity> other) => _value.CompareTo(other._value);

    public static bool operator <(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) < 0;
    public static bool operator <=(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) <= 0;
    public static bool operator >(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) > 0;
    public static bool operator >=(Id<TEntity> left, Id<TEntity> right) => left.CompareTo(right) >= 0;
}
