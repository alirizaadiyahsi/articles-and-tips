ASP.NET Core projelerinizde, yetkilendirmeleri `Policy` ler ile yapabiliriz. 
Örneğin izinleri (`Permissions`) policy olarak tanımlayıp, her isteği bu izinlere göre yetkilendirebiliriz.

İzinleri aşağıdaki gibi tanımlayabiliriz.

**Permissions.cs**

````c#
public static class Permissions
{
    public static List<string> GetAll()
    {
        return new List<string>
        {
            Users.Read, Users.Create, Users.Update, Users.Delete,
            Roles.Read, Roles.Create, Roles.Update, Roles.Delete
        };
    }

    public static class Users
    {
        public const string Read = "Permissions.Users.Read";
        public const string Create = "Permissions.Users.Create";
        public const string Update = "Permissions.Users.Update";
        public const string Delete = "Permissions.Users.Delete";
    }

    public static class Roles
    {
        public const string Read = "Permissions.Roles.Read";
        public const string Create = "Permissions.Roles.Create";
        public const string Update = "Permissions.Roles.Update";
        public const string Delete = "Permissions.Roles.Delete";
    }
}
````

İzinleri bir liste olarak değilde, sabit(constants) olarak tanımladık, çünkü aynı zamanda bunları, `AuthorizeAttribute` içinde parametre olarak verebilelim diye. `AuthorizeAttribute` sadece `const` tipinde değişkenleri kabul ediyor.

İzinleri `Policy` olarak eklemek için, `IAuthorizationRequirement` sınıfından kalıtım alan, `PermissionRequirement.cs` tipinde bir sınıf ekliyoruz. 

**PermissionRequirement.cs**

````c#
public class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}
````

ASP.NET Core yetkilendirme olaylarını tekip eden bir servis sağlıyor, `IAuthorizationHandler`. Biz de izinleri(permissions) yakalamak için bu `AuthorizationHandler` sınıfından türettiğimiz `PermissionHandler.cs` sınıfını kullanacağız.

**PermissionHandler.cs**

````c#
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionAppService _permissionAppService;

    public PermissionHandler(IPermissionAppService permissionAppService)
    {
        _permissionAppService = permissionAppService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User == null || !context.User.Identity.IsAuthenticated)
        {
            context.Fail();
            return;
        }

        // Kullanıcının ilgili role sahip olup olmadığı kontrol ediliyor.
        var hasPermission = await _permissionAppService.IsUserGrantedToPermissionAsync(context.User.Identity.Name, requirement.Permission);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
````

Tabiki yukarıda eklediğimiz sınıfları, `Startup.cs` içinde ayarlarını yapıp, DI Container'a register etmemiz gerekiyor.
Ayrıca `Permissions.cs` içinde tanımladığımız statik izinleride, policy listesine eklememiz gerekiyor.

**Startup.cs**

````c#
public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        ...
        
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.GetAll())
            {
                // İzinleri policy listesine ekliyoruz
                options.AddPolicy(permission,
                    policy => policy.Requirements.Add(new PermissionRequirement(permission)));
            }
        });

        ...
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
````

Artık policy tabanlı yetkilendirmeyi, controller sınıfı veya metodlarında kullanabiliriz. Tek yapmak gereken, sınıfın veya metodun başına `[Authorize(Permissions....)]` şeklinde tanımlama yapmak.

**RolesController.cs**

````c#
public class RolesController: ControllerBase
{
    private readonly IRoleAppService _roleAppService;

    public RolesController(IRoleAppService roleAppService)
    {
        _roleAppService = roleAppService;
    }

    [HttpGet]
    [Authorize(Permissions.Roles.Read)]
    public async Task<ActionResult<RoleOutput>> GetRoles(Guid id)
    {
        var role = await _roleAppService.GetAsync(id);
        if (role == null) return NotFound(UserFriendlyMessages.EntityNotFound);

        return Ok(role);
    }

    [HttpGet]
    [Authorize(Permissions.Roles.Read)]
    public async Task<ActionResult<IPagedListResult<RoleListOutput>>> GetRoles(PagedListInput input)
    {
        var roles = await _roleAppService.GetListAsync(input);

        return Ok(roles);
    }

    [HttpPost]
    [Authorize(Permissions.Roles.Create)]
    public async Task<ActionResult<RoleOutput>> PostRoles(CreateRoleInput input)
    {
        var role = await _authorizationAppService.FindRoleByNameAsync(input.Name);
        if (role != null) return Conflict(UserFriendlyMessages.RoleNameAlreadyExist);

        var roleOutput = await _roleAppService.CreateAsync(input);

        return Ok(roleOutput);
    }

    [HttpPut]
    [Authorize(Permissions.Roles.Update)]
    public async Task<ActionResult<RoleOutput>> PutRoles(UpdateRoleInput input)
    {
        var role = await _authorizationAppService.FindRoleByNameAsync(input.Name);
        if (role != null) return Conflict(UserFriendlyMessages.RoleNameAlreadyExist);

        var roleOutput = _roleAppService.Update(input);

        return Ok(roleOutput);
    }

    [HttpDelete]
    [Authorize(Permissions.Roles.Delete)]
    public async Task<ActionResult<RoleOutput>> DeleteRoles(Guid id)
    {
        var roleOutput = await _roleAppService.DeleteAsync(id);

        return Ok(roleOutput);
    }
}
````



