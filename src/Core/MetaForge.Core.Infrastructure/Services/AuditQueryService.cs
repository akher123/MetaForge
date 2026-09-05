using System.Text.Json;
using MetaForge.Domain.Audit;

namespace MetaForge.Infrastructure.Services;

/// <summary>
/// Queries audit logs and builds human-readable change summaries from JSON snapshots.
/// </summary>
public class AuditQueryService : IAuditQueryService
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new() { WriteIndented = true };

    private readonly MetaForgeDbContext _dbContext;
    private readonly IFormMetadataCache _formCache;

    public AuditQueryService(MetaForgeDbContext dbContext, IFormMetadataCache formCache)
    {
        _dbContext = dbContext;
        _formCache = formCache;
    }

    public async Task<PagedResult<AuditLogListItemDto>> GetPagedAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = _dbContext.AuditLogs.AsNoTracking().AsQueryable();
        q = ApplyFilters(q, query);

        var total = await q.CountAsync(cancellationToken);

        var rows = await q
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows.Select(row =>
        {
            var changeCount = CountChanges(row.Action, row.OldValue, row.NewValue);
            return new AuditLogListItemDto
            {
                Id = row.Id,
                EntityName = row.EntityName,
                RecordId = row.RecordId,
                Action = row.Action,
                UserName = row.UserName,
                Timestamp = row.Timestamp,
                Summary = BuildSummary(row.Action, changeCount),
                ChangeCount = changeCount
            };
        }).ToList();

        return new PagedResult<AuditLogListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AuditLogDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        var row = await _dbContext.AuditLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (row == null)
            return null;

        var labelMap = await BuildFieldLabelMapAsync(row.EntityName, cancellationToken);
        var changeCount = CountChanges(row.Action, row.OldValue, row.NewValue);
        var (changes, sections) = BuildChanges(row.Action, row.OldValue, row.NewValue, labelMap);

        var timelineRows = await _dbContext.AuditLogs.AsNoTracking()
            .Where(x => x.EntityName == row.EntityName && x.RecordId == row.RecordId)
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Take(50)
            .ToListAsync(cancellationToken);

        var timelineItems = timelineRows.Select(t => new AuditTimelineItemDto
        {
            Id = t.Id,
            Timestamp = t.Timestamp,
            Action = t.Action,
            UserName = t.UserName,
            Summary = BuildSummary(t.Action, CountChanges(t.Action, t.OldValue, t.NewValue))
        }).ToList();

        return new AuditLogDetailDto
        {
            Id = row.Id,
            EntityName = row.EntityName,
            RecordId = row.RecordId,
            Action = row.Action,
            UserName = row.UserName,
            Timestamp = row.Timestamp,
            Summary = BuildSummary(row.Action, changeCount),
            Changes = changes,
            Sections = sections,
            OldValueJson = PrettyJson(row.OldValue),
            NewValueJson = PrettyJson(row.NewValue),
            Timeline = timelineItems
        };
    }

    public async Task<IReadOnlyList<AuditEntityOptionDto>> GetEntityOptionsAsync(CancellationToken cancellationToken = default)
    {
        var entityNames = await _dbContext.AuditLogs.AsNoTracking()
            .Select(x => x.EntityName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var forms = await _dbContext.ForgeForms.AsNoTracking()
            .Where(f => entityNames.Contains(f.EntityName))
            .Select(f => new { f.EntityName, f.Name })
            .ToListAsync(cancellationToken);

        var formNames = forms
            .GroupBy(f => f.EntityName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);

        return entityNames.Select(name => new AuditEntityOptionDto
        {
            EntityName = name,
            FormName = formNames.GetValueOrDefault(name)
        }).ToList();
    }

    public async Task<IReadOnlyList<string>> GetActionOptionsAsync(CancellationToken cancellationToken = default) =>
        await _dbContext.AuditLogs.AsNoTracking()
            .Select(x => x.Action)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

    private static IQueryable<AuditLog> ApplyFilters(IQueryable<AuditLog> q, AuditLogQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.EntityName))
            q = q.Where(x => x.EntityName == query.EntityName);

        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(x => x.Action == query.Action);

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            var user = query.UserName.Trim();
            q = q.Where(x => x.UserName != null && x.UserName.Contains(user));
        }

        if (!string.IsNullOrWhiteSpace(query.RecordId))
        {
            var recordId = query.RecordId.Trim();
            q = q.Where(x => x.RecordId == recordId);
        }

        if (query.From.HasValue)
            q = q.Where(x => x.Timestamp >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(x => x.Timestamp <= query.To.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(x =>
                x.EntityName.Contains(search)
                || x.RecordId.Contains(search)
                || (x.UserName != null && x.UserName.Contains(search))
                || x.Action.Contains(search));
        }

        return q;
    }

    private async Task<Dictionary<string, string>> BuildFieldLabelMapAsync(
        string entityName,
        CancellationToken cancellationToken)
    {
        var form = await _formCache.GetByEntityNameAsync(entityName, cancellationToken);
        if (form?.Fields == null || form.Fields.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return form.Fields
            .Where(f => !string.IsNullOrWhiteSpace(f.PropertyName))
            .GroupBy(f => f.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => string.IsNullOrWhiteSpace(g.First().Label) ? g.Key : g.First().Label,
                StringComparer.OrdinalIgnoreCase);
    }

    private static (IReadOnlyList<AuditChangeDto> Changes, IReadOnlyList<AuditSectionDto> Sections) BuildChanges(
        string action,
        string? oldValue,
        string? newValue,
        IReadOnlyDictionary<string, string> labelMap)
    {
        if (string.Equals(action, "SaveMasterDetail", StringComparison.OrdinalIgnoreCase))
            return ([], BuildMasterDetailSections(newValue));

        return action switch
        {
            "Insert" => (BuildFieldChanges(null, newValue, labelMap, includeAddedOnly: true), []),
            "Delete" => (BuildFieldChanges(oldValue, null, labelMap, includeRemovedOnly: true), []),
            "Update" => (BuildFieldChanges(oldValue, newValue, labelMap), []),
            _ => (BuildFieldChanges(oldValue, newValue, labelMap), [])
        };
    }

    private static IReadOnlyList<AuditSectionDto> BuildMasterDetailSections(string? newValue)
    {
        if (string.IsNullOrWhiteSpace(newValue))
            return [];

        var root = ParseObject(newValue);
        if (root.Count == 0)
            return [new AuditSectionDto { Name = "Payload", Content = newValue }];

        var sections = new List<AuditSectionDto>();
        foreach (var (key, value) in root.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            sections.Add(new AuditSectionDto
            {
                Name = FormatSectionName(key),
                Content = PrettyElement(value)
            });
        }

        return sections;
    }

    private static IReadOnlyList<AuditChangeDto> BuildFieldChanges(
        string? oldValue,
        string? newValue,
        IReadOnlyDictionary<string, string> labelMap,
        bool includeAddedOnly = false,
        bool includeRemovedOnly = false)
    {
        var oldDict = ParseObject(oldValue);
        var newDict = ParseObject(newValue);
        var keys = oldDict.Keys.Union(newDict.Keys).OrderBy(k => k, StringComparer.OrdinalIgnoreCase);
        var changes = new List<AuditChangeDto>();

        foreach (var key in keys)
        {
            var hasOld = oldDict.TryGetValue(key, out var oldEl);
            var hasNew = newDict.TryGetValue(key, out var newEl);

            if (!hasOld && hasNew)
            {
                if (includeRemovedOnly) continue;
                changes.Add(new AuditChangeDto
                {
                    Field = key,
                    Label = ResolveLabel(key, labelMap),
                    OldValue = null,
                    NewValue = FormatElement(newEl),
                    ChangeType = "Added"
                });
                continue;
            }

            if (hasOld && !hasNew)
            {
                if (includeAddedOnly) continue;
                changes.Add(new AuditChangeDto
                {
                    Field = key,
                    Label = ResolveLabel(key, labelMap),
                    OldValue = FormatElement(oldEl),
                    NewValue = null,
                    ChangeType = "Removed"
                });
                continue;
            }

            if (hasOld && hasNew && !ElementsEqual(oldEl, newEl))
            {
                if (includeAddedOnly || includeRemovedOnly) continue;
                changes.Add(new AuditChangeDto
                {
                    Field = key,
                    Label = ResolveLabel(key, labelMap),
                    OldValue = FormatElement(oldEl),
                    NewValue = FormatElement(newEl),
                    ChangeType = "Modified"
                });
            }
        }

        return changes;
    }

    private static int CountChanges(string action, string? oldValue, string? newValue)
    {
        if (string.Equals(action, "Insert", StringComparison.OrdinalIgnoreCase))
            return ParseObject(newValue).Count;

        if (string.Equals(action, "Delete", StringComparison.OrdinalIgnoreCase))
            return ParseObject(oldValue).Count;

        if (string.Equals(action, "SaveMasterDetail", StringComparison.OrdinalIgnoreCase))
            return ParseObject(newValue).Count;

        var oldDict = ParseObject(oldValue);
        var newDict = ParseObject(newValue);
        var keys = oldDict.Keys.Union(newDict.Keys);
        var count = 0;

        foreach (var key in keys)
        {
            var hasOld = oldDict.TryGetValue(key, out var oldEl);
            var hasNew = newDict.TryGetValue(key, out var newEl);

            if (!hasOld && hasNew || hasOld && !hasNew)
                count++;
            else if (hasOld && hasNew && !ElementsEqual(oldEl, newEl))
                count++;
        }

        return count;
    }

    private static string BuildSummary(string action, int changeCount) => action switch
    {
        "Insert" => "Record created",
        "Delete" => "Record deleted",
        "Update" => changeCount > 0 ? $"{changeCount} field(s) changed" : "Record updated",
        "SaveMasterDetail" => changeCount > 0 ? $"Master-detail saved ({changeCount} section(s))" : "Master-detail saved",
        _ => changeCount > 0 ? $"{action} ({changeCount} change(s))" : action
    };

    private static string ResolveLabel(string field, IReadOnlyDictionary<string, string> labelMap) =>
        labelMap.TryGetValue(field, out var label) && !string.IsNullOrWhiteSpace(label) ? label : field;

    private static Dictionary<string, JsonElement> ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Value"] = doc.RootElement.Clone()
                };

            return doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool ElementsEqual(JsonElement left, JsonElement right) =>
        string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);

    private static string FormatElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "Yes",
        JsonValueKind.False => "No",
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Array => $"[{element.GetArrayLength()} item(s)]",
        JsonValueKind.Object => PrettyElement(element),
        _ => element.GetRawText()
    };

    private static string PrettyElement(JsonElement element) =>
        JsonSerializer.Serialize(element, PrettyJsonOptions);

    private static string? PrettyJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, PrettyJsonOptions);
        }
        catch
        {
            return json;
        }
    }

    private static string FormatSectionName(string key) => key switch
    {
        "masterData" => "Master Data",
        "detailData" => "Detail Data",
        "deletedDetailIds" => "Deleted Detail Rows",
        "detailSections" => "Detail Sections",
        _ => SplitPascalCase(key)
    };

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return string.Concat(value.Select((c, i) =>
            i > 0 && char.IsUpper(c) && !char.IsUpper(value[i - 1]) ? " " + c : c.ToString()));
    }
}
