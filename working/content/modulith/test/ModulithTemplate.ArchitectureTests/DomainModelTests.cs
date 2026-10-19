using System.Reflection;

using ModulithTemplate.SharedKernel.Domain.Entities;
using ModulithTemplate.SharedKernel.Domain.Events;

using static ModulithTemplate.ArchitectureTests.TypeShape;

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

    [Fact]
    public void Entities_change_state_only_through_their_own_methods() =>
        AssertNone(
            from entity in Entities()
            from property in entity.GetProperties(DeclaredInstance)
            where property.SetMethod is { IsPrivate: false }
            select $"{entity.Name}.{property.Name} has a non-private setter",
            "an entity changes state only through named methods (AddLine, Confirm) that enforce its invariants. "
                + "Any setter but a private one lets a handler or a form bypass them: make it `private set` and "
                + "add a method that says what happened.");

    [Fact]
    public void Entities_expose_collections_read_only() =>
        AssertNone(
            from entity in Entities()
            from property in entity.GetProperties(DeclaredInstance)
            where property.GetMethod is { IsPublic: true } && IsMutableCollection(property.PropertyType)
            select $"{entity.Name}.{property.Name} is a {Describe(property.PropertyType)}",
            "a mutable collection lets callers add or remove children without the entity's invariants running. "
                + "Expose IReadOnlyList<T> over a private backing List<T>, changed only by the entity's methods.");

    [Fact]
    public void Entities_are_created_only_through_a_factory_or_domain_service() =>
        AssertNone(
            from entity in Entities()
            where !entity.IsAbstract && entity.GetConstructors(BindingFlags.Instance | BindingFlags.Public).Length > 0
            select $"{entity.Name} has a public constructor",
            "Application obtains an entity through a public static factory method or a domain service, never "
                + "`new`. Make the constructor internal.");

    [Fact]
    public void Entities_have_a_private_parameterless_constructor_for_EF() =>
        AssertNone(
            from entity in Entities()
            where !entity.IsAbstract
            let constructor = entity.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes)
            where constructor is not { IsPrivate: true }
            select constructor is null
                ? $"{entity.Name} has no parameterless constructor"
                : $"{entity.Name}'s parameterless constructor is not private",
            "EF Core materialises an entity through its parameterless constructor, which bypasses the invariants, "
                + "so it must exist for EF and be private so that nothing else can call it.");

    /// <remarks>
    /// A child may point back at the root that holds it — that is one aggregate. Only roots deriving
    /// <see cref="AggregateRoot"/> are recognised, since deriving it is opt-in.
    /// </remarks>
    [Fact]
    public void Entities_reference_other_aggregates_only_by_id()
    {
        var entities = Entities();
        var roots = entities.Where(IsAggregateRoot).ToHashSet();

        AssertNone(
            from entity in entities
            from member in StateMembers(entity)
            from referenced in NamedTypes(member.Type).Where(roots.Contains).Distinct()
            where IsAggregateRoot(entity) || !HeldBy(referenced, entities).Contains(entity)
            select $"{entity.Name}.{member.Name} holds a {referenced.Name}",
            "each aggregate is loaded, changed and saved as a unit, and a navigation into another one lets a single "
                + "save change both. Hold the other aggregate's id instead, and have the handler load it.");
    }

    [Fact]
    public void Value_objects_are_immutable() =>
        AssertNone(
            ValueObjects().SelectMany(MutableMembers),
            "a value object is defined only by its values, so it is replaced rather than changed. Declare it as a "
                + "positional record, with readonly collections.");

    [Fact]
    public void Value_objects_compare_by_value() =>
        AssertNone(
            from valueObject in ValueObjects()
            where valueObject.GetMethod(
                nameof(Equals),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                [typeof(object)]) is null
            select $"{valueObject.Name} does not override Equals",
            "two value objects holding the same values are the same value; without value equality, comparing "
                + "them compares references. Declare it as a record.");

    [Fact]
    public void Domain_events_are_immutable() =>
        AssertNone(
            SolutionAssemblies.FeatureAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type is { IsInterface: false, IsAbstract: false }
                    && typeof(IDomainEvent).IsAssignableFrom(type))
                .SelectMany(MutableMembers),
            "a domain event records something that already happened, and every handler receives the same instance, "
                + "so one handler changing it would change what the others see. Declare it as a positional record.");

    /// <summary>The top-level types of every feature's Domain layer.</summary>
    private static Type[] DomainTypes()
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

    private static Type[] Entities() =>
        [.. DomainTypes().Where(type => type.IsClass && (InNamespace(type, "Entities") || IsAggregateRoot(type)))];

    private static Type[] ValueObjects() =>
        [.. DomainTypes().Where(type => InNamespace(type, "ValueObjects"))];

    private static bool IsAggregateRoot(Type type) => typeof(AggregateRoot).IsAssignableFrom(type);

    /// <summary>Whether the type sits in the feature Domain namespace ending in <paramref name="folder"/>, or below it.</summary>
    private static bool InNamespace(Type type, string folder)
    {
        var segment = ".Domain." + folder;
        var ns = type.Namespace ?? string.Empty;

        return ns.EndsWith(segment, StringComparison.Ordinal)
            || ns.Contains(segment + ".", StringComparison.Ordinal);
    }

    /// <summary>The members holding an entity's state: its properties, and fields the author declared.</summary>
    private static IEnumerable<(string Name, Type Type)> StateMembers(Type type) =>
        type.GetProperties(DeclaredInstance).Select(property => (property.Name, property.PropertyType))
            .Concat(type.GetFields(DeclaredInstance)
                .Where(field => !field.Name.StartsWith('<'))
                .Select(field => (field.Name, field.FieldType)));

    /// <summary>The non-root entities reachable from <paramref name="root"/> through its members: its aggregate.</summary>
    private static HashSet<Type> HeldBy(Type root, Type[] entities)
    {
        var children = entities.Where(entity => !IsAggregateRoot(entity)).ToHashSet();
        var held = new HashSet<Type>();
        var pending = new Queue<Type>([root]);

        while (pending.TryDequeue(out var current))
        {
            foreach (var child in StateMembers(current).SelectMany(member => NamedTypes(member.Type)))
            {
                if (children.Contains(child) && held.Add(child))
                {
                    pending.Enqueue(child);
                }
            }
        }

        return held;
    }
}
