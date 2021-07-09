namespace Microsoft.EntityFrameworkCore
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlite<DbContextType>(this IServiceCollection services, string connectionString) where DbContextType : DbContext
        {
            return services.AddDbContext<DbContextType>(options => options.UseSqlite(connectionString));
        }
    }
}