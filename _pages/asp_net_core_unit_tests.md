### ASP.NET Core Birim Testler - Örnekler (ASP.NET Core Unit Tests wiht xUnit and Moq)

### Controller Testleri

Aşağıdaki gibi bir controller sınıfımız olsun;

**AccountController.cs**

````c#
public class AccountController : ApiControllerBase
{
    private readonly IAuthorizationAppService _authorizationAppService;
    private readonly JwtTokenConfiguration _jwtTokenConfiguration;
    private readonly IConfiguration _configuration;
    private readonly IEmailSender _emailSender;

    public AccountController(
        IAuthorizationAppService authorizationAppService,
        IOptions<JwtTokenConfiguration> jwtTokenConfiguration,
        IConfiguration configuration,
        IEmailSender emailSender)
    {
        _authorizationAppService = authorizationAppService;
        _configuration = configuration;
        _emailSender = emailSender;
        _jwtTokenConfiguration = jwtTokenConfiguration.Value;
    }
    
    ...
    
    [HttpPost("/api/[action]")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordInput input)
    {
        if (input.NewPassword != input.PasswordRepeat)
        {
            return BadRequest(UserFriendlyMessages.PasswordsAreNotMatched);
        }

        var userOutput = await _authorizationAppService.FindUserByUserNameAsync(User.Identity.Name);
        var result = await _authorizationAppService.ChangePasswordAsync(userOutput, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded) return BadRequest(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));

        return Ok();
    }
    
    ...
}
````

Burada test için sadece `ChangePassword` metodunu seçtim. Çünkü gerekli olan tüm test kombinasyonları içeriyor. 
Mesela bunlardan birisi de ContextUser (`User.Identity.Name`) nesnesini kullanıyor. 
Bu controller sınıfı içinde tabiki başka metodlar da var ve sınıf içerinde private olarak tanımladığım alanları, bu controller metodları da kullanıyor.
Testlerimizde bu private alanları `Constructor` a nasıl vereceğimizide göstereceğim.

**AccountControllerTests.cs**

````c#
public class AccountControllerTests : WebApiTestBase
{
    private readonly UserOutput _testUserOutput;
    private readonly IOptions<JwtTokenConfiguration> _jwtTokenConfiguration = Options.Create(new JwtTokenConfiguration());
    private readonly Mock<IConfiguration> _configurationMock = SetupMockConfiguration();
    private readonly Mock<IEmailSender> _emailSenderMock = new Mock<IEmailSender>();

    public AccountControllerTests()
    {
        _testUserOutput = new UserOutput
        {
            Id = Guid.NewGuid(),
            UserName = "test_userName",
            Email = "test_userName@mail.com"
        };
    }
    
    ...
    
    [Fact]
    public async Task Should_Change_Password_Async()
    {
        var authorizationAppServiceMock = new Mock<IAuthorizationAppService>();
        authorizationAppServiceMock.Setup(x => x.FindUserByUserNameAsync(It.IsAny<string>())).ReturnsAsync(_testUserOutput);
        authorizationAppServiceMock.Setup(x => x.ChangePasswordAsync(It.IsAny<UserOutput>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);

        var accountController = new AccountController(
            authorizationAppServiceMock.Object,
            _jwtTokenConfiguration,
            _configurationMock.Object,
            _emailSenderMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.Name, _testUserOutput.UserName) }, "TestAuthTypeName"))
                }
            }
        };

        var actionResult = await accountController.ChangePassword(new ChangePasswordInput
        {
            CurrentPassword = "123qwe",
            NewPassword = "123qwe123qwe",
            PasswordRepeat = "123qwe123qwe"
        });

        var okResult = Assert.IsType<OkResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.OK, okResult.StatusCode);
    }

    private static Mock<IConfiguration> SetupMockConfiguration()
    {
        var configurationMock = new Mock<IConfiguration>();
        configurationMock.Setup(x => x["App:ClientUrl"]).Returns("http://localhost:8080");

        return configurationMock;
    }
    
    ...
}
````

