using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Funcy.Data;

public class FunctionAppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FunctionAppDbContext>
{
    public FunctionAppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FunctionAppDbContext>();
        optionsBuilder.UseSqlite("Data Source=design_time.db");
        return new FunctionAppDbContext(optionsBuilder.Options);
    }
}
