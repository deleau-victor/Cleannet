using Infrastructure.Data.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Extensions;

internal static class ModelConfigurationBuilderExtensions
{
    extension(ModelConfigurationBuilder configurationBuilder)
    {
        /// <summary>
        /// Enregistre <see cref="IdConvention"/>, qui applique le mapping ULID-as-char(26)
        /// aux propriétés de type <see cref="Id"/>, <see cref="Id"/>? et <see cref="Id{TEntity}"/>.
        /// </summary>
        public void UseDomainIdConventions()
            => configurationBuilder.Conventions.Add(_ => new IdConvention());
    }
}