Controller sınıfının constructor içinde aldığı parametlerin hepsini Mock nesne olarak oluşturuyoruz.
Bazı Mock nesneler için Mock ayarı yapıyoruz ama bazıları için gerek yok. Sadece `AccountController` sınıfının örneğini oluşturmak için kullanıyoruz.

Burada bizim için önemli olan iki kısım var. 

**Birincisi**, `ApplicationService` sınıfının Mock olarak oluşturulması ve kullanılacak olan metodların ayarlarının yapılması.

````c#
var authorizationAppServiceMock = new Mock<IAuthorizationAppService>();
authorizationAppServiceMock.Setup(x => x.FindUserByUserNameAsync(It.IsAny<string>())).ReturnsAsync(_testUserOutput);
authorizationAppServiceMock.Setup(x => x.ChangePasswordAsync(It.IsAny<UserOutput>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(IdentityResult.Success);
````

Sadece `FindUserByUserNameAsync` ve `ChangePasswordAsync` metodlarını kullandığımız için sadece bu metodların Mock ayarlarını yapıyoruz.

**İkincisi** ise,  `ChangePassword` metodu içinde kullanılan `ContextUser` nesnesinin de controller için tanımlanması.

````c#
var accountController = new AccountController(
    authorizationAppServiceMock.Object,
    _jwtTokenConfiguration,
    _configurationMock.Object,
    _emailSenderMock.Object)
{
    ControllerContext = new ControllerContext
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.Name, _testUserOutput.UserName) }, "TestAuthTypeName"))
        }
    }
};
````

`HttpContext.User` tanımı yaparak, controller sınıfının o an login olan kullanıcıyı tanımasını sağlıyoruz. 

### UserManager ve RoleManager Kullanan Sınıfların Test Edilmesi

ASP.NET Core'da tanımlı olan `UserManager` ve `RoleManager` sınıflarını kullanan kendi sınıflarımızı (örneğin, application-service, repository gibi) nasıl test edebileceğimize bir bakalım. 

Bu test için `PermissionAppService` adında bir application-service sınıfını test edeceğiz.

**PermissionAppService.cs**

````c#
public class PermissionAppService : IPermissionAppService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public PermissionAppService(UserManager<User> userManager, RoleManager<Role> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<bool> IsUserGrantedToPermissionAsync(string userName, string permission)
    {
        var user = await _userManager.FindByNameAsync(userName);
        var userClaims = await _userManager.GetClaimsAsync(user);
        if (userClaims.Any(x => x.Type == CustomClaimTypes.Permission && x.Value == permission))
        {
            return true;
        }

        var userRoles = user.UserRoles.Select(ur => ur.Role);
        foreach (var role in userRoles)
        {
            var roleClaims = await _roleManager.GetClaimsAsync(role);
            if (roleClaims.Any(x => x.Type == CustomClaimTypes.Permission && x.Value == permission))
            {
                return true;
            }
        }

        return false;
    }
}
````

Bu servis, kullanıcı adını ve kullanıcı iznini (permission) alarak, kullanıcının bu izne yetkisi olup olmadığını dönüyor. Yazacağımız test de bir iznin kullanıcıya veya kullanıcının bağlı olduğu role ait olup olmadığına göre bir sonuç dönecek. Ayrıca başarısız senaryolarıda test ediyoruz.

**PermissionAppServiceTests.cs**

````c#
public class PermissionAppServiceTests : AppServiceTestBase
{
    private readonly IPermissionAppService _permissionAppService;
    private static readonly string TestPermissionClaimForUser = "TestPermissionClaimForUser";
    private static readonly string TestPermissionClaimForRole = "TestPermissionClaimForRoe";
    private readonly User _testUser;
    private readonly Role _testRole;

