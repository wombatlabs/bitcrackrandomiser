using BitcrackPoolBackend.Data;
using BitcrackPoolBackend.Dtos;
using BitcrackPoolBackend.Enums;
using BitcrackPoolBackend.Models;
using BitcrackPoolBackend.Options;
using BitcrackPoolBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using System.Numerics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PoolOptions>(builder.Configuration.GetSection("PoolOptions"));

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=pool.db";
builder.Services.AddDbContext<PoolDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<RangeService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PoolDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors();

var frontendPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "Frontend"));
if (Directory.Exists(frontendPath))
{
    var fileProvider = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider,
        RequestPath = ""
    });
}

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/api/clients/register", async (
    RegisterClientRequest request,
    PoolDbContext db,
    RangeService rangeService,
    IOptions<PoolOptions> poolOptions,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.User))
    {
        return Results.BadRequest("User is required.");
    }

    var options = poolOptions.Value;
    var applicationType = ParseApplicationType(request.ApplicationType);

    var client = new Client
    {
        Id = Guid.NewGuid(),
        User = request.User.Trim(),
        WorkerName = string.IsNullOrWhiteSpace(request.WorkerName)
            ? null
            : request.WorkerName.Trim(),
        Puzzle = string.IsNullOrWhiteSpace(request.Puzzle)
            ? options.Puzzle
            : request.Puzzle.Trim(),
        ApplicationType = applicationType,
        CardsConnected = Math.Max(0, request.CardsConnected),
        GpuInfo = string.IsNullOrWhiteSpace(request.GpuInfo) ? null : request.GpuInfo.Trim(),
        ClientVersion = string.IsNullOrWhiteSpace(request.ClientVersion) ? null : request.ClientVersion.Trim(),
        ApiKey = Guid.NewGuid().ToString("N"),
        Status = ClientStatus.Idle,
        RegisteredAt = DateTime.UtcNow,
        LastSeenUtc = DateTime.UtcNow
    };

    db.Clients.Add(client);
    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    var assignedRange = await rangeService.AssignNextRangeAsync(db, client, cancellationToken).ConfigureAwait(false);

    if (assignedRange is not null)
        await db.Entry(assignedRange).Reference(r => r.PuzzleDefinition).LoadAsync(cancellationToken).ConfigureAwait(false);

    var response = new RegisterClientResponse
    {
        ClientId = client.Id,
        ClientToken = client.ApiKey,
        AssignedRange = assignedRange is null ? null : ToDescriptor(assignedRange)
    };

    return Results.Ok(response);
});

app.MapPost("/api/ranges/claim", async (
    RangeClaimRequest request,
    HttpRequest httpRequest,
    PoolDbContext db,
    RangeService rangeService,
    CancellationToken cancellationToken) =>
{
    var token = httpRequest.Headers["X-Client-Token"].ToString();
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var client = await AuthenticateClientAsync(request.ClientId, token, db, cancellationToken).ConfigureAwait(false);
    if (client is null)
    {
        return Results.Unauthorized();
    }

    var range = await rangeService.AssignNextRangeAsync(db, client, cancellationToken).ConfigureAwait(false);
    if (range is null)
    {
        return Results.NotFound("No ranges available.");
    }

    await db.Entry(range).Reference(r => r.PuzzleDefinition).LoadAsync(cancellationToken).ConfigureAwait(false);

    return Results.Ok(ToDescriptor(range));
});

