using System.Text.Json;
using SqlSugar;
using Tianci.OA.Application.Abstractions;
using Tianci.OA.Domain.Common;
using Tianci.OA.Domain.Identity;

namespace Tianci.OA.Infrastructure.Authorization;

public sealed class PermissionService(ISqlSugarClient db, Tianci.OA.Application.Abstractions.ICacheService cache) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(long userId, string permissionCode)
    {
        var permissions = await GetPermissionsAsync(userId); return permissions.Contains("*") || permissions.Contains(permissionCode);
    }
    public async Task<IReadOnlySet<string>> GetPermissionsAsync(long userId)
    {
        var key = $"oa:perm:user:{userId}"; var cached = await cache.GetAsync(key);
        if (cached != null) return JsonSerializer.Deserialize<HashSet<string>>(cached) ?? new HashSet<string>();
        var isSuper = await db.Queryable<SysUserRole, SysRole>((ur, r) => ur.RoleId == r.Id)
            .Where((ur, r) => ur.UserId == userId && r.Code == "SUPER_ADMIN" && r.Status == EnabledStatus.Enabled && !r.IsDeleted).AnyAsync();
        HashSet<string> result;
        if (isSuper) result = ["*"];
        else
        {
            var list = await db.Queryable<SysUserRole, SysRole, SysRoleMenu, SysMenu>((ur, r, rm, m) => ur.RoleId == r.Id && r.Id == rm.RoleId && rm.MenuId == m.Id)
                .Where((ur, r, rm, m) => ur.UserId == userId && r.Status == EnabledStatus.Enabled && !r.IsDeleted && m.Status == EnabledStatus.Enabled && !m.IsDeleted && m.PermissionCode != null)
                .Select((ur, r, rm, m) => m.PermissionCode!).Distinct().ToListAsync();
            result = list.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        await cache.SetAsync(key, JsonSerializer.Serialize(result), TimeSpan.FromMinutes(10)); return result;
    }
}
