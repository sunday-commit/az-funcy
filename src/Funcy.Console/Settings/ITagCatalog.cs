using Funcy.Data;
using Microsoft.EntityFrameworkCore;

namespace Funcy.Console.Settings;

// Surfaces the distinct tag keys cached across all function apps, used to suggest values while
// editing the TagColumns setting. A DB failure yields an empty list so it never blocks editing.
public interface ITagCatalog
{
    Task<IReadOnlyList<string>> GetDistinctTagKeysAsync();
}

public sealed class TagCatalog(IDbContextFactory<FunctionAppDbContext> dbFactory) : ITagCatalog
{
    public async Task<IReadOnlyList<string>> GetDistinctTagKeysAsync()
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var keys = await db.FunctionAppTags.Select(t => t.Key).ToListAsync();
            return TagColumnSuggestions.Distinct(keys);
        }
        catch
        {
            // A hint is a nicety; if the small tag table can't be read, edit without it.
            return [];
        }
    }
}