app.MapPost("/api/ranges/report", async (
    RangeProgressReportRequest request,
    HttpRequest httpRequest,
    PoolDbContext db,
    RangeService rangeService,
    CancellationToken cancellationToken) =>
{
    var token = httpRequest.Headers["X-Client-Token"].ToString();
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    var client = await AuthenticateClientAsync(request.ClientId, token, db, cancellationToken).ConfigureAwait(false);
    if (client is null)
    {
        return Results.Unauthorized();
    }

    var range = await db.RangeAssignments
        .Include(r => r.PuzzleDefinition)
        .FirstOrDefaultAsync(r => r.Id == request.RangeId, cancellationToken)
        .ConfigureAwait(false);
    if (range is null)
    {
        return Results.NotFound("Range not found.");
    }

    if (range.AssignedToClientId != client.Id)
    {
        return Results.BadRequest("Range does not belong to client.");
    }

    var now = DateTime.UtcNow;
    range.ProgressPercent = Math.Clamp(request.ProgressPercent, 0, 100);
    range.LastUpdateUtc = now;
    range.ReportedSpeedKeysPerSecond = request.SpeedKeysPerSecond ?? range.ReportedSpeedKeysPerSecond;

    client.LastSeenUtc = now;
    if (request.CardsConnected.HasValue)
    {
        client.CardsConnected = Math.Max(0, request.CardsConnected.Value);
    }
    if (request.SpeedKeysPerSecond.HasValue)
    {
        client.SpeedKeysPerSecond = Math.Max(0, request.SpeedKeysPerSecond.Value);
    }

    var markedComplete = request.MarkComplete || range.ProgressPercent >= 100;
    if (markedComplete)
    {
        range.Status = RangeStatus.Completed;
        range.ProgressPercent = 100;
        range.CompletedAtUtc = now;
        client.Status = ClientStatus.Completed;
        client.CurrentRangeId = null;
    }
    else
    {
        range.Status = RangeStatus.Assigned;
        client.Status = ClientStatus.Scanning;
        client.CurrentRangeId = range.Id;
        client.Puzzle = range.Puzzle;
    }

    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    RangeAssignment? nextRange = null;
    if (markedComplete)
    {
        nextRange = await rangeService.AssignNextRangeAsync(db, client, cancellationToken).ConfigureAwait(false);
        if (nextRange is not null)
            await db.Entry(nextRange).Reference(r => r.PuzzleDefinition).LoadAsync(cancellationToken).ConfigureAwait(false);
    }

    var response = new RangeReportResponse
    {
        CurrentRange = ToDescriptor(range),
        NextRange = nextRange is null ? null : ToDescriptor(nextRange),
        HasMoreWork = nextRange is not null
    };

    return Results.Ok(response);
});

