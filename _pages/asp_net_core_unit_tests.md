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

### UserManager ve RoleManager kullanan sınıfların test edilmesi

ASP.NET Core'da tanımlı olan `UserManager` ve `RoleManager` sınıflarını kullanan kendi sınıflarımızı (örneğin, application-service, repository gibi) nasıl test edebileceğimize bir bakalım. 





