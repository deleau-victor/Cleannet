using System.Diagnostics;
using Domain.Abstractions;

[assembly: DebuggerDisplay("{ToString(),nq}", Target = typeof(Id))]
[assembly: DebuggerDisplay("{ToString(),nq}", Target = typeof(Id<>))]
