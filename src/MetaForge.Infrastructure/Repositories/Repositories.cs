namespace MetaForge.Infrastructure.Repositories;

public class ForgeFormRepository : IForgeFormRepository
{
    private readonly MetaForgeDbContext _context;

    public ForgeFormRepository(MetaForgeDbContext context) => _context = context;

    public async Task<ForgeForm?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.ForgeForms
            .Include(m => m.Fields.OrderBy(f => f.DisplayOrder))
            .Include(m => m.Relations)
            .Include(m => m.GridColumns.OrderBy(c => c.DisplayOrder))
            .Include(m => m.GridActions.OrderBy(a => a.DisplayOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ForgeForm>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.ForgeForms
            .Include(m => m.Fields)
            .Include(m => m.Relations)
            .AsNoTracking()
            .OrderBy(m => m.GroupName)
            .ThenBy(m => m.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ForgeForm entity, CancellationToken cancellationToken = default) =>
        await _context.ForgeForms.AddAsync(entity, cancellationToken);

    public void Update(ForgeForm entity) => _context.ForgeForms.Update(entity);

    public void Remove(ForgeForm entity) => _context.ForgeForms.Remove(entity);

    public async Task<ForgeForm?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await _context.ForgeForms
            .Include(m => m.Fields.OrderBy(f => f.DisplayOrder))
            .Include(m => m.Relations)
            .Include(m => m.GridColumns.OrderBy(c => c.DisplayOrder))
            .Include(m => m.GridActions.OrderBy(a => a.DisplayOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken);

    public async Task<ForgeForm?> GetByEntityNameAsync(string entityName, CancellationToken cancellationToken = default) =>
        await _context.ForgeForms
            .Include(m => m.Fields.OrderBy(f => f.DisplayOrder))
            .Include(m => m.Relations)
            .Include(m => m.GridColumns.OrderBy(c => c.DisplayOrder))
            .Include(m => m.GridActions.OrderBy(a => a.DisplayOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.EntityName == entityName, cancellationToken);

    public async Task<IReadOnlyList<ForgeForm>> GetActiveFormsAsync(CancellationToken cancellationToken = default) =>
        await _context.ForgeForms
            .Include(m => m.Relations)
            .Where(m => m.IsActive)
            .OrderBy(m => m.GroupName)
            .ThenBy(m => m.DisplayOrder)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<ForgeForm?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.ForgeForms
            .Include(m => m.Fields)
            .Include(m => m.Relations)
            .Include(m => m.GridColumns)
            .Include(m => m.GridActions)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await _context.ForgeForms.AnyAsync(m => m.Code == code && (!excludeId.HasValue || m.Id != excludeId.Value), cancellationToken);

    public async Task<bool> ExistsByEntityNameAsync(string entityName, int? excludeId = null, CancellationToken cancellationToken = default) =>
        await _context.ForgeForms.AnyAsync(m => m.EntityName == entityName && (!excludeId.HasValue || m.Id != excludeId.Value), cancellationToken);
}

public class ForgeMenuRepository : IForgeMenuRepository
{
    private readonly MetaForgeDbContext _context;

    public ForgeMenuRepository(MetaForgeDbContext context) => _context = context;

    public async Task<ForgeMenu?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus
            .Include(m => m.Form)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ForgeMenu>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus
            .Include(m => m.Form)
            .AsNoTracking()
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ForgeMenu entity, CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus.AddAsync(entity, cancellationToken);

    public void Update(ForgeMenu entity) => _context.ForgeMenus.Update(entity);

    public void Remove(ForgeMenu entity) => _context.ForgeMenus.Remove(entity);

    public async Task<IReadOnlyList<ForgeMenu>> GetActiveTreeAsync(CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus
            .Include(m => m.Form)
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<ForgeMenu?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus
            .Include(m => m.Form)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<ForgeMenu?> GetByFormIdAsync(int formId, CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus
            .Include(m => m.Form)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.FormId == formId, cancellationToken);

    public async Task<ForgeMenu?> GetByFormIdTrackedAsync(int formId, CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus
            .FirstOrDefaultAsync(m => m.FormId == formId, cancellationToken);

    public async Task<ForgeMenu?> FindFolderByNameAsync(string name, int? parentId, CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus
            .FirstOrDefaultAsync(m =>
                m.ItemType == MenuItemType.Folder
                && m.Name == name
                && m.ParentId == parentId
                && m.IsActive,
                cancellationToken);

    public async Task<bool> HasChildrenAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.ForgeMenus.AnyAsync(m => m.ParentId == id, cancellationToken);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly MetaForgeDbContext _context;

    public UnitOfWork(MetaForgeDbContext context, IForgeFormRepository forms, IForgeMenuRepository menus)
    {
        _context = context;
        Forms = forms;
        Menus = menus;
    }

    public IForgeFormRepository Forms { get; }

    public IForgeMenuRepository Menus { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
