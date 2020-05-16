## ASP.NET Core Existing Database Code First Migrations

Var olan veritabanında, EF Core Code First olarak ilerlemek mümkün. 
Bunun için var olan veritabanı tablolarını, uygulama tarafında nesnelerle eşleştirmek gerekiyor.
Tabi var olan veritabanında belki yüzlerce tablo olabilir. 
Bunları tek tek sınıf olarak yazmak yerine, `Scaffold-DbContext` komutunu kullanarak, otomatik olarak üreteceğiz.

### Gerekli NuGet paketlerinin yüklenmesi

İlk olarak eğer; 
  - veritabanı MySql ise `MySql.Data.EntityFrameworkCore` paketini
  - veritabanı MsSql ise `Microsoft.EntityFrameworkCore.SqlServer` paketini ilgili projeye ekliyoruz.

Her iki durumda için de `Microsoft.EntityFrameworkCore.Tools` paketini ilgili projeye ekliyoruz.

### Scaffold Komutu

Bizim örneğimizde MsSql bir veritabanımız var ve MsSql privider paketini kullanacağız. 
Örnek veritabanında sadece `Student` adında bir tablo var. 

Aşağıdaki komutu (Package Manager Console'dan ve MsSql veritabanı için) çalıştırdığımız zaman;

````
Scaffold-DbContext "Server=localhost;Database=ExistingDBSample;Trusted_Connection=True;" Microsoft.EntityFrameworkCore.SqlServer -OutputDir Models
````

Models klasörü altında `DbContext` sınıfı ve `Student` modeli oluşacak. Oluşan sınıflar şöyle.

#### ExistingDBSampleContext.cs

````c#
public partial class ExistingDBSampleContext : DbContext
{
    public ExistingDBSampleContext()
    {
    }

    public ExistingDBSampleContext(DbContextOptions<ExistingDBSampleContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Student> Student { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. See http://go.microsoft.com/fwlink/?LinkId=723263 for guidance on storing connection strings.
            optionsBuilder.UseSqlServer("Server=localhost;Database=ExistingDBSample;Trusted_Connection=True;");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
````

#### Student.cs

````c#
 public partial class Student
{
    public int Id { get; set; }
    public string Name { get; set; }
}
````

Veritabanı modelleri ve context artık hazır. Artık her güncellemeden sonra `add-migration` ve `update-database` komutlarını çalıştırarak veritabanını güncel tutabiliriz.

### Problem

Yalnız burada bir problem var. Şimdi eğer `add-migration` komutunu çalıştırırsak aşağıdaki gibi bir migration dosyası oluşacak.

#### InitialMigration.cs

````c#
public partial class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Student",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Student", x => x.Id);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Student");
    }
}
````

Bu şekildeki `update-database` komutunu çalıştırırsak, zaten `Student` adında bir tablo olduğuna dair bir hata mesajı alırız. 
Yani aslında var olan tüm tabloların migration bilgileri bu migration dosyasında oluşuyor.
Bundan dolayı, bu migration'ı uygulamadan önce `Up` metodu içindeki tüm kod satırlarını silmek gerekiyor. Yani migration dosyası aşağıdaki gibi olmalı.

#### InitialMigration.cs

````c#
public partial class InitialMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Student");
    }
}
````

Şimdi artık `update-database` komutunu çalıştırabiliriz. Bu komuttan sonra `__EFMigrationsHistory` tablosu, veritabanına eklenecek.
Bundan sonra artık klasik code-first yapısı ile ilerleyebilirsiniz.


