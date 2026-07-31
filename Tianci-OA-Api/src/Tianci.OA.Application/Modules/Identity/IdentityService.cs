using AutoMapper;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Application.Common;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Identity;

namespace Tianci.OA.Application.Modules.Identity;

public interface IIdentityService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task InitializeAdminAsync(InitializeAdminRequest request, CancellationToken ct);
    Task<PagedResult<UserDto>> GetUsersAsync(PageRequest page, string? keyword, CancellationToken ct);
    Task<UserDto> GetUserAsync(string id, CancellationToken ct);
    Task<UserDto> CreateUserAsync(UserCreateRequest request, CancellationToken ct);
    Task<UserDto> UpdateUserAsync(string id, UserUpdateRequest request, CancellationToken ct);
    Task ResetPasswordAsync(string id, ResetPasswordRequest request, CancellationToken ct);
    Task AssignUserRolesAsync(string id, AssignIdsRequest request, CancellationToken ct);
    Task DeleteUserAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct);
    Task<RoleDto> CreateRoleAsync(RoleUpsertRequest request, CancellationToken ct);
    Task<RoleDto> UpdateRoleAsync(string id, RoleUpsertRequest request, CancellationToken ct);
    Task AssignRoleMenusAsync(string id, AssignIdsRequest request, CancellationToken ct);
    Task DeleteRoleAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<MenuDto>> GetMenusAsync(CancellationToken ct);
    Task<MenuDto> CreateMenuAsync(MenuUpsertRequest request, CancellationToken ct);
    Task<MenuDto> UpdateMenuAsync(string id, MenuUpsertRequest request, CancellationToken ct);
    Task DeleteMenuAsync(string id, CancellationToken ct);
}