app.MapGet("/api/stats/overview", async (
    PoolDbContext db,
    RangeService rangeService,
    IOptions<PoolOptions> poolOptions,
    CancellationToken cancellationToken) =>
{
    var options = poolOptions.Value;
    var now = DateTime.UtcNow;
    var offlineThreshold = now - options.WorkerOfflineAfter;

    var puzzles = await db.PuzzleDefinitions
        .OrderBy(p => p.DisplayName)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    if (puzzles.Count == 0)
    {
        var seeded = await rangeService.EnsureDefaultPuzzleAsync(db, cancellationToken).ConfigureAwait(false);
        if (seeded is not null)
            puzzles.Add(seeded);
    }

    var assignmentGroups = await db.RangeAssignments
        .GroupBy(r => new { r.PuzzleId, r.Status })
        .Select(g => new
        {
            g.Key.PuzzleId,
            g.Key.Status,
            Count = g.Count()
        })
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    var keyGroups = await db.KeyFindEvents
        .GroupBy(e => e.Puzzle)
        .Select(g => new { Puzzle = g.Key, Count = g.Count() })
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    var clients = await db.Clients
        .Include(c => c.CurrentRange)
        .OrderBy(c => c.User)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    var workersOnline = clients.Count(c => c.LastSeenUtc >= offlineThreshold);
    var totalSpeed = clients
        .Where(c => c.LastSeenUtc >= offlineThreshold)
        .Sum(c => c.SpeedKeysPerSecond);

    var workerDtos = clients.Select(c => new WorkerStatDto
    {
        ClientId = c.Id,
        User = c.User,
        WorkerName = c.WorkerName,
        ApplicationType = c.ApplicationType.ToString(),
        PuzzleCode = c.Puzzle,
        CardsConnected = c.CardsConnected,
        SpeedKeysPerSecond = c.SpeedKeysPerSecond,
        LastSeenUtc = c.LastSeenUtc,
        CurrentRange = c.CurrentRange is null ? null : $"{c.CurrentRange.PrefixStart}-{c.CurrentRange.PrefixEnd}",
        CurrentRangeProgress = c.CurrentRange?.ProgressPercent,
        Status = c.Status.ToString()
    }).ToList();

    var activeRanges = await db.RangeAssignments
        .Include(r => r.AssignedToClient)
        .Where(r => r.Status != RangeStatus.Completed)
        .OrderByDescending(r => r.LastUpdateUtc)
        .Take(25)
        .Select(r => new RangeSummaryDto
        {
            RangeId = r.Id,
            PrefixStart = r.PrefixStart,
            PrefixEnd = r.PrefixEnd,
            ProgressPercent = r.ProgressPercent,
            Status = r.Status.ToString(),
            PuzzleCode = r.Puzzle,
            AssignedTo = r.AssignedToClient != null ? r.AssignedToClient.WorkerName ?? r.AssignedToClient.User : null,
            LastUpdateUtc = r.LastUpdateUtc
        })
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    var puzzleDtos = new List<PuzzleOverviewDto>();
    foreach (var puzzle in puzzles)
    {
        var totalSlots = ComputeTotalChunks(puzzle);
        var completed = assignmentGroups
            .Where(g => g.PuzzleId == puzzle.Id && g.Status == RangeStatus.Completed)
            .Sum(g => g.Count);
        var inProgress = assignmentGroups
            .Where(g => g.PuzzleId == puzzle.Id && g.Status == RangeStatus.Assigned)
            .Sum(g => g.Count);
        var keysFound = keyGroups
            .Where(g => string.Equals(g.Puzzle, puzzle.Code, StringComparison.OrdinalIgnoreCase))
            .Sum(g => g.Count);

        double percentage = totalSlots <= 0
            ? 0
            : Math.Clamp(completed / (double)totalSlots * 100, 0, 100);

        puzzleDtos.Add(new PuzzleOverviewDto
        {
            PuzzleId = puzzle.Id,
            Code = puzzle.Code,
            DisplayName = puzzle.DisplayName,
            RangesTotal = totalSlots > int.MaxValue ? int.MaxValue : (int)totalSlots,
            RangesCompleted = completed,
            RangesInProgress = inProgress,
            PercentageSearched = percentage,
            KeysFound = keysFound
        });
    }

    var response = new StatsOverviewResponse
    {
        TotalSpeedKeysPerSecond = totalSpeed,
        WorkersOnline = workersOnline,
        GeneratedAtUtc = now,
        Puzzles = puzzleDtos,
        Workers = workerDtos,
        ActiveRanges = activeRanges
    };

    return Results.Ok(response);
});

app.MapPost("/api/events/key-found", async (
    KeyFoundRequest request,
    HttpRequest httpRequest,
    PoolDbContext db,
    NotificationService notificationService,
    CancellationToken cancellationToken) =>
{
    var token = httpRequest.Headers["X-Client-Token"].ToString();
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.PrivateKey))
    {
        return Results.BadRequest("Private key is required.");
    }

    var client = await AuthenticateClientAsync(request.ClientId, token, db, cancellationToken).ConfigureAwait(false);
    if (client is null)
    {
        return Results.Unauthorized();
    }

    var range = request.RangeId.HasValue
        ? await db.RangeAssignments.FirstOrDefaultAsync(r => r.Id == request.RangeId.Value, cancellationToken).ConfigureAwait(false)
        : null;

    var puzzle = request.Puzzle ?? client.Puzzle;

    var keyEvent = new KeyFindEvent
    {
        Id = Guid.NewGuid(),
        ClientId = client.Id,
        RangeId = request.RangeId,
        Puzzle = puzzle,
        WorkerName = client.WorkerName ?? client.User,
        User = client.User,
        PrivateKey = request.PrivateKey.Trim(),
        ReportedAtUtc = DateTime.UtcNow
    };

    db.KeyFindEvents.Add(keyEvent);

    if (range is not null)
    {
        range.Status = RangeStatus.Completed;
        range.CompletedAtUtc = DateTime.UtcNow;
        range.ProgressPercent = 100;
    }
    client.Status = ClientStatus.Completed;
    client.CurrentRangeId = null;
    client.LastSeenUtc = DateTime.UtcNow;

    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    var notifyMessage = $"*Key Found!*\nWorker: `{client.WorkerName ?? client.User}`\nPuzzle: `{puzzle}`\nRange: `{range?.PrefixStart}-{range?.PrefixEnd}`";
    await notificationService.SendKeyFoundAsync(notifyMessage, cancellationToken).ConfigureAwait(false);

    return Results.Ok(new { accepted = true });
});

