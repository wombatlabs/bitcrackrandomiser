using BitcrackPoolBackend.Models;
using Microsoft.EntityFrameworkCore;

namespace BitcrackPoolBackend.Data
{
    public class PoolDbContext : DbContext
    {
        public PoolDbContext(DbContextOptions<PoolDbContext> options) : base(options)
        {
        }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<RangeAssignment> RangeAssignments => Set<RangeAssignment>();
        public DbSet<PoolState> PoolStates => Set<PoolState>();
        public DbSet<PuzzleDefinition> PuzzleDefinitions => Set<PuzzleDefinition>();
        public DbSet<KeyFindEvent> KeyFindEvents => Set<KeyFindEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasIndex(e => e.ApiKey).IsUnique();
                entity.Property(e => e.User).HasMaxLength(64);
                entity.Property(e => e.WorkerName).HasMaxLength(64);
                entity.Property(e => e.Puzzle).HasMaxLength(32);
                entity.Property(e => e.GpuInfo).HasMaxLength(256);
                entity.Property(e => e.ClientVersion).HasMaxLength(32);
                entity.Property(e => e.ApiKey).HasMaxLength(64);

                entity.HasMany(e => e.RangeAssignments)
                      .WithOne(r => r.AssignedToClient)
                      .HasForeignKey(r => r.AssignedToClientId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.CurrentRange)
                      .WithMany()
                      .HasForeignKey(e => e.CurrentRangeId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<RangeAssignment>(entity =>
            {
                entity.HasIndex(e => new { e.Puzzle, e.PrefixStart }).IsUnique();
                entity.Property(e => e.Puzzle).HasMaxLength(32);
                entity.Property(e => e.PrefixStart).HasMaxLength(64);
                entity.Property(e => e.PrefixEnd).HasMaxLength(64);
                entity.Property(e => e.RangeStartHex).HasMaxLength(128);
                entity.Property(e => e.RangeEndHex).HasMaxLength(128);

                entity.HasOne(e => e.PuzzleDefinition)
                      .WithMany(p => p.RangeAssignments)
                      .HasForeignKey(e => e.PuzzleId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PoolState>(entity =>
            {
                entity.HasIndex(e => e.Puzzle).IsUnique();
                entity.Property(e => e.Puzzle).HasMaxLength(16);
                entity.Property(e => e.NextPrefixHex).HasMaxLength(32);
            });

            modelBuilder.Entity<KeyFindEvent>(entity =>
            {
                entity.Property(e => e.Puzzle).HasMaxLength(32);
                entity.Property(e => e.WorkerName).HasMaxLength(64);
                entity.Property(e => e.User).HasMaxLength(64);
                entity.Property(e => e.PrivateKey).HasMaxLength(128);
                entity.HasIndex(e => new { e.Puzzle, e.ReportedAtUtc });
            });

            modelBuilder.Entity<PuzzleDefinition>(entity =>
            {
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(32);
                entity.Property(e => e.DisplayName).HasMaxLength(128);
                entity.Property(e => e.TargetAddress).HasMaxLength(128);
                entity.Property(e => e.MinPrefixHex).HasMaxLength(64);
                entity.Property(e => e.MaxPrefixHex).HasMaxLength(64);
                entity.Property(e => e.WorkloadStartSuffix).HasMaxLength(128);
                entity.Property(e => e.WorkloadEndSuffix).HasMaxLength(128);
            });
        }
    }
}
