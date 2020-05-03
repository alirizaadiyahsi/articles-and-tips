### Entity Framework Core Global Filtreler (Global Query Filters)

Bazen verileri veritabanından silmek yerine, `IsDeleted` diye bir alan ekleyip, veriyi silmek yerine bu alanla silinen verileri filtreleriz (SoftDelete).
`SoftDelete` olarak silinen verileri veritabanından çekerken, her sorguda `IsDeleted==false` diye filtre eklemek yerine, global filtreleri kullanabiliriz.

Bunun için öncelikle `ISoftDelete` diye bir interface tanımlayıp, bunu kalıtım alan tüm sınıfları, global filtreden geçireceğiz.

**ISoftDelete.cs**

````c#
public interface ISoftDelete
{
    bool IsDeleted { get; set; }
}
````

**Product.cs**

````c#
public class Product : ISoftDelete
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public bool IsDeleted { get; set; }
}
````

`DbContext` tanımlarken, aşağıdaki gibi bir ayar yapıyoruz.

**DbContext**

````c#
public class EShopDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(@"Server=localhost;Database=EShopDB;Trusted_Connection=True;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ISoftDelete sınıfından kalıtım alan tüm sınıflara query-filter uyguluyoruz 
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                modelBuilder.Entity(entityType.ClrType).AddQueryFilter<ISoftDelete>(e => e.IsDeleted == false);
        }
    }
}
````

Ayrıca filtreyi "generic" olarak eklemek için aşağıdaki gibi bir "extension" kullanabilirsiniz.

**EntityTypeBuilderExtensions**

````c#
public static class EntityTypeBuilderExtensions
{
    public static void AddQueryFilter<T>(this EntityTypeBuilder entityTypeBuilder, Expression<Func<T, bool>> expression)
    {
        var parameterType = Expression.Parameter(entityTypeBuilder.Metadata.ClrType);
        var expressionFilter = ReplacingExpressionVisitor.Replace(
            expression.Parameters.Single(), parameterType, expression.Body);

        var currentQueryFilter = entityTypeBuilder.Metadata.GetQueryFilter();
        if (currentQueryFilter != null)
        {
            var currentExpressionFilter = ReplacingExpressionVisitor.Replace(
                currentQueryFilter.Parameters.Single(), parameterType, currentQueryFilter.Body);
            expressionFilter = Expression.AndAlso(currentExpressionFilter, expressionFilter);
        }

        var lambdaExpression = Expression.Lambda(expressionFilter, parameterType);
        entityTypeBuilder.HasQueryFilter(lambdaExpression);
    }
}
````

Artık tüm veritabanı sorguları `IsDeleted == false` olarak filrelenmiş olarak gelecek. Ekstra `where` sorgusu yazmaya gerek yok.

#### Kaynaklar

https://gist.github.com/haacked/febe9e88354fb2f4a4eb11ba88d64c24