app.MapGet("/api/admin/puzzles", async (
    HttpRequest httpRequest,
    PoolDbContext db,
    IOptions<PoolOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!AuthorizeAdmin(httpRequest, options.Value))
        return Results.Unauthorized();

    var puzzles = await db.PuzzleDefinitions
        .OrderBy(p => p.DisplayName)
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);

    var response = puzzles.Select(ToDto).ToList();
    return Results.Ok(response);
});

app.MapPut("/api/admin/puzzles/{code}", async (
    string code,
    PuzzleDefinitionDto dto,
    HttpRequest httpRequest,
    PoolDbContext db,
    IOptions<PoolOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!AuthorizeAdmin(httpRequest, options.Value))
        return Results.Unauthorized();

    code = (code ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("Puzzle code is required.");

    var normalizedCode = code.ToUpperInvariant();
    var puzzle = await db.PuzzleDefinitions.FirstOrDefaultAsync(p => p.Code == normalizedCode, cancellationToken).ConfigureAwait(false);
    if (puzzle is null)
    {
        puzzle = new PuzzleDefinition
        {
            Id = Guid.NewGuid(),
            Code = normalizedCode,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.PuzzleDefinitions.Add(puzzle);
    }

    try
    {
        ApplyPuzzleUpdates(puzzle, dto);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
    puzzle.Code = normalizedCode;
    puzzle.UpdatedAtUtc = DateTime.UtcNow;

    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return Results.Ok(ToDto(puzzle));
});

app.MapDelete("/api/admin/puzzles/{code}", async (
    string code,
    HttpRequest httpRequest,
    PoolDbContext db,
    IOptions<PoolOptions> options,
    CancellationToken cancellationToken) =>
{
    if (!AuthorizeAdmin(httpRequest, options.Value))
        return Results.Unauthorized();

    code = (code ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(code))
        return Results.BadRequest("Puzzle code is required.");

    var normalizedCode = code.ToUpperInvariant();
    var puzzle = await db.PuzzleDefinitions.FirstOrDefaultAsync(p => p.Code == normalizedCode, cancellationToken).ConfigureAwait(false);
    if (puzzle is null)
        return Results.NotFound();

    db.PuzzleDefinitions.Remove(puzzle);
    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return Results.Ok();
});

app.Run();

static ClientApplicationType ParseApplicationType(string? type)
{
    if (string.IsNullOrWhiteSpace(type))
    {
        return ClientApplicationType.Unknown;
    }

    return type.Trim().ToLowerInvariant() switch
    {
        "bitcrack" => ClientApplicationType.Bitcrack,
        "vanitysearch" => ClientApplicationType.VanitySearch,
        "custom" => ClientApplicationType.Custom,
        "cpu" => ClientApplicationType.VanitySearch,
        _ => ClientApplicationType.Unknown
    };
}

static RangeDescriptor ToDescriptor(RangeAssignment range) => new()
{
    RangeId = range.Id,
    Puzzle = range.Puzzle,
    PrefixStart = range.PrefixStart,
    PrefixEnd = range.PrefixEnd,
    RangeStart = range.RangeStartHex,
    RangeEnd = range.RangeEndHex,
    ChunkSize = range.ChunkSize,
    TargetAddress = range.PuzzleDefinition?.TargetAddress ?? string.Empty,
    WorkloadStartSuffix = range.PuzzleDefinition?.WorkloadStartSuffix ?? string.Empty,
    WorkloadEndSuffix = range.PuzzleDefinition?.WorkloadEndSuffix ?? string.Empty
};

static async Task<Client?> AuthenticateClientAsync(Guid clientId, string token, PoolDbContext db, CancellationToken cancellationToken)
{
    return await db.Clients.Include(c => c.CurrentRange)
        .FirstOrDefaultAsync(c => c.Id == clientId && c.ApiKey == token, cancellationToken)
        .ConfigureAwait(false);
}

static int ComputeTotalChunks(PuzzleDefinition puzzle)
{
    var chunk = BigInteger.Max(BigInteger.One, new BigInteger(puzzle.ChunkSize));
    var min = ParseHex(puzzle.MinPrefixHex);
    var max = ParseHex(puzzle.MaxPrefixHex);
    if (max < min)
        return 0;

    var totalPrefixes = max - min + BigInteger.One;
    var totalChunks = BigInteger.Divide(totalPrefixes + chunk - BigInteger.One, chunk);
    if (totalChunks > int.MaxValue)
        return int.MaxValue;
    if (totalChunks < 0)
        return 0;
    return (int)totalChunks;
}

static BigInteger ParseHex(string hex)
{
    if (string.IsNullOrWhiteSpace(hex))
        return BigInteger.Zero;
    return BigInteger.Parse(hex.Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
}

static bool AuthorizeAdmin(HttpRequest request, PoolOptions options)
{
    if (string.IsNullOrWhiteSpace(options.AdminApiKey))
        return false;
    var provided = request.Headers["X-Admin-Key"].ToString();
    return string.Equals(provided?.Trim(), options.AdminApiKey.Trim(), StringComparison.Ordinal);
}

static PuzzleDefinitionDto ToDto(PuzzleDefinition puzzle) => new()
{
    Id = puzzle.Id,
    Code = puzzle.Code,
    DisplayName = puzzle.DisplayName,
    Enabled = puzzle.Enabled,
    Randomized = puzzle.Randomized,
    Weight = puzzle.Weight,
    TargetAddress = puzzle.TargetAddress,
    MinPrefixHex = puzzle.MinPrefixHex,
    MaxPrefixHex = puzzle.MaxPrefixHex,
    PrefixLength = puzzle.PrefixLength,
    ChunkSize = puzzle.ChunkSize,
    WorkloadStartSuffix = puzzle.WorkloadStartSuffix,
    WorkloadEndSuffix = puzzle.WorkloadEndSuffix,
    Notes = puzzle.Notes
};

static void ApplyPuzzleUpdates(PuzzleDefinition puzzle, PuzzleDefinitionDto dto)
{
    if (!string.IsNullOrWhiteSpace(dto.DisplayName))
        puzzle.DisplayName = dto.DisplayName.Trim();
    if (!string.IsNullOrWhiteSpace(dto.TargetAddress))
        puzzle.TargetAddress = dto.TargetAddress.Trim();
    if (!string.IsNullOrWhiteSpace(dto.MinPrefixHex))
        puzzle.MinPrefixHex = NormalizeHex(dto.MinPrefixHex);
    if (!string.IsNullOrWhiteSpace(dto.MaxPrefixHex))
        puzzle.MaxPrefixHex = NormalizeHex(dto.MaxPrefixHex);

    if (string.IsNullOrWhiteSpace(puzzle.MinPrefixHex) || string.IsNullOrWhiteSpace(puzzle.MaxPrefixHex))
        throw new InvalidOperationException("Min and max prefix values are required.");
    if (!string.IsNullOrWhiteSpace(dto.WorkloadStartSuffix))
        puzzle.WorkloadStartSuffix = dto.WorkloadStartSuffix.Trim();
    if (!string.IsNullOrWhiteSpace(dto.WorkloadEndSuffix))
        puzzle.WorkloadEndSuffix = dto.WorkloadEndSuffix.Trim();
    if (!string.IsNullOrWhiteSpace(dto.Notes))
        puzzle.Notes = dto.Notes.Trim();

    var minValue = ParseHex(puzzle.MinPrefixHex);
    var maxValue = ParseHex(puzzle.MaxPrefixHex);
    if (maxValue < minValue)
        throw new InvalidOperationException("Max prefix must be greater than or equal to min prefix.");

    puzzle.Enabled = dto.Enabled;
    puzzle.Randomized = dto.Randomized;
    puzzle.Weight = dto.Weight <= 0 ? 1 : dto.Weight;
    puzzle.ChunkSize = dto.ChunkSize <= 0 ? 1 : dto.ChunkSize;
    puzzle.PrefixLength = dto.PrefixLength > 0 ? dto.PrefixLength : puzzle.MinPrefixHex.Length;
}

static string NormalizeHex(string hex)
{
    return hex.Trim().Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
}
