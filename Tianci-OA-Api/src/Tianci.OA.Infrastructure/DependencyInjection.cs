using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Contracts;
using Tianci.OA.Application.Modules.Audit;
using Tianci.OA.Application.Modules.Employees;
using Tianci.OA.Application.Modules.Files;
using Tianci.OA.Application.Modules.Identity;
using Tianci.OA.Application.Modules.Organization;
using Tianci.OA.Application.Modules.Recruitment;
using Tianci.OA.Application.Modules.Workflows;
using Tianci.OA.Domain.Audit;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Files;
using Tianci.OA.Domain.Identity;
using Tianci.OA.Domain.Organization;
using Tianci.OA.Domain.Recruitment;
using Tianci.OA.Domain.Workflows;
using Tianci.OA.Infrastructure.Audit;
using Tianci.OA.Infrastructure.Authorization;
using Tianci.OA.Infrastructure.Caching;
using Tianci.OA.Infrastructure.Files;
using Tianci.OA.Infrastructure.Persistence;
using Tianci.OA.Infrastructure.Security;

namespace Tianci.OA.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MySql") ?? throw new InvalidOperationException("缺少 ConnectionStrings:MySql");
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>() ?? throw new InvalidOperationException("缺少 Jwt 配置");
        if (Encoding.UTF8.GetByteCount(jwt.Secret) < 32) throw new InvalidOperationException("Jwt:Secret 必须至少 32 字节，并通过环境变量覆盖");
        services.Configure<FileStorageOptions>(configuration.GetSection("FileStorage"));
        var keyPath = Path.GetFullPath(configuration["DataProtection:KeyPath"] ?? "App_Data/keys");
        Directory.CreateDirectory(keyPath);
        services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyPath)).SetApplicationName("Tianci.OA");
        services.AddHttpContextAccessor();
        services.AddAutoMapper(cfg => { }, typeof(MappingProfile));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISnowflakeIdGenerator>(_ => new SnowflakeIdGenerator(configuration.GetValue("Snowflake:NodeId", 1)));
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ISensitiveDataProtector, SensitiveDataProtector>();
        services.AddSingleton<Tianci.OA.Application.Abstractions.ICacheService>(_ => new RedisCacheService(configuration.GetConnectionString("Redis")));
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ITokenIssuer, TokenIssuer>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuditWriter, AuditWriter>();
        services.AddScoped(typeof(IRepository<>), typeof(SqlSugarRepository<>));
        services.AddScoped<IUnitOfWork, SqlSugarUnitOfWork>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IInterviewerQuery, InterviewerQuery>();
        services.AddScoped<IRecruitmentService, RecruitmentService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<ISqlSugarClient>(_ => CreateClient(connectionString));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var idText = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var stamp = context.Principal?.FindFirst("security_stamp")?.Value;
                    if (!long.TryParse(idText, out var id)) { context.Fail("无效用户标识"); return; }
                    var repository = context.HttpContext.RequestServices.GetRequiredService<IRepository<SysUser>>();
                    var user = await repository.FirstAsync(x => x.Id == id && !x.IsDeleted);
                    if (user is null || user.Status != Tianci.OA.Domain.Common.UserStatus.Enabled || user.SecurityStamp != stamp) context.Fail("会话已失效");
                }
            };
        });
        services.AddAuthorization();
        return services;
    }

    private static ISqlSugarClient CreateClient(string connectionString)
    {
        var client = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = connectionString, DbType = DbType.MySql, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
            MoreSettings = new ConnMoreSettings { IsAutoRemoveDataCache = true },
            ConfigureExternalServices = new ConfigureExternalServices
            {
                EntityNameService = (type, info) => info.DbTableName = TableName(type),
                EntityService = (property, column) =>
                {
                    column.DbColumnName = UtilMethods.ToUnderLine(property.Name);
                    if (property.Name == "Id") column.IsPrimarykey = true;
                }
            }
        });
        client.Aop.OnLogExecuting = (sql, parameters) => { };
        return client;
    }

    private static string TableName(Type type) => type.Name switch
    {
        nameof(SysUser) => "sys_user", nameof(SysRole) => "sys_role", nameof(SysMenu) => "sys_menu", nameof(SysUserRole) => "sys_user_role", nameof(SysRoleMenu) => "sys_role_menu",
        nameof(Department) => "department", nameof(Position) => "position", nameof(Employee) => "employee", nameof(EmployeeEntry) => "employee_entry",
        nameof(Resume) => "resume", nameof(InterviewRecord) => "interview_record", nameof(EmployeeContract) => "employee_contract", nameof(SysFile) => "sys_file",
        nameof(OperationLog) => "operation_log", nameof(WorkflowInstance) => "workflow_instance", nameof(WorkflowNode) => "workflow_node", nameof(WorkflowRecord) => "workflow_record",
        _ => UtilMethods.ToUnderLine(type.Name)
    };
}
