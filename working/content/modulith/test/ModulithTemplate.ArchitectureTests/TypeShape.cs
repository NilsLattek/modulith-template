using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Reflection helpers for the rules about a type's shape — its setters, fields and collections —
/// which ArchUnitNET cannot express.
/// </summary>
internal static class TypeShape
{
    /// <summary>Collection types a caller can read but not change.</summary>
    private static readonly Type[] ReadOnlyCollections =
    [
        typeof(IEnumerable<>),
        typeof(IReadOnlyCollection<>),
        typeof(IReadOnlyList<>),
        typeof(IReadOnlySet<>),
        typeof(IReadOnlyDictionary<,>),
        typeof(ReadOnlyCollection<>),
        typeof(ReadOnlyDictionary<,>),
    ];

    /// <summary>Whether a property or field of this type lets its reader add, remove or replace elements.</summary>
    /// <remarks>
    /// <c>byte[]</c> is exempt: EF Core maps it as one scalar column, not as a collection of children.
    /// </remarks>
    /// <param name="type">The declared member type.</param>
    /// <returns><see langword="true"/> for <c>List&lt;T&gt;</c>, arrays, <c>ICollection&lt;T&gt;</c> and the like.</returns>
    public static bool IsMutableCollection(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]) || type == typeof(IEnumerable)
            || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return false;
        }

        if (type.Namespace is "System.Collections.Immutable" or "System.Collections.Frozen")
        {
            return false;
        }

        return !(type.IsGenericType && ReadOnlyCollections.Contains(type.GetGenericTypeDefinition()));
    }

    /// <summary>Whether the setter can run only during construction (<c>init</c>).</summary>
    /// <param name="setter">A property's set accessor.</param>
    /// <returns><see langword="true"/> for an <c>init</c> accessor.</returns>
    public static bool IsInitOnly(MethodInfo setter) =>
        setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));

    /// <summary>Everything that lets a constructed instance of <paramref name="type"/> change.</summary>
    /// <remarks>
    /// Compiler-generated backing fields are skipped: their property's setter is reported instead,
    /// under the name the author wrote.
    /// </remarks>
    /// <param name="type">The type to inspect.</param>
    /// <returns>One description per mutable member; empty if the type is immutable.</returns>
    public static IEnumerable<string> MutableMembers(Type type)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var property in type.GetProperties(instance))
        {
            if (property.SetMethod is { } setter && !IsInitOnly(setter))
            {
                yield return $"{type.Name}.{property.Name} has a setter";
            }

            if (property.GetMethod is { IsPublic: true } && IsMutableCollection(property.PropertyType))
            {
                yield return $"{type.Name}.{property.Name} is a mutable {Describe(property.PropertyType)}";
            }
        }

        foreach (var field in type.GetFields(instance))
        {
            if (!field.IsInitOnly && !field.IsDefined(typeof(CompilerGeneratedAttribute)))
            {
                yield return $"{type.Name}.{field.Name} is a field that is not readonly";
            }
        }
    }

    /// <summary>The type and, recursively, its array element and generic arguments.</summary>
    /// <param name="type">A member's declared type.</param>
    /// <returns>Every type the declaration names, so <c>List&lt;Order&gt;</c> yields <c>Order</c>.</returns>
    public static IEnumerable<Type> NamedTypes(Type type)
    {
        yield return type;

        var inner = type.IsArray ? [type.GetElementType()!] : type.GetGenericArguments();
        foreach (var named in inner.SelectMany(NamedTypes))
        {
            yield return named;
        }
    }

    /// <summary>A readable type name: <c>List&lt;OrderLine&gt;</c> rather than <c>List`1</c>.</summary>
    /// <param name="type">The type to describe.</param>
    /// <returns>Its name with generic arguments spelled out.</returns>
    public static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;

    /// <summary>Fails with every violation listed, then the reason the rule exists.</summary>
    /// <param name="violations">What broke the rule, one line each.</param>
    /// <param name="because">Why the rule exists and how to comply.</param>
    public static void AssertNone(IEnumerable<string> violations, string because)
    {
        var found = violations.Distinct(StringComparer.Ordinal).ToArray();

        Assert.True(
            found.Length == 0,
            string.Join(Environment.NewLine, found.Select(violation => "  - " + violation))
                + Environment.NewLine + Environment.NewLine + because);
    }
}
