using Tianci.OA.Application.Common;
using Tianci.OA.Application.Modules.Identity;
using Tianci.OA.Domain.Identity;

namespace Tianci.OA.UnitTests;

public sealed class IdentityBindingQueryTests
{
    [Fact]
    public async Task Get_user_role_ids_throws_when_user_does_not_exist()
    {
        var fixture = new Fixture();

        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Service.GetUserRoleIdsAsync("100", default));

        Assert.Equal("NOT_FOUND", error.Code);
        Assert.Equal("用户不存在", error.Message);
    }

    [Fact]
    public async Task Get_role_menu_ids_throws_when_role_does_not_exist()
    {
        var fixture = new Fixture();

        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            fixture.Service.GetRoleMenuIdsAsync("200", default));

        Assert.Equal("NOT_FOUND", error.Code);
        Assert.Equal("角色不存在", error.Message);
    }

    [Fact]
    public async Task Get_user_role_ids_returns_distinct_current_bindings_and_filters_deleted_roles()
    {
        var fixture = new Fixture(
            users: [new SysUser { Id = 100 }],
            roles:
            [
                new SysRole { Id = 11 },
                new SysRole { Id = 12 },
                new SysRole { Id = 13, IsDeleted = true }
            ],
            userRoles:
            [
                new SysUserRole { Id = 1, UserId = 100, RoleId = 11 },
                new SysUserRole { Id = 2, UserId = 100, RoleId = 11 },
                new SysUserRole { Id = 3, UserId = 100, RoleId = 12 },
                new SysUserRole { Id = 4, UserId = 100, RoleId = 13 },
                new SysUserRole { Id = 5, UserId = 100, RoleId = 999 },
                new SysUserRole { Id = 6, UserId = 101, RoleId = 12 }
            ]);

        var ids = await fixture.Service.GetUserRoleIdsAsync("100", default);

        Assert.Equal(["11", "12"], ids);
    }

    [Fact]
    public async Task Get_role_menu_ids_returns_distinct_current_bindings_and_filters_deleted_menus()
    {
        var fixture = new Fixture(
            roles: [new SysRole { Id = 200 }],
            menus:
            [
                new SysMenu { Id = 21 },
                new SysMenu { Id = 22 },
                new SysMenu { Id = 23, IsDeleted = true }
            ],
            roleMenus:
            [
                new SysRoleMenu { Id = 1, RoleId = 200, MenuId = 21 },
                new SysRoleMenu { Id = 2, RoleId = 200, MenuId = 21 },
                new SysRoleMenu { Id = 3, RoleId = 200, MenuId = 22 },
                new SysRoleMenu { Id = 4, RoleId = 200, MenuId = 23 },
                new SysRoleMenu { Id = 5, RoleId = 200, MenuId = 999 },
                new SysRoleMenu { Id = 6, RoleId = 201, MenuId = 22 }
            ]);

        var ids = await fixture.Service.GetRoleMenuIdsAsync("200", default);

        Assert.Equal(["21", "22"], ids);
    }

    private sealed class Fixture
    {
        public IdentityService Service { get; }

        public Fixture(
            SysUser[]? users = null,
            SysRole[]? roles = null,
            SysMenu[]? menus = null,
            SysUserRole[]? userRoles = null,
            SysRoleMenu[]? roleMenus = null)
        {
            Service = new IdentityService(
                new InMemoryRepository<SysUser>(users ?? []),
                new InMemoryRepository<SysRole>(roles ?? []),
                new InMemoryRepository<SysMenu>(menus ?? []),
                new InMemoryRepository<SysUserRole>(userRoles ?? []),
                new InMemoryRepository<SysRoleMenu>(roleMenus ?? []),
                null!,
                null!,
                null!,
                new StubIds(),
                new StubClock(),
                new StubCurrentUser(),
                new TrackingUnitOfWork(),
                null!);
        }
    }
}
