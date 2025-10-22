using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using BitcrackPoolBackend.Data;
using BitcrackPoolBackend.Enums;
using BitcrackPoolBackend.Models;
using BitcrackPoolBackend.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BitcrackPoolBackend.Services
{
    public class RangeService
    {
        private readonly IOptions<PoolOptions> _options;
        private readonly SemaphoreSlim _claimLock = new(1, 1);

        public RangeService(IOptions<PoolOptions> options)
        {
            _options = options;
        }

        public async Task<PuzzleDefinition?> EnsureDefaultPuzzleAsync(PoolDbContext db, CancellationToken cancellationToken)
        {
            if (await db.PuzzleDefinitions.AnyAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var defaults = _options.Value;
            var minPrefix = NormalizeHex(defaults.RangeStartHex);
            var maxPrefix = NormalizeHex(defaults.RangeEndHex);

            if (defaults.SeedPuzzles is not null && defaults.SeedPuzzles.Count > 0)
            {
                foreach (var seed in defaults.SeedPuzzles)
                {
                    var min = NormalizeHex(string.IsNullOrWhiteSpace(seed.MinPrefixHex) ? defaults.RangeStartHex : seed.MinPrefixHex);
                    var max = NormalizeHex(string.IsNullOrWhiteSpace(seed.MaxPrefixHex) ? defaults.RangeEndHex : seed.MaxPrefixHex);
                    var puzzleSeed = new PuzzleDefinition
                    {
                        Id = Guid.NewGuid(),
                        Code = string.IsNullOrWhiteSpace(seed.Code) ? defaults.Puzzle : seed.Code.Trim().ToUpperInvariant(),
                        DisplayName = string.IsNullOrWhiteSpace(seed.DisplayName) ? $"Puzzle {seed.Code}" : seed.DisplayName.Trim(),
                        Enabled = seed.Enabled,
                        Randomized = seed.Randomized,
                        Weight = seed.Weight <= 0 ? 1 : seed.Weight,
                        TargetAddress = seed.TargetAddress?.Trim() ?? string.Empty,
                        MinPrefixHex = min,
                        MaxPrefixHex = max,
                        PrefixLength = seed.PrefixLength > 0 ? seed.PrefixLength : min.Length,
                        ChunkSize = seed.ChunkSize <= 0 ? Math.Max(1, defaults.RangeChunkSize) : seed.ChunkSize,
                        WorkloadStartSuffix = string.IsNullOrWhiteSpace(seed.WorkloadStartSuffix) ? defaults.WorkloadStartSuffix : seed.WorkloadStartSuffix,
                        WorkloadEndSuffix = string.IsNullOrWhiteSpace(seed.WorkloadEndSuffix) ? defaults.WorkloadEndSuffix : seed.WorkloadEndSuffix,
                        Notes = seed.Notes,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    };
                    db.PuzzleDefinitions.Add(puzzleSeed);
                }
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return await db.PuzzleDefinitions.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            }

            var puzzle = new PuzzleDefinition
            {
                Id = Guid.NewGuid(),
                Code = defaults.Puzzle,
                DisplayName = $"Puzzle {defaults.Puzzle}",
                Enabled = true,
                Randomized = true,
                Weight = 1,
                TargetAddress = string.Empty,
                MinPrefixHex = minPrefix,
                MaxPrefixHex = maxPrefix,
                PrefixLength = minPrefix.Length,
                ChunkSize = Math.Max(1, defaults.RangeChunkSize),
                WorkloadStartSuffix = defaults.WorkloadStartSuffix,
                WorkloadEndSuffix = defaults.WorkloadEndSuffix,
                Notes = "Seeded from PoolOptions",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.PuzzleDefinitions.Add(puzzle);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return puzzle;
        }

        public async Task<RangeAssignment?> AssignNextRangeAsync(PoolDbContext db, Client client, CancellationToken cancellationToken = default)
        {
            await _claimLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var puzzles = await db.PuzzleDefinitions
                    .Where(p => p.Enabled)
                    .OrderBy(p => p.DisplayName)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

            if (puzzles.Count == 0)
            {
                var seeded = await EnsureDefaultPuzzleAsync(db, cancellationToken).ConfigureAwait(false);
                if (seeded is not null)
                {
                    puzzles = await db.PuzzleDefinitions
                        .Where(p => p.Enabled)
                        .OrderBy(p => p.DisplayName)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (client.CurrentRangeId.HasValue)
                {
                    var existing = await db.RangeAssignments
                        .FirstOrDefaultAsync(r => r.Id == client.CurrentRangeId, cancellationToken)
                        .ConfigureAwait(false);
                    if (existing is not null && existing.Status == RangeStatus.Assigned)
                        return existing;
                }

                var puzzle = SelectPuzzle(puzzles);
                if (puzzle is null)
                    return null;

                var range = await AllocateRangeAsync(db, client, puzzle, cancellationToken).ConfigureAwait(false);
                return range;
            }
            finally
            {
                _claimLock.Release();
            }
        }

        public async Task<long> GetTotalAssignmentSlotsAsync(PoolDbContext db, Guid? puzzleId, CancellationToken cancellationToken)
        {
            IQueryable<PuzzleDefinition> query = db.PuzzleDefinitions.AsQueryable();
            if (puzzleId.HasValue)
                query = query.Where(p => p.Id == puzzleId.Value);

            var puzzles = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
            if (puzzles.Count == 0)
                return 0;

            BigInteger total = BigInteger.Zero;
            foreach (var puzzle in puzzles)
            {
                var chunk = BigInteger.Max(BigInteger.One, new BigInteger(puzzle.ChunkSize));
                var min = ParseHex(puzzle.MinPrefixHex);
                var max = ParseHex(puzzle.MaxPrefixHex);
                var totalPrefixes = max - min + BigInteger.One;
                var chunks = BigInteger.Divide(totalPrefixes + chunk - BigInteger.One, chunk);
                if (chunks > long.MaxValue)
                    return long.MaxValue;
                total += chunks;
                if (total > long.MaxValue)
                    return long.MaxValue;
            }

            return (long)total;
        }

        private async Task<RangeAssignment?> AllocateRangeAsync(PoolDbContext db, Client client, PuzzleDefinition puzzle, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var attempts = 0;
            const int maxAttempts = 200;

            while (attempts < maxAttempts)
            {
                attempts++;
                var candidate = puzzle.Randomized
                    ? GetRandomChunk(puzzle)
                    : await GetSequentialChunkAsync(db, puzzle, cancellationToken).ConfigureAwait(false);

                if (candidate is null)
                    break;

                var range = await TryPersistRangeAsync(db, client, puzzle, candidate.Value, now, cancellationToken).ConfigureAwait(false);
                if (range is not null)
                    return range;
            }

            if (puzzle.Randomized)
            {
                var sequentialCandidate = await GetSequentialChunkAsync(db, puzzle, cancellationToken).ConfigureAwait(false);
                if (sequentialCandidate is not null)
                {
                    var sequentialRange = await TryPersistRangeAsync(db, client, puzzle, sequentialCandidate.Value, DateTime.UtcNow, cancellationToken).ConfigureAwait(false);
                    if (sequentialRange is not null)
                        return sequentialRange;
                }
            }

            return null;
        }

        private async Task<RangeAssignment?> TryPersistRangeAsync(
            PoolDbContext db,
            Client client,
            PuzzleDefinition puzzle,
            (string PrefixStart, string PrefixEnd, int ChunkSize) candidate,
            DateTime timestampUtc,
            CancellationToken cancellationToken)
        {
            var (prefixStart, prefixEnd, actualChunk) = candidate;

            var exists = await db.RangeAssignments
                .AnyAsync(r => r.PuzzleId == puzzle.Id && r.PrefixStart == prefixStart, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                return null;

            var range = new RangeAssignment
            {
                Id = Guid.NewGuid(),
                Puzzle = puzzle.Code,
                PuzzleId = puzzle.Id,
                PuzzleDefinition = puzzle,
                PrefixStart = prefixStart,
                PrefixEnd = prefixEnd,
                RangeStartHex = prefixStart + puzzle.WorkloadStartSuffix,
                RangeEndHex = prefixEnd + puzzle.WorkloadEndSuffix,
                ChunkSize = actualChunk,
                Status = RangeStatus.Assigned,
                AssignedToClientId = client.Id,
                AssignedAtUtc = timestampUtc,
                LastUpdateUtc = timestampUtc,
                ProgressPercent = 0
            };

            db.RangeAssignments.Add(range);

            client.Puzzle = puzzle.Code;
            client.CurrentRangeId = range.Id;
            client.Status = ClientStatus.Scanning;
            client.LastSeenUtc = timestampUtc;
            puzzle.UpdatedAtUtc = timestampUtc;

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return range;
            }
            catch (DbUpdateException)
            {
                db.Entry(range).State = EntityState.Detached;
                client.CurrentRangeId = null;
                client.Status = ClientStatus.Idle;
                client.LastSeenUtc = timestampUtc;
                await db.Entry(client).ReloadAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
        }

        private async Task<(string PrefixStart, string PrefixEnd, int ChunkSize)?> GetSequentialChunkAsync(PoolDbContext db, PuzzleDefinition puzzle, CancellationToken cancellationToken)
        {
            var state = await db.PoolStates.SingleOrDefaultAsync(s => s.Puzzle == puzzle.Code, cancellationToken).ConfigureAwait(false);
            if (state is null)
            {
                state = new PoolState
                {
                    Id = Guid.NewGuid(),
                    Puzzle = puzzle.Code,
                    NextPrefixHex = NormalizeHex(puzzle.MinPrefixHex),
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.PoolStates.Add(state);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            var prefixLength = puzzle.PrefixLength > 0 ? puzzle.PrefixLength : puzzle.MinPrefixHex.Length;
            var nextValue = ParseHex(state.NextPrefixHex);
            var maxValue = ParseHex(puzzle.MaxPrefixHex);
            if (nextValue > maxValue)
                return null;

            var chunk = BigInteger.Max(BigInteger.One, new BigInteger(puzzle.ChunkSize));
            var endValue = BigInteger.Min(maxValue, nextValue + chunk - BigInteger.One);

            var prefixStart = FormatHex(nextValue, prefixLength);
            var prefixEnd = FormatHex(endValue, prefixLength);
            var actualChunk = (int)(endValue - nextValue + BigInteger.One);

            var newNext = endValue + BigInteger.One;
            state.NextPrefixHex = FormatHex(newNext, prefixLength);
            state.UpdatedAtUtc = DateTime.UtcNow;

            return (prefixStart, prefixEnd, actualChunk);
        }

        private (string PrefixStart, string PrefixEnd, int ChunkSize)? GetRandomChunk(PuzzleDefinition puzzle)
        {
            var prefixLength = puzzle.PrefixLength > 0 ? puzzle.PrefixLength : puzzle.MinPrefixHex.Length;
            var minValue = ParseHex(puzzle.MinPrefixHex);
            var maxValue = ParseHex(puzzle.MaxPrefixHex);
            var chunk = BigInteger.Max(BigInteger.One, new BigInteger(puzzle.ChunkSize));
            var totalPrefixes = maxValue - minValue + BigInteger.One;
            if (totalPrefixes <= BigInteger.Zero)
                return null;

            var totalChunks = BigInteger.Divide(totalPrefixes + chunk - BigInteger.One, chunk);
            if (totalChunks <= BigInteger.Zero)
                return null;

            var randomIndex = GetRandomBigInteger(totalChunks);
            var startValue = minValue + randomIndex * chunk;
            if (startValue > maxValue)
                startValue = maxValue;
            var endValue = BigInteger.Min(maxValue, startValue + chunk - BigInteger.One);

            var prefixStart = FormatHex(startValue, prefixLength);
            var prefixEnd = FormatHex(endValue, prefixLength);
            var actualChunk = (int)(endValue - startValue + BigInteger.One);

            return (prefixStart, prefixEnd, actualChunk);
        }

        private static PuzzleDefinition? SelectPuzzle(IReadOnlyList<PuzzleDefinition> puzzles)
        {
            if (puzzles.Count == 0)
                return null;

            if (puzzles.Count == 1)
                return puzzles[0];

            var totalWeight = puzzles.Sum(p => Math.Max(0.01, p.Weight));
            var roll = Random.Shared.NextDouble() * totalWeight;
            double cumulative = 0;
            foreach (var puzzle in puzzles)
            {
                cumulative += Math.Max(0.01, puzzle.Weight);
                if (roll <= cumulative)
                    return puzzle;
            }

            return puzzles[^1];
        }

        private static string NormalizeHex(string hex)
        {
            return hex.Trim().Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        }

        private static BigInteger ParseHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return BigInteger.Zero;

            return BigInteger.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private static string FormatHex(BigInteger value, int length)
        {
            if (value < BigInteger.Zero)
                value = BigInteger.Zero;

            var hex = value.ToString("X", CultureInfo.InvariantCulture);
            if (hex.Length < length)
                hex = hex.PadLeft(length, '0');
            else if (hex.Length > length)
                hex = hex[^length..];
            return hex;
        }

        private static BigInteger GetRandomBigInteger(BigInteger maxExclusive)
        {
            if (maxExclusive <= BigInteger.One)
                return BigInteger.Zero;

            var bytes = maxExclusive.ToByteArray(isUnsigned: true, isBigEndian: true);
            if (bytes.Length == 0)
                bytes = new byte[1];

            BigInteger result;
            do
            {
                RandomNumberGenerator.Fill(bytes);
                result = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            } while (result >= maxExclusive);

            return result;
        }
    }
}
