namespace MetaForge.Application.Interfaces.Repositories;

/// <summary>
/// Generic repository contract with a typed key.
/// </summary>
public interface IRepository<T, TKey> where T : class
{
    Task<T?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}

/// <summary>
/// Generic repository contract with integer key (default).
/// </summary>
public interface IRepository<T> : IRepository<T, int> where T : class
{
}

/// <summary>
/// Admin form repository.
/// </summary>
public interface IForgeFormRepository : IRepository<ForgeForm>
{
    Task<ForgeForm?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<ForgeForm?> GetByEntityNameAsync(string entityName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForgeForm>> GetActiveFormsAsync(CancellationToken cancellationToken = default);

    Task<ForgeForm?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEntityNameAsync(string entityName, int? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForgeTreeLevel>> GetTreeLevelsAsync(int formId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin menu repository.
/// </summary>
public interface IForgeMenuRepository : IRepository<ForgeMenu>
{
    Task<IReadOnlyList<ForgeMenu>> GetActiveTreeAsync(CancellationToken cancellationToken = default);

    Task<ForgeMenu?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default);

    Task<ForgeMenu?> GetByFormIdAsync(int formId, CancellationToken cancellationToken = default);

    Task<ForgeMenu?> GetByFormIdTrackedAsync(int formId, CancellationToken cancellationToken = default);

    Task<ForgeMenu?> FindFolderByNameAsync(string name, int? parentId, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin report repository.
/// </summary>
public interface IForgeReportRepository : IRepository<ForgeReport>
{
    Task<ForgeReport?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForgeReport>> GetActiveReportsAsync(CancellationToken cancellationToken = default);

    Task<ForgeReport?> GetByIdTrackedAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of work for transactional operations.
/// </summary>
public interface IUnitOfWork
{
    IForgeFormRepository Forms { get; }

    IForgeMenuRepository Menus { get; }

    IForgeReportRepository Reports { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