    public PermissionAppServiceTests()
    {
        _testRole = new Role
        {
            Name = roleName
        };
        
        _testUser = new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            Email = email,
            IsDeleted = false,
            EmailConfirmed = true,
            NormalizedEmail = email.ToUpper(CultureInfo.GetCultureInfo("en-US")),
            NormalizedUserName = userName.ToUpper(CultureInfo.GetCultureInfo("en-US")),
            PasswordHash = Guid.NewGuid().ToString(),
            PhoneNumberConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        
        var testUserRole = new UserRole
        {
            User = testUser,
            Role = testRole
        };

        testUser.UserRoles.Add(testUserRole);
        testRole.UserRoles.Add(testUserRole);

        var userManagerMock = SetupUserManagerMock();
        var roleManagerMock = SetupRoleManagerMock();

        _permissionAppService = new PermissionAppService(userManagerMock.Object, roleManagerMock.Object);
    }

    [Fact]
    public async Task Should_Permission_Granted_To_User()
    {
        var isPermissionGranted =
            await _permissionAppService.IsUserGrantedToPermissionAsync(_testUser.UserName, TestPermissionClaimForUser);

        Assert.True(isPermissionGranted);
    }

    [Fact]
    public async Task Should_Permission_Granted_To_User_Role()
    {
        var isPermissionGranted =
            await _permissionAppService.IsUserGrantedToPermissionAsync(_testUser.UserName, TestPermissionClaimForRole);

        Assert.True(isPermissionGranted);
    }

    [Fact]
    public async Task Should_Not_Permission_Granted_To_User()
    {
        var isPermissionNotGranted =
            await _permissionAppService.IsUserGrantedToPermissionAsync(_testUser.UserName, "NotGrantedPermissionClaim");

        Assert.False(isPermissionNotGranted);
    }

    private Mock<UserManager<User>> SetupUserManagerMock()
    {
        var mockUserManager = new Mock<UserManager<User>>(new Mock<IUserStore<User>>().Object, null, null, null, null, null,
            null, null, null);
        mockUserManager.Setup(x => x.FindByNameAsync(_testUser.UserName)).ReturnsAsync(_testUser);
        mockUserManager.Setup(x => x.GetClaimsAsync(_testUser)).ReturnsAsync(
            new List<Claim>
            {
                new Claim(CustomClaimTypes.Permission, TestPermissionClaimForUser)
            });
        return mockUserManager;
    }

    private Mock<RoleManager<Role>> SetupRoleManagerMock()
    {
        var mockRoleManager = new Mock<RoleManager<Role>>(new Mock<IRoleStore<Role>>().Object, null, null, null, null);
        mockRoleManager.Setup(x => x.GetClaimsAsync(_testRole)).ReturnsAsync(
            new List<Claim>
            {
                new Claim(CustomClaimTypes.Permission, TestPermissionClaimForRole)
            });
        return mockRoleManager;
    }
}
````

`PermissionAppService` sınıfından bir örnek oluşturabilmek için, constructor'a geçmek gereken parametreleri Mock olarak oluşturuyoruz.
Bize gereken `UserManager` ve `RoleManager` sınıflarını Mock olarak oluşturmak. Tabiki bu sınıflar da, constructor metodlarında bazı parametreler alıyor. Bunlardan birisi `IUserStore` ve bu parametre null olamaz. Bundan dolayıda user-store nesnesini de parametre olarak vermek gerekiyor.

Daha sonra da `UserManager` sınıfında kullanmak istediğimiz metodları `Mock.Setup` ile ayarlıyoruz. 

````c#
var mockUserManager = new Mock<UserManager<User>>(new Mock<IUserStore<User>>().Object, null, null, null, null, null,
            null, null, null);
mockUserManager.Setup(x => x.FindByNameAsync(_testUser.UserName)).ReturnsAsync(_testUser);
mockUserManager.Setup(x => x.GetClaimsAsync(_testUser)).ReturnsAsync(
    new List<Claim>
    {
        new Claim(CustomClaimTypes.Permission, TestPermissionClaimForUser)
    });
````

Yukarıda test olarak verdiğimiz bir parametreye karşılık Mock metodlar yine test verileri dönüyorlar. 
