using System.Collections.Immutable;
using System.Reflection;
using NetArchTest.Rules;

namespace Architecture.Tests;

/// <summary>
/// Tests d'architecture sur les abstractions Domain. Vérifie les contrats structurels
/// (types de propriétés, dépendances entre namespaces) qui ne peuvent pas être garantis
/// par le compilateur seul.
/// </summary>
/// <remarks>
/// <para>
/// Pour le contrat « <c>ResultError.Args</c> = uniquement des primitifs » :
/// l'enforcement complet exigerait un Roslyn analyzer (scan des call sites de
/// <c>new ResultError(...)</c>). À défaut, on combine :
/// </para>
/// <list type="number">
///   <item>Validation runtime dans l'<c>init</c> setter (voir <c>ResultErrorArgsValidator</c>).</item>
///   <item>Tests unitaires qui exercent la validation (voir <c>Domain.UnitTests.Abstractions.ResultErrorTests</c>).</item>
///   <item>Tests structurels ci-dessous (typage des propriétés).</item>
/// </list>
/// </remarks>
public class DomainAbstractionsArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(ResultError).Assembly;

    public class StructuralContracts
    {
        [Fact]
        public void ResultError_Args_is_ImmutableArray_of_object()
        {
            PropertyInfo args = typeof(ResultError).GetProperty(nameof(ResultError.Args))!;
            args.PropertyType.ShouldBe(typeof(ImmutableArray<object>));
        }

        [Fact]
        public void ResultError_ValidationErrors_is_ImmutableDictionary_of_ImmutableArray()
        {
            PropertyInfo validationErrors = typeof(ResultError).GetProperty(nameof(ResultError.ValidationErrors))!;
            validationErrors.PropertyType.ShouldBe(
                typeof(ImmutableDictionary<string, ImmutableArray<ValidationFieldError>>));
        }

        [Fact]
        public void ValidationFieldError_Args_is_ImmutableArray_of_object()
        {
            PropertyInfo args = typeof(ValidationFieldError).GetProperty(nameof(ValidationFieldError.Args))!;
            args.PropertyType.ShouldBe(typeof(ImmutableArray<object>));
        }

        [Fact]
        public void ResultErrorArgsValidator_is_internal()
        {
            // L'enforcement du contrat ne doit pas être exposé publiquement
            // (sinon il serait facile de le contourner / d'introduire des dépendances).
            Type? validator = DomainAssembly.GetType("Domain.Abstractions.ResultErrorArgsValidator");
            validator.ShouldNotBeNull();
            validator.IsNotPublic.ShouldBeTrue();
        }
    }

    public class CleanArchitectureBoundaries
    {
        [Fact]
        public void Domain_does_not_depend_on_AspNetCore()
        {
            TestResult result = Types.InAssembly(DomainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.AspNetCore")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Types fautifs : {Format(result)}");
        }

        [Fact]
        public void Domain_does_not_depend_on_EntityFrameworkCore()
        {
            TestResult result = Types.InAssembly(DomainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.EntityFrameworkCore")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Types fautifs : {Format(result)}");
        }

        [Fact]
        public void Domain_does_not_depend_on_SystemTextJson()
        {
            TestResult result = Types.InAssembly(DomainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny("System.Text.Json")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Types fautifs : {Format(result)}");
        }

        [Fact]
        public void Domain_does_not_depend_on_ComponentModel()
        {
            TestResult result = Types.InAssembly(DomainAssembly)
                .ShouldNot()
                .HaveDependencyOnAny("System.ComponentModel")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(
                $"Types fautifs : {Format(result)}");
        }

        private static string Format(TestResult result)
            => result.FailingTypes is null || result.FailingTypes.Count == 0
                ? "(aucun)"
                : string.Join(", ", result.FailingTypes.Select(t => t.FullName));
    }
}
