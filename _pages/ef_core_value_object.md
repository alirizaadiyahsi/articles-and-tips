### Entity Framework Core ve Value Object

`Value Object` için çok fazla tanım ve teknik açıklama var.
Bundan dolayı `Value Object` nedir, konularına girmeyeceğim. 
Yinede merak edenler için; `Value Object` nedir, nasıl ortaya çıkmıştır, hangi amaçla kullanılır ve daha birçok detay, kapsamlı olarak [burada](https://martinfowler.com/bliki/ValueObject.html) anlatılıyor.

Bir entity sınıfı üzerinde birbiri ile ilişkili alanların bir araya gelerek, 
anlamlı bir nesneyi ifade edebilen, 
ama yine de domain için tek başına anlamlı olmayan nesneler `Value Object` olarak tanımlanabilir.

Örneğin adres, tarih aralığı, koordinat, vb. gibi nesneler `Value Object` olarak tanmlanabilir.  

Öncelikle `ValueObject.cs` sınıfını tanımlayalım. `Value Object` nesneleri bu sınıfı implemente edecekler.

````c#
public abstract class ValueObject
{
    protected static bool EqualOperator(ValueObject left, ValueObject right)
    {
        if (ReferenceEquals(left, null) ^ ReferenceEquals(right, null))
        {
            return false;
        }
        return ReferenceEquals(left, null) || left.Equals(right);
    }

    protected static bool NotEqualOperator(ValueObject left, ValueObject right)
    {
        return !EqualOperator(left, right);
    }

    protected abstract IEnumerable<object> GetAtomicValues();

    public override bool Equals(object obj)
    {
        if (obj == null || obj.GetType() != GetType())
        {
            return false;
        }

        var other = (ValueObject)obj;
        var thisValues = GetAtomicValues().GetEnumerator();
        var otherValues = other.GetAtomicValues().GetEnumerator();

        while (thisValues.MoveNext() && otherValues.MoveNext())
        {
            if (ReferenceEquals(thisValues.Current, null) ^
                ReferenceEquals(otherValues.Current, null))
            {
                return false;
            }

            if (thisValues.Current != null &&
                !thisValues.Current.Equals(otherValues.Current))
            {
                return false;
            }
        }
        return !thisValues.MoveNext() && !otherValues.MoveNext();
    }

    public override int GetHashCode()
    {
        return GetAtomicValues()
            .Select(x => x != null ? x.GetHashCode() : 0)
            .Aggregate((x, y) => x ^ y);
    }
    // Other utility methods
}
````
   
Şimdide koordinat ve adres için `Value Object` nesnelerimizi tanımlayalım.

**Coordinates.cs**

````c#
public class Coordinates : ValueObject
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Latitude;
        yield return Longitude;
    }

    public override string ToString()
    {
        return $"{Latitude} - {Longitude}";
    }
}
````

**Address.cs**

