using Microsoft.EntityFrameworkCore;

using ModulithTemplate.SharedKernel.Domain.Entities;
using ModulithTemplate.SharedKernel.Domain.Exceptions;
using ModulithTemplate.SharedKernel.Infrastructure;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// Opt-in optimistic concurrency against real Postgres: an <see cref="IVersioned"/> row refuses a change
/// made from a stale copy, and any other row stays last-write-wins.
/// </summary>
/// <remarks>
/// A context of its own, because the shipped sample has no versioned entity: versioning is optional, so
/// the placeholder does not carry it. The check is <c>xmin</c>'s alone, which only a real database shows.
/// </remarks>
public sealed class OptimisticConcurrencyTests : IDisposable
{
    private readonly TestDatabase _database = TestDatabase.Create();

    public OptimisticConcurrencyTests()
    {
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task A_change_carrying_the_version_last_read_is_saved_and_moves_the_version()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (id, version) = await AddAsync(new VersionedNote("draft"), cancellationToken);

        // Act
        var saved = await RenameAsync(id, version, "final", cancellationToken);

        // Assert
        Assert.Equal("final", saved.Text);
        Assert.NotEqual(version, saved.Version);
    }

    [Fact]
    public async Task A_change_from_a_stale_copy_is_refused_and_keeps_the_newer_change()
    {
        // Arrange: two tabs read the same version; the first one's change lands.
        var cancellationToken = TestContext.Current.CancellationToken;
        var (id, stale) = await AddAsync(new VersionedNote("draft"), cancellationToken);
        await RenameAsync(id, stale, "first", cancellationToken);

        // Act
        var act = () => RenameAsync(id, stale, "second", cancellationToken);

        // Assert
        await Assert.ThrowsAsync<ConcurrencyException>(act);
        Assert.Equal("first", (await ReadAsync<VersionedNote>(id, cancellationToken)).Text);
    }

    [Fact]
    public async Task A_removal_from_a_stale_copy_is_refused()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var (id, stale) = await AddAsync(new VersionedNote("draft"), cancellationToken);
        await RenameAsync(id, stale, "first", cancellationToken);

        // Act
        var act = async () =>
        {
            await using var context = NewContext();
            var repository = new NotesRepository<VersionedNote>(context);
            var note = await repository.GetByIdAsync(id, cancellationToken);

            // Before DeleteAsync, which saves at once: an expectation set after it checks nothing.
            repository.ExpectVersion(note!, stale);
            await repository.DeleteAsync(note!, cancellationToken);
        };

        // Assert
        await Assert.ThrowsAsync<ConcurrencyException>(act);
    }

    [Fact]
    public async Task An_entity_that_does_not_opt_in_is_last_write_wins()
    {
        // Arrange: two copies read before either saves.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var adding = NewContext();
        var note = new PlainNote("draft");
        adding.Add(note);
        await adding.SaveChangesAsync(cancellationToken);

        await using var first = NewContext();
        await using var second = NewContext();
        var firstCopy = await first.Set<PlainNote>().SingleAsync(cancellationToken);
        var secondCopy = await second.Set<PlainNote>().SingleAsync(cancellationToken);

        // Act
        firstCopy.Rename("first");
        await first.SaveChangesAsync(cancellationToken);
        secondCopy.Rename("second");
        await second.SaveChangesAsync(cancellationToken);

        // Assert
        Assert.Equal("second", (await ReadAsync<PlainNote>(note.Id, cancellationToken)).Text);
    }

    private NotesContext NewContext() =>
        new(new DbContextOptionsBuilder<NotesContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    private async Task<(Guid Id, uint Version)> AddAsync(VersionedNote note, CancellationToken cancellationToken)
    {
        await using var context = NewContext();
        var repository = new NotesRepository<VersionedNote>(context);
        await repository.AddAsync(note, cancellationToken);
        Assert.NotEqual(0u, note.Version);
        return (note.Id, note.Version);
    }

    /// <summary>What a handler does: load, change, hand over the caller's version, save.</summary>
    private async Task<VersionedNote> RenameAsync(Guid id, uint version, string text, CancellationToken cancellationToken)
    {
        await using var context = NewContext();
        var repository = new NotesRepository<VersionedNote>(context);
        var note = await repository.GetByIdAsync(id, cancellationToken);
        note!.Rename(text);
        repository.ExpectVersion(note, version);
        await repository.SaveChangesAsync(cancellationToken);
        return note;
    }

    private async Task<T> ReadAsync<T>(Guid id, CancellationToken cancellationToken)
        where T : class
    {
        await using var context = NewContext();
        return (await context.Set<T>().FindAsync([id], cancellationToken))!;
    }

    public sealed class VersionedNote : IVersioned
    {
        public VersionedNote(string text) => Text = text;

        public Guid Id { get; private set; } = Guid.CreateVersion7();

        public string Text { get; private set; }

        public uint Version { get; }

        public void Rename(string text) => Text = text;
    }

    public sealed class PlainNote
    {
        public PlainNote(string text) => Text = text;

        public Guid Id { get; private set; } = Guid.CreateVersion7();

        public string Text { get; private set; }

        public void Rename(string text) => Text = text;
    }

    public sealed class NotesContext(DbContextOptions<NotesContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.HasDefaultSchema("notes");
            modelBuilder.Entity<VersionedNote>();
            modelBuilder.Entity<PlainNote>();
            modelBuilder.ApplySharedModel();
        }
    }

    private sealed class NotesRepository<T>(NotesContext dbContext) : FeatureRepository<T>(dbContext)
        where T : class;
}
