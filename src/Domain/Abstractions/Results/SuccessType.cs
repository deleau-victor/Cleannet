namespace Domain.Abstractions.Results;

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
