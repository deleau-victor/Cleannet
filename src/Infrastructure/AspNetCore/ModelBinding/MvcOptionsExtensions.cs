using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.AspNetCore.ModelBinding;

public static class MvcOptionsExtensions
{
    extension(MvcOptions options)
    {
        /// <summary>
        /// Enregistre <see cref="IdModelBinderProvider"/> en tête de la chaîne de providers,
        /// activant le binding MVC de <see cref="Id"/> et <see cref="Id{TEntity}"/> depuis
        /// les route values, query strings et form fields.
        /// </summary>
        /// <remarks>
        /// Non requis pour les Minimal APIs : <c>Id</c> et <c>Id&lt;T&gt;</c> implémentent
        /// <see cref="IParsable{TSelf}"/> et sont liés automatiquement.
        /// </remarks>
        public void AddTypedIdModelBinding()
            => options.ModelBinderProviders.Insert(0, new IdModelBinderProvider());
    }
}
