using Domain.Abstractions.Results;
using System.Collections.Immutable;
using System.Globalization;

namespace Domain.UnitTests.Abstractions;

public class ResultErrorTests
{
    public class ArgsValidation
    {
        [Theory]
        [InlineData("hello")]
        [InlineData(42)]
        [InlineData(42L)]
        [InlineData(true)]
        [InlineData(3.14)]
        [InlineData(3.14f)]
        [InlineData('x')]
        public void Allows_primitive_types_and_string(object primitive)
        {
            Action act = () => _ = new ResultError("X", "msg") { Args = [primitive] };
            act.ShouldNotThrow();
        }

        [Fact]
        public void Allows_decimal()
        {
            Action act = () => _ = new ResultError("X", "msg") { Args = [42m] };
            act.ShouldNotThrow();
        }

        [Fact]
        public void Allows_null_in_args()
        {
            Action act = () => _ = new ResultError("X", "msg") { Args = [null!] };
            act.ShouldNotThrow();
        }

        [Fact]
        public void Allows_empty_args()
        {
            Action act = () => _ = new ResultError("X", "msg") { Args = [] };
            act.ShouldNotThrow();
        }

        [Fact]
        public void Allows_default_ImmutableArray_via_init()
        {
            // Default ImmutableArray is normalized to Empty by the init setter
            var error = new ResultError("X", "msg") { Args = default };
            error.Args.IsEmpty.ShouldBeTrue();
        }

        [Fact]
        public void Rejects_DateTime()
        {
            Action act = () => _ = new ResultError("X", "msg") { Args = [DateTime.Now] };
            act.ShouldThrow<ArgumentException>()
               .Message.ShouldContain(nameof(DateTime));
        }

        [Fact]
        public void Rejects_enum()
        {
            Action act = () => _ = new ResultError("X", "msg") { Args = [ErrorType.Validation] };
            act.ShouldThrow<ArgumentException>()
               .Message.ShouldContain(nameof(ErrorType));
        }

        [Fact]
        public void Rejects_complex_object()
        {
            Action act = () => _ = new ResultError("X", "msg") { Args = [new { Foo = "bar" }] };
            act.ShouldThrow<ArgumentException>();
        }

        [Fact]
        public void Validation_runs_on_with_expression()
        {
            // `with { Args = ... }` must also pass through the init setter (validation)
            var baseError = new ResultError("X", "msg");
            Action act = () => _ = baseError with { Args = [DateTime.Now] };
            act.ShouldThrow<ArgumentException>();
        }
    }

    public class Formatting
    {
        [Fact]
        public void FormattedMessage_returns_template_when_no_args()
        {
            var error = new ResultError("X", "Plain message");
            error.FormattedMessage().ShouldBe("Plain message");
        }

        [Fact]
        public void FormattedMessage_substitutes_args()
        {
            var error = new ResultError("X", "Pseudo doit faire entre {0} et {1} caractères")
            {
                Args = [3, 16]
            };
            error.FormattedMessage().ShouldBe("Pseudo doit faire entre 3 et 16 caractères");
        }

        [Fact]
        public void FormattedMessage_uses_invariant_culture_by_default()
        {
            var error = new ResultError("X", "Prix: {0}") { Args = [3.5m] };
            error.FormattedMessage().ShouldBe("Prix: 3.5");
        }

        [Fact]
        public void FormattedMessage_honors_custom_provider()
        {
            var error = new ResultError("X", "Prix: {0}") { Args = [3.5m] };
            var fr = CultureInfo.GetCultureInfo("fr-FR");
            error.FormattedMessage(fr).ShouldBe("Prix: 3,5");
        }

        [Fact]
        public void Message_property_remains_the_raw_template()
        {
            // Important : Message ne doit PAS être pré-formaté ; il reste la clé i18n.
            var error = new ResultError("X", "Valeur {0}") { Args = [42] };
            error.Message.ShouldBe("Valeur {0}");
        }
    }

    public class Equality
    {
        [Fact]
        public void Two_errors_with_same_data_are_equal()
        {
            var a = new ResultError("X", "msg", ErrorType.Validation) { Args = [1, "two"] };
            var b = new ResultError("X", "msg", ErrorType.Validation) { Args = [1, "two"] };
            a.ShouldBe(b);
            a.GetHashCode().ShouldBe(b.GetHashCode());
        }

        [Fact]
        public void Different_args_break_equality()
        {
            var a = new ResultError("X", "msg") { Args = [1] };
            var b = new ResultError("X", "msg") { Args = [2] };
            a.ShouldNotBe(b);
        }
    }
}
