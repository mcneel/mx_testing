// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Not localized.")]
[assembly: SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Generics is here a way to circumvent prohibition to use GetType() in static methods.",
    Scope = "member", Target = "~M:MxTests.AnyCommand`1.GetTestModels~System.Collections.Generic.IEnumerable{System.String[]}")]
[assembly: SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Names define ordering in NUnit", Scope = "type", Target = "~T:MxTests._OpenRhinoTests")]
