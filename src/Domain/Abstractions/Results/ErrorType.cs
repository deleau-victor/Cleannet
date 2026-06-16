namespace Domain.Abstractions.Results;

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