public sealed class IdentityService(
    IRepository<SysUser> users,
    IRepository<SysRole> roles,
    IRepository<SysMenu> menus,
    IRepository<SysUserRole> userRoles,
    IRepository<SysRoleMenu> roleMenus,
    IPasswordService passwords,
    ITokenIssuer tokens,
    IPermissionService permissions,
    ISnowflakeIdGenerator ids,
    IClock clock,
    ICurrentUser currentUser,
    IUnitOfWork uow,
    IMapper mapper) : IIdentityService
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FirstAsync(x => x.Username == request.Username && !x.IsDeleted, ct)
            ?? throw new BusinessException("用户名或密码错误", "INVALID_CREDENTIALS", 401);
        if (user.Status != UserStatus.Enabled || user.RequiresInitialization || string.IsNullOrEmpty(user.PasswordHash) || !passwords.Verify(user.Username, user.PasswordHash, request.Password))
            throw new BusinessException("用户名或密码错误", "INVALID_CREDENTIALS", 401);
        user.LastLoginAt = clock.UtcNow;
        EntityAudit.Update(user, clock, currentUser);
        await users.UpdateAsync(user, ct);
        var token = tokens.Issue(user.Id, user.Username, user.DisplayName, user.SecurityStamp);
        return new LoginResponse(token.AccessToken, token.ExpiresAtUtc, mapper.Map<UserDto>(user), await permissions.GetPermissionsAsync(user.Id));
    }

    public async Task InitializeAdminAsync(InitializeAdminRequest request, CancellationToken ct)
    {
        EnsureStrongPassword(request.Password);
        var admin = await users.FirstAsync(x => x.Username == "admin" && !x.IsDeleted, ct) ?? throw new NotFoundException("初始化管理员不存在");
        if (!admin.RequiresInitialization) throw new ConflictException("管理员已完成初始化", "ALREADY_INITIALIZED");
        await uow.BeginAsync();
        try
        {
            admin.PasswordHash = passwords.Hash(admin.Username, request.Password);
            admin.SecurityStamp = Guid.NewGuid().ToString("N");
            admin.RequiresInitialization = false;
            admin.Status = UserStatus.Enabled;
            EntityAudit.Update(admin, clock, currentUser);
            await users.UpdateAsync(admin, ct);
            await uow.CommitAsync();
        }
        catch { await uow.RollbackAsync(); throw; }
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(PageRequest page, string? keyword, CancellationToken ct)
    {
        keyword = keyword?.Trim() ?? "";
        var result = await users.PageAsync(x => !x.IsDeleted && (keyword == "" || x.Username.Contains(keyword) || x.DisplayName.Contains(keyword)), page.SafePageNumber, page.SafePageSize, x => x.UpdatedAt, true, ct);
        return new(mapper.Map<IReadOnlyList<UserDto>>(result.Items), page.SafePageNumber, page.SafePageSize, result.Total);
    }

    public async Task<UserDto> GetUserAsync(string id, CancellationToken ct) => mapper.Map<UserDto>(await GetRequiredAsync<SysUser>(users, IdParser.Parse(id), "用户不存在", ct));

    public async Task<UserDto> CreateUserAsync(UserCreateRequest request, CancellationToken ct)
    {
        EnsureStrongPassword(request.Password);
        if (await users.ExistsAsync(x => x.Username == request.Username && !x.IsDeleted, ct)) throw new ConflictException("用户名已存在", "USERNAME_EXISTS");
        var user = new SysUser
        {
            Username = request.Username.Trim(), DisplayName = request.DisplayName.Trim(), Phone = request.Phone, Email = request.Email,
            EmployeeId = IdParser.ParseNullable(request.EmployeeId, "employeeId"), DepartmentId = IdParser.ParseNullable(request.DepartmentId, "departmentId"),
            Status = UserStatus.Enabled, RequiresInitialization = false, SecurityStamp = Guid.NewGuid().ToString("N")
        };
        user.PasswordHash = passwords.Hash(user.Username, request.Password);
        EntityAudit.Create(user, ids, clock, currentUser);
        await uow.BeginAsync();
        try
        {
            await users.InsertAsync(user, ct);
            await ReplaceUserRolesAsync(user.Id, request.RoleIds, ct);
            await uow.CommitAsync();
        }
        catch { await uow.RollbackAsync(); throw; }
        return mapper.Map<UserDto>(user);
    }

    public async Task<UserDto> UpdateUserAsync(string id, UserUpdateRequest request, CancellationToken ct)
    {
        var user = await GetRequiredAsync<SysUser>(users, IdParser.Parse(id), "用户不存在", ct);
        user.DisplayName = request.DisplayName.Trim(); user.Phone = request.Phone; user.Email = request.Email;
        user.EmployeeId = IdParser.ParseNullable(request.EmployeeId, "employeeId"); user.DepartmentId = IdParser.ParseNullable(request.DepartmentId, "departmentId");
        if (user.Status != request.Status) user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.Status = request.Status; EntityAudit.Update(user, clock, currentUser);
        await users.UpdateAsync(user, ct);
        return mapper.Map<UserDto>(user);
    }

    public async Task ResetPasswordAsync(string id, ResetPasswordRequest request, CancellationToken ct)
    {
        EnsureStrongPassword(request.NewPassword);
        var user = await GetRequiredAsync<SysUser>(users, IdParser.Parse(id), "用户不存在", ct);
        user.PasswordHash = passwords.Hash(user.Username, request.NewPassword); user.SecurityStamp = Guid.NewGuid().ToString("N"); user.RequiresInitialization = false;
        EntityAudit.Update(user, clock, currentUser); await users.UpdateAsync(user, ct);
    }

    public async Task AssignUserRolesAsync(string id, AssignIdsRequest request, CancellationToken ct)
    {
        var userId = IdParser.Parse(id); _ = await GetRequiredAsync<SysUser>(users, userId, "用户不存在", ct);
        await uow.BeginAsync();
        try { await ReplaceUserRolesAsync(userId, request.Ids, ct); var user = await users.GetByIdAsync(userId, ct); user!.SecurityStamp = Guid.NewGuid().ToString("N"); await users.UpdateAsync(user, ct); await uow.CommitAsync(); }
        catch { await uow.RollbackAsync(); throw; }
    }

    public async Task DeleteUserAsync(string id, CancellationToken ct)
    {
        var user = await GetRequiredAsync<SysUser>(users, IdParser.Parse(id), "用户不存在", ct);
        if (user.Username == "admin") throw new ConflictException("内置管理员不可删除");
        user.IsDeleted = true; user.DeletedAt = clock.UtcNow; user.DeletedBy = currentUser.UserId; user.Status = UserStatus.Disabled; user.SecurityStamp = Guid.NewGuid().ToString("N");
        await users.UpdateAsync(user, ct);
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct) => mapper.Map<IReadOnlyList<RoleDto>>(await roles.ListAsync(x => !x.IsDeleted, ct));

    public async Task<RoleDto> CreateRoleAsync(RoleUpsertRequest request, CancellationToken ct)
    {
        if (await roles.ExistsAsync(x => x.Code == request.Code && !x.IsDeleted, ct)) throw new ConflictException("角色编码已存在");
        var entity = new SysRole { Name = request.Name.Trim(), Code = request.Code.Trim(), DataScope = request.DataScope, Status = request.Status, Remark = request.Remark };
        EntityAudit.Create(entity, ids, clock, currentUser); await roles.InsertAsync(entity, ct); return mapper.Map<RoleDto>(entity);
    }

    public async Task<RoleDto> UpdateRoleAsync(string id, RoleUpsertRequest request, CancellationToken ct)
    {
        var role = await GetRequiredAsync<SysRole>(roles, IdParser.Parse(id), "角色不存在", ct);
        if (role.IsSystem && request.Status == EnabledStatus.Disabled) throw new ConflictException("系统角色不可停用");
        role.Name = request.Name.Trim(); role.Code = request.Code.Trim(); role.DataScope = request.DataScope; role.Status = request.Status; role.Remark = request.Remark;
        EntityAudit.Update(role, clock, currentUser); await roles.UpdateAsync(role, ct); return mapper.Map<RoleDto>(role);
    }

    public async Task AssignRoleMenusAsync(string id, AssignIdsRequest request, CancellationToken ct)
    {
        var roleId = IdParser.Parse(id); _ = await GetRequiredAsync<SysRole>(roles, roleId, "角色不存在", ct);
        var menuIds = request.Ids.Select(x => IdParser.Parse(x, "menuId")).Distinct().ToArray();
        foreach (var menuId in menuIds) if (!await menus.ExistsAsync(x => x.Id == menuId && !x.IsDeleted, ct)) throw new NotFoundException($"菜单 {menuId} 不存在");
        await uow.BeginAsync();
        try
        {
            await roleMenus.DeleteWhereAsync(x => x.RoleId == roleId, ct);
            await roleMenus.InsertRangeAsync(menuIds.Select(x => new SysRoleMenu { Id = ids.NextId(), RoleId = roleId, MenuId = x, CreatedAt = clock.UtcNow, CreatedBy = currentUser.UserId }), ct);
            await uow.CommitAsync();
        }
        catch { await uow.RollbackAsync(); throw; }
    }

    public async Task DeleteRoleAsync(string id, CancellationToken ct)
    {
        var role = await GetRequiredAsync<SysRole>(roles, IdParser.Parse(id), "角色不存在", ct);
        if (role.IsSystem) throw new ConflictException("系统角色不可删除");
        role.IsDeleted = true; role.DeletedAt = clock.UtcNow; role.DeletedBy = currentUser.UserId; await roles.UpdateAsync(role, ct);
    }

    public async Task<IReadOnlyList<MenuDto>> GetMenusAsync(CancellationToken ct) => mapper.Map<IReadOnlyList<MenuDto>>(await menus.ListAsync(x => !x.IsDeleted, ct));

    public async Task<MenuDto> CreateMenuAsync(MenuUpsertRequest request, CancellationToken ct)
    {
        var entity = CreateMenu(request); EntityAudit.Create(entity, ids, clock, currentUser); await menus.InsertAsync(entity, ct); return mapper.Map<MenuDto>(entity);
    }

    public async Task<MenuDto> UpdateMenuAsync(string id, MenuUpsertRequest request, CancellationToken ct)
    {
        var menu = await GetRequiredAsync<SysMenu>(menus, IdParser.Parse(id), "菜单不存在", ct);
        if (request.ParentId == id) throw new BusinessException("菜单不能以自身为父级");
        ApplyMenu(menu, request); EntityAudit.Update(menu, clock, currentUser); await menus.UpdateAsync(menu, ct); return mapper.Map<MenuDto>(menu);
    }

    public async Task DeleteMenuAsync(string id, CancellationToken ct)
    {
        var menu = await GetRequiredAsync<SysMenu>(menus, IdParser.Parse(id), "菜单不存在", ct);
        if (await menus.ExistsAsync(x => x.ParentId == menu.Id && !x.IsDeleted, ct)) throw new ConflictException("请先删除子菜单");
        menu.IsDeleted = true; menu.DeletedAt = clock.UtcNow; menu.DeletedBy = currentUser.UserId; await menus.UpdateAsync(menu, ct);
    }

    private async Task ReplaceUserRolesAsync(long userId, IEnumerable<string> roleIdStrings, CancellationToken ct)
    {
        var roleIds = roleIdStrings.Select(x => IdParser.Parse(x, "roleId")).Distinct().ToArray();
        foreach (var roleId in roleIds) if (!await roles.ExistsAsync(x => x.Id == roleId && !x.IsDeleted && x.Status == EnabledStatus.Enabled, ct)) throw new NotFoundException($"角色 {roleId} 不存在或未启用");
        await userRoles.DeleteWhereAsync(x => x.UserId == userId, ct);
        await userRoles.InsertRangeAsync(roleIds.Select(x => new SysUserRole { Id = ids.NextId(), UserId = userId, RoleId = x, CreatedAt = clock.UtcNow, CreatedBy = currentUser.UserId }), ct);
    }

    private SysMenu CreateMenu(MenuUpsertRequest r) { var e = new SysMenu(); ApplyMenu(e, r); return e; }
    private static void ApplyMenu(SysMenu e, MenuUpsertRequest r)
    {
        e.ParentId = IdParser.ParseNullable(r.ParentId, "parentId"); e.Type = r.Type; e.Name = r.Name.Trim(); e.RoutePath = r.RoutePath;
        e.Component = r.Component; e.PermissionCode = string.IsNullOrWhiteSpace(r.PermissionCode) ? null : r.PermissionCode.Trim(); e.Icon = r.Icon; e.SortOrder = r.SortOrder; e.Visible = r.Visible; e.Status = r.Status;
    }
    private static void EnsureStrongPassword(string password)
    {
        if (password.Length < 8 || !password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit) || !password.Any(ch => !char.IsLetterOrDigit(ch)))
            throw new BusinessException("密码必须至少 8 位，并包含大小写字母、数字和特殊字符", "WEAK_PASSWORD");
    }
    private static async Task<T> GetRequiredAsync<T>(IRepository<T> repository, long id, string message, CancellationToken ct) where T : class =>
        await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException(message);
}
