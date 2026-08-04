using System.Text.Json;
using SqlSugar;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Identity;
using ApplicationCacheService = Tianci.OA.Application.Abstractions.ICacheService;

namespace Tianci.OA.Infrastructure.Authorization;

public sealed class PermissionService(
    ISqlSugarClient db,
    ApplicationCacheService cache) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(long userId, string permissionCode)
    {
        var permissions = await GetPermissionsAsync(userId);

        return permissions.Contains("*") || permissions.Contains(permissionCode);
    }
    public async Task<IReadOnlySet<string>> GetPermissionsAsync(long userId)
    {
        var key = $"oa:perm:user:{userId}";
        var cached = await cache.GetAsync(key);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<HashSet<string>>(cached) ?? [];
        }

        var isSuper = await db
            .Queryable<SysUserRole, SysRole>(
                (userRole, role) => userRole.RoleId == role.Id)
            .Where((userRole, role) =>
                userRole.UserId == userId
                && role.Code == "SUPER_ADMIN"
                && role.Status == EnabledStatus.Enabled
                && !role.IsDeleted)
            .AnyAsync();

        HashSet<string> result;
        if (isSuper)
        {
            result = ["*"];
        }
        else
        {
            var list = await db
                .Queryable<SysUserRole, SysRole, SysRoleMenu, SysMenu>(
                    (userRole, role, roleMenu, menu) =>
                        userRole.RoleId == role.Id
                        && role.Id == roleMenu.RoleId
                        && roleMenu.MenuId == menu.Id)
                .Where((userRole, role, roleMenu, menu) =>
                    userRole.UserId == userId
                    && role.Status == EnabledStatus.Enabled
                    && !role.IsDeleted
                    && menu.Status == EnabledStatus.Enabled
                    && !menu.IsDeleted
                    && menu.PermissionCode != null)
                .Select((userRole, role, roleMenu, menu) => menu.PermissionCode!)
                .Distinct()
                .ToListAsync();

            result = list.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        await cache.SetAsync(key, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(10));

        return result;
    }
}
