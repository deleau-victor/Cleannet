namespace Domain.UnitTests.Abstractions;

public class ResultTests
{
    private static readonly ResultError SampleError = new("X.Failed", "Échec {0}") { Args = ["test"] };

    public class Factories
    {
        [Fact]
        public void Success_creates_Ok_by_default()
        {
            var r = Result<int>.Success(42);
            r.IsSuccess.ShouldBeTrue();
            r.Value.ShouldBe(42);
            r.SuccessType.ShouldBe(SuccessType.Ok);
        }

        [Fact]
        public void Created_returns_Created_type()
        {
            var r = Result<int>.Created(42);
            r.SuccessType.ShouldBe(SuccessType.Created);
        }

        [Fact]
        public void Failure_carries_error()
        {
            var r = Result<int>.Failure(SampleError);
            r.IsFailure.ShouldBeTrue();
            r.Error.ShouldBe(SampleError);
        }

        [Fact]
        public void Value_on_failure_throws()
        {
            var r = Result<int>.Failure(SampleError);
            Should.Throw<InvalidOperationException>(() => _ = r.Value);
        }

        [Fact]
        public void Error_on_success_throws()
        {
            var r = Result<int>.Success(42);
            Should.Throw<InvalidOperationException>(() => _ = r.Error);
        }
    }

    public class ImplicitConversion
    {
        [Fact]
        public void From_value()
        {
            Result<int> r = 42;
            r.IsSuccess.ShouldBeTrue();
            r.Value.ShouldBe(42);
        }

        [Fact]
        public void From_error()
        {
            Result<int> r = SampleError;
            r.IsFailure.ShouldBeTrue();
        }
    }

    public class FunctionalApi
    {
        [Fact]
        public void Map_transforms_success_and_preserves_SuccessType()
        {
            var r = Result<int>.Created(42).Map(i => i.ToString());
            r.Value.ShouldBe("42");
            r.SuccessType.ShouldBe(SuccessType.Created);
        }

        [Fact]
        public void Map_propagates_failure_without_invoking_mapper()
        {
            int callCount = 0;
            var r = Result<int>.Failure(SampleError).Map(i => { callCount++; return i.ToString(); });
            r.IsFailure.ShouldBeTrue();
            callCount.ShouldBe(0);
        }

        [Fact]
        public void Bind_chains_to_new_result()
        {
            var r = Result<int>.Success(42).Bind(i => Result<string>.Success(i.ToString()));
            r.Value.ShouldBe("42");
        }

        [Fact]
        public void Bind_uses_binder_SuccessType_not_source_SuccessType()
        {
            var r = Result<int>.Created(42).Bind(i => Result<string>.Accepted(i.ToString()));
            r.SuccessType.ShouldBe(SuccessType.Accepted);
        }

        [Fact]
        public void Match_dispatches_to_success_or_failure_branch()
        {
            var ok = Result<int>.Success(42);
            ok.Match(i => $"v={i}", e => $"e={e.Code}").ShouldBe("v=42");

            var ko = Result<int>.Failure(SampleError);
            ko.Match(i => $"v={i}", e => $"e={e.Code}").ShouldBe("e=X.Failed");
        }

        [Fact]
        public void Tap_runs_on_success_only()
        {
            int sideEffect = 0;
            Result<int>.Success(42).Tap(_ => sideEffect++);
            Result<int>.Failure(SampleError).Tap(_ => sideEffect++);
            sideEffect.ShouldBe(1);
        }

        [Fact]
        public void TapError_runs_on_failure_only()
        {
            int sideEffect = 0;
            Result<int>.Success(42).TapError(_ => sideEffect++);
            Result<int>.Failure(SampleError).TapError(_ => sideEffect++);
            sideEffect.ShouldBe(1);
        }

        [Fact]
        public void MapError_rewraps_failure_without_affecting_success()
        {
            var ok = Result<int>.Success(42).MapError(_ => new ResultError("Y", "ignored"));
            ok.IsSuccess.ShouldBeTrue();

            var ko = Result<int>.Failure(SampleError)
                .MapError(e => e with { Code = $"Wrapped.{e.Code}" });
            ko.Error.Code.ShouldBe("Wrapped.X.Failed");
        }

        [Fact]
        public void Recover_converts_failure_to_success()
        {
            var r = Result<int>.Failure(SampleError).Recover(_ => -1);
            r.IsSuccess.ShouldBeTrue();
            r.Value.ShouldBe(-1);
        }

        [Fact]
        public void Recover_leaves_success_untouched()
        {
            var r = Result<int>.Success(42).Recover(_ => -1);
            r.Value.ShouldBe(42);
        }

        [Fact]
        public void Ensure_turns_success_into_failure_when_predicate_fails()
        {
            var guard = new ResultError("Guard.Failed", "Predicate failed");
            var r = Result<int>.Success(17).Ensure(i => i >= 18, guard);
            r.Error.ShouldBe(guard);
        }

        [Fact]
        public void Ensure_propagates_existing_failure_without_evaluating_predicate()
        {
            bool predicateCalled = false;
            var r = Result<int>.Failure(SampleError)
                .Ensure(_ => { predicateCalled = true; return true; }, new ResultError("Other", "ignored"));
            r.Error.ShouldBe(SampleError);
            predicateCalled.ShouldBeFalse();
        }
    }

    public class TryPattern
    {
        [Fact]
        public void Try_wraps_thrown_exception()
        {
            var r = Result.Try(
                () => int.Parse("not a number"),
                ex => new ResultError("Parse.Failed", ex.Message));

            r.IsFailure.ShouldBeTrue();
            r.Error.Code.ShouldBe("Parse.Failed");
        }

        [Fact]
        public void Try_returns_success_when_no_exception()
        {
            var r = Result.Try(() => 42, _ => new ResultError("Ignored", "no"));
            r.Value.ShouldBe(42);
        }

        [Fact]
        public async Task TryAsync_wraps_async_exception()
        {
            var r = await Result.TryAsync(
                () => Task.FromException<int>(new InvalidOperationException("boom")),
                ex => new ResultError("Async.Failed", ex.Message));

            r.IsFailure.ShouldBeTrue();
            r.Error.Code.ShouldBe("Async.Failed");
        }
    }
}
