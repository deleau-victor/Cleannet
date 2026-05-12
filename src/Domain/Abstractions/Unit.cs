namespace Domain.Abstractions;

/// <summary>
/// Type représentant l'absence de valeur, équivalent fonctionnel à <c>void</c> dans un contexte générique.
/// </summary>
/// <remarks>
/// Utilisé par <see cref="Result{T}"/> et <see cref="Result"/> pour exprimer un succès sans payload
/// (ex. <see cref="Result.Success(SuccessType)"/> retourne un <see cref="Result{T}"/> où <c>T = Unit</c>).
/// </remarks>
public readonly record struct Unit
{
    /// <summary>
    /// L'unique instance de <see cref="Unit"/>. Équivalent à <c>default(Unit)</c>.
    /// </summary>
    public static Unit Value => default;
}
