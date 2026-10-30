using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;

using ModulithTemplate.SharedKernel.Domain.Entities;
using ModulithTemplate.SharedKernel.Domain.Events;

namespace ModulithTemplate.ArchitectureTests;

/// <summary>
/// Holds entities, value objects and domain events to the shapes CLAUDE.md describes, so state can
/// change only through code that enforces the invariants.
/// </summary>
/// <remarks>
/// An entity is a class in a feature's <c>Domain.Entities</c> namespace, or one deriving
/// <see cref="AggregateRoot"/>; a value object is a type in <c>Domain.ValueObjects</c>. Matching
/// nothing passes: a freshly scaffolded feature has no entities yet.
/// </remarks>
public class DomainModelTests
{
    private const BindingFlags DeclaredInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

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

    [Fact]
    public void Entities_change_state_only_through_their_own_methods()
    {
        // Arrange
        var entities = FindEntityTypes();

        // Act
        var violations =
            from entity in entities
            from property in entity.GetProperties(DeclaredInstance)
            where property.SetMethod is { IsPrivate: false }
            select $"{entity.Name}.{property.Name} has a non-private setter";

        // Assert
        AssertNone(
            violations,
            "an entity changes state only through named methods (AddLine, Confirm) that enforce its invariants. "
                + "Any setter but a private one lets a handler or a form bypass them: make it `private set` and "
                + "add a method that says what happened.");
    }

    [Fact]
    public void Entities_expose_collections_read_only()
    {
        // Arrange
        var entities = FindEntityTypes();

        // Act
        var violations =
            from entity in entities
            from property in entity.GetProperties(DeclaredInstance)
            where property.GetMethod is { IsPublic: true } && IsMutableCollection(property.PropertyType)
            select $"{entity.Name}.{property.Name} is a {FormatReadableTypeName(property.PropertyType)}";

        // Assert
        AssertNone(
            violations,
            "a mutable collection lets callers add or remove children without the entity's invariants running. "
                + "Expose IReadOnlyList<T> over a private backing List<T>, changed only by the entity's methods.");
    }

    [Fact]
    public void Entities_are_created_only_through_a_factory_or_domain_service()
    {
        // Arrange
        var entities = FindEntityTypes();

        // Act
        var violations =
            from entity in entities
            where !entity.IsAbstract && entity.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length > 0
            select $"{entity.Name} has a public constructor";

        // Assert
        AssertNone(
            violations,
            "Application obtains an entity through a public static factory method or a domain service, never "
                + "`new`. Make the constructor internal.");
    }

    [Fact]
    public void Entities_have_a_private_parameterless_constructor_for_EF()
    {
        // Arrange
        var entities = FindEntityTypes();

        // Act
        var violations =
            from entity in entities
            where !entity.IsAbstract
            let constructor = entity.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes)
            where constructor is not { IsPrivate: true }
            select constructor is null
                ? $"{entity.Name} has no parameterless constructor"
                : $"{entity.Name}'s parameterless constructor is not private";