```c#
public class Address : ValueObject
{
    public string Street { get; set; }
    public string City { get; set;}
    public string State { get; set;}
    public string Country { get; set;}
    public string ZipCode { get; set;}

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return Country;
        yield return ZipCode;
    }

    public override string ToString()
    {
        return $"{Street} - {ZipCode}, {City}, {State}, {Country}";
    }
}
````
    
Bu iki `Value Object` nesnesini kullanacak olan `Customer.cs` entity sınıfı da aşağıdaki gibi olacak.

````c#
public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public Address ShippingAddress { get; set; }
    public Address BillingAddress { get; set; }
    public Coordinates Coordinates { get; set; }
}
````
    
`Value Object` nesnelerinin nasıl çalıştığını görmek için, örnek bir `Customer` nesnesi oluşturup çıktılarına bakabiliriz.

````c#
class Program
{
    static void Main(string[] args)
    {
        var customer = new Customer
        {
            BillingAddress = new Address { Street = "b_street", City = "b_city", State = "b_state", Country = "b_country", ZipCode = "b_zipcode" },
            ShippingAddress = new Address { Street = "s_street", City = "s_city", State = "s_state", Country = "s_country", ZipCode = "s_zipcode" },
            Coordinates = new Coordinates { Latitude = new decimal(1233234.123), Longitude = new decimal(123234.345) },
            Name = "test customer",
            Id = Guid.NewGuid()
        };

        Console.WriteLine($"id              : {customer.Id}");
        Console.WriteLine($"name            : {customer.Name}");
        Console.WriteLine($"billing address : {customer.BillingAddress}");
        Console.WriteLine($"shipping address: {customer.ShippingAddress}");
        Console.WriteLine($"coordinates     : {customer.Coordinates}");
    }
}
````
 
**Çıktı:**

    // console output:
    id              : 05a7ce05-a456-4ec1-a9c1-8718cf7f6a5c
    name            : test customer
    billing address : b_street - b_zipcode, b_city, b_state, b_country
    shipping address: s_street - s_zipcode, s_city, s_state, s_country
    coordinates     : 1233234,123 - 123234,345

#### Entity Framework Ayarları
    
Bu nesnelerin `Customer` tablosunda birer alan olarak temsil edilebilemesi için, bazı ayarlar yapmamız gerekiyor.

**DbContext.cs**

````c#
public class EShopDbContext : DbContext
{
    public DbSet<Customer> Customers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(@"Server=localhost;Database=EShopDB;Trusted_Connection=True;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>(b =>
        {
            b.OwnsOne(x => x.Coordinates);
            b.OwnsOne(x => x.BillingAddress);
            b.OwnsOne(x => x.ShippingAddress);
        });
    }
}
````

`Value Object` nesnelerinin tanımı yukarıdaki gibi gayet basit. Bunun için `OwnsOne` metodunu kullanmak yeterli. 
Migration'ı çalıştırdığımızda aşağıdaki gibi bir migration dosyası oluşacak.

**MigrationName_Migrations.cs**

````c#
public partial class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(nullable: true),
                ShippingAddress_Street = table.Column<string>(nullable: true),
                ShippingAddress_City = table.Column<string>(nullable: true),
                ShippingAddress_State = table.Column<string>(nullable: true),
                ShippingAddress_Country = table.Column<string>(nullable: true),
                ShippingAddress_ZipCode = table.Column<string>(nullable: true),
                BillingAddress_Street = table.Column<string>(nullable: true),
                BillingAddress_City = table.Column<string>(nullable: true),
                BillingAddress_State = table.Column<string>(nullable: true),
                BillingAddress_Country = table.Column<string>(nullable: true),
                BillingAddress_ZipCode = table.Column<string>(nullable: true),
                Coordinates_Latitude = table.Column<decimal>(nullable: true),
                Coordinates_Longitude = table.Column<decimal>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Customers");
    }
}
````

`Value Object` nesnesinin alanları için, nesne adı ve alan adı şeklinde kolon isimleri oluştu. 
Aslında bu alan isimlerini de değiştirebiliriz. Örneğin;

````c#
...
b.OwnsOne(x => x.BillingAddress).Property(x=>x.City).HasColumnName("BillingCity");
b.OwnsOne(x => x.ShippingAddress).Property(x=>x.City).HasColumnName("ShippingCity");
...
````

Şeklinde bir değişiklik yaparsak, migration aşağıdaki gibi olur;

**MigrationName_Migrations.cs**

````c#
public partial class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(nullable: false),
                Name = table.Column<string>(nullable: true),
                ShippingAddress_Street = table.Column<string>(nullable: true),
                ShippingCity = table.Column<string>(nullable: true),
                ShippingAddress_State = table.Column<string>(nullable: true),
                ShippingAddress_Country = table.Column<string>(nullable: true),
                ShippingAddress_ZipCode = table.Column<string>(nullable: true),
                BillingAddress_Street = table.Column<string>(nullable: true),
                BillingCity = table.Column<string>(nullable: true),
                BillingAddress_State = table.Column<string>(nullable: true),
                BillingAddress_Country = table.Column<string>(nullable: true),
                BillingAddress_ZipCode = table.Column<string>(nullable: true),
                Coordinates_Latitude = table.Column<decimal>(nullable: true),
                Coordinates_Longitude = table.Column<decimal>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Customers");
    }
}
````

Gördüğünüz gibi, `City` için yaptığımız kolon isimleri, belirlediğimiz isimle oluştururuldu. 

### Kaynaklar
    
- [Implement value objects](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects)
- [ValueObject](https://martinfowler.com/bliki/ValueObject.html)