        // Assert
        AssertNone(
            violations,
            "EF Core materialises an entity through its parameterless constructor, which bypasses the invariants, "
                + "so it must exist for EF and be private so that nothing else can call it.");
    }

    /// <remarks>
    /// A child may point back at the root that holds it — that is one aggregate. Only roots deriving
    /// <see cref="AggregateRoot"/> are recognised, since deriving it is opt-in.
    /// </remarks>
    [Fact]
    public void Entities_reference_other_aggregates_only_by_id()
    {
        // Arrange
        var entities = FindEntityTypes();
        var roots = entities.Where(IsAggregateRoot).ToHashSet();

        // Act
        var violations =
            from entity in entities
            from member in FindStateMembers(entity)
            from referenced in EnumerateTypeAndItsTypeArguments(member.Type).Where(roots.Contains).Distinct()
            where IsAggregateRoot(entity) || !FindEntitiesInsideAggregate(referenced, entities).Contains(entity)
            select $"{entity.Name}.{member.Name} holds a {referenced.Name}";

        // Assert
        AssertNone(
            violations,
            "each aggregate is loaded, changed and saved as a unit, and a navigation into another one lets a single "
                + "save change both. Hold the other aggregate's id instead, and have the handler load it.");
    }

    [Fact]
    public void Value_objects_are_immutable()
    {
        // Arrange
        var valueObjects = FindValueObjectTypes();

        // Act
        var violations = valueObjects.SelectMany(FindMutableMembers);

        // Assert
        AssertNone(
            violations,
            "a value object is defined only by its values, so it is replaced rather than changed. Declare it as a "
                + "positional record, with readonly collections.");
    }

    [Fact]
    public void Value_objects_compare_by_value()
    {
        // Arrange
        var valueObjects = FindValueObjectTypes();

        // Act
        var violations =
            from valueObject in valueObjects
            where valueObject.GetMethod(
                nameof(Equals),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                [typeof(object)]) is null
            select $"{valueObject.Name} does not override Equals";

        // Assert
        AssertNone(
            violations,
            "two value objects holding the same values are the same value; without value equality, comparing "
                + "them compares references. Declare it as a record.");
    }

    [Fact]
    public void Domain_events_are_immutable()
    {
        // Arrange
        var domainEvents = FindDomainEventTypes();

        // Act
        var violations = domainEvents.SelectMany(FindMutableMembers);

        // Assert
        AssertNone(
            violations,
            "a domain event records something that already happened, and every handler receives the same instance, "
                + "so one handler changing it would change what the others see. Declare it as a positional record.");
    }

    /// <summary>The top-level types of every feature's Domain layer.</summary>
    private static Type[] FindDomainTypes()
    {
        Assert.True(
            SolutionAssemblies.HasFeatureLayer("Domain"),
            "No feature Domain assembly was found; the domain model rules would pass vacuously.");

        return
        [
            .. SolutionAssemblies.FeatureAssemblies
                .Where(assembly => assembly.GetName().Name!.EndsWith(".Domain", StringComparison.Ordinal))
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => !type.IsNested && !type.IsInterface && !type.IsEnum
                    && !(type.IsAbstract && type.IsSealed)),
        ];
    }

    private static Type[] FindDomainEventTypes() =>
    [
        .. SolutionAssemblies.FeatureTypes.Where(type => type is { IsInterface: false, IsAbstract: false }
            && typeof(IDomainEvent).IsAssignableFrom(type)),
    ];

    private static Type[] FindEntityTypes() =>
        [.. FindDomainTypes().Where(type => type.IsClass && (IsInDomainNamespace(type, "Entities") || IsAggregateRoot(type)))];

    private static Type[] FindValueObjectTypes() =>
        [.. FindDomainTypes().Where(type => IsInDomainNamespace(type, "ValueObjects"))];

    private static bool IsAggregateRoot(Type type) => typeof(AggregateRoot).IsAssignableFrom(type);

    /// <summary>Whether the type sits in the feature Domain namespace ending in <paramref name="folder"/>, or below it.</summary>
    private static bool IsInDomainNamespace(Type type, string folder)
    {
        var segment = ".Domain." + folder;
        var ns = type.Namespace ?? string.Empty;

        return ns.EndsWith(segment, StringComparison.Ordinal)
            || ns.Contains(segment + ".", StringComparison.Ordinal);
    }

    /// <summary>The members holding an entity's state: its properties, and fields the author declared.</summary>
    private static IEnumerable<(string Name, Type Type)> FindStateMembers(Type type)
    {
        foreach (var property in type.GetProperties(DeclaredInstance))
        {
            yield return (property.Name, property.PropertyType);
        }

        // Compiler-generated backing fields are named <Property>k__BackingField.
        foreach (var field in type.GetFields(DeclaredInstance).Where(field => !field.Name.StartsWith('<')))
        {
            yield return (field.Name, field.FieldType);
        }
    }

    /// <summary>The non-root entities reachable from <paramref name="root"/> through its members: its aggregate.</summary>
    private static HashSet<Type> FindEntitiesInsideAggregate(Type root, Type[] entities)
    {
        var children = entities.Where(entity => !IsAggregateRoot(entity)).ToHashSet();
        var held = new HashSet<Type>();
        var pending = new Queue<Type>([root]);

        while (pending.TryDequeue(out var current))
        {
            foreach (var child in FindStateMembers(current).SelectMany(member => EnumerateTypeAndItsTypeArguments(member.Type)))
            {
                if (children.Contains(child) && held.Add(child))
                {
                    pending.Enqueue(child);
                }
            }
        }

        return held;
    }

    /// <summary>Whether a member of this type lets its reader add, remove or replace elements.</summary>
    /// <remarks><c>byte[]</c> is exempt: EF Core maps it as one scalar column, not a collection of children.</remarks>
    private static bool IsMutableCollection(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]) || type == typeof(IEnumerable)
            || !typeof(IEnumerable).IsAssignableFrom(type)
            || type.Namespace is "System.Collections.Immutable" or "System.Collections.Frozen")
        {
            return false;
        }

        return !(type.IsGenericType && ReadOnlyCollections.Contains(type.GetGenericTypeDefinition()));
    }

    /// <summary>Everything that lets a constructed instance of <paramref name="type"/> change.</summary>
    /// <remarks>Backing fields are skipped; their property's setter is reported under the name the author wrote.</remarks>
    private static IEnumerable<string> FindMutableMembers(Type type)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var property in type.GetProperties(instance))
        {
            if (property.SetMethod is { } setter
                && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            {
                yield return $"{type.Name}.{property.Name} has a setter";
            }

            if (property.GetMethod is { IsPublic: true } && IsMutableCollection(property.PropertyType))
            {
                yield return $"{type.Name}.{property.Name} is a mutable {FormatReadableTypeName(property.PropertyType)}";
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
    private static IEnumerable<Type> EnumerateTypeAndItsTypeArguments(Type type)
    {
        yield return type;

        var inner = type.IsArray ? [type.GetElementType()!] : type.GetGenericArguments();
        foreach (var named in inner.SelectMany(EnumerateTypeAndItsTypeArguments))
        {
            yield return named;
        }
    }

    /// <summary>A readable type name: <c>List&lt;OrderLine&gt;</c> rather than <c>List`1</c>.</summary>
    private static string FormatReadableTypeName(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(", ", type.GetGenericArguments().Select(FormatReadableTypeName))}>"
            : type.Name;

    /// <summary>Fails with every violation listed, then the reason the rule exists.</summary>
    private static void AssertNone(IEnumerable<string> violations, string because)
    {
        var found = violations.Distinct(StringComparer.Ordinal).ToArray();

        Assert.True(
            found.Length == 0,
            string.Join(Environment.NewLine, found.Select(violation => "  - " + violation))
                + Environment.NewLine + Environment.NewLine + because);
    }
}
