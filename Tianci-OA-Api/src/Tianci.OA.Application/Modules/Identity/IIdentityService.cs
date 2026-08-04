using Tianci.OA.Application.Common;

namespace Tianci.OA.Application.Modules.Identity;

public interface IIdentityService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task InitializeAdminAsync(InitializeAdminRequest request, CancellationToken cancellationToken);

    Task<PagedResult<UserDto>> GetUsersAsync(
        PageRequest page,
        string? keyword,
        CancellationToken cancellationToken);

    Task<UserDto> GetUserAsync(string id, CancellationToken cancellationToken);

    Task<UserDto> CreateUserAsync(UserCreateRequest request, CancellationToken cancellationToken);

    Task<UserDto> UpdateUserAsync(
        string id,
        UserUpdateRequest request,
        CancellationToken cancellationToken);

    Task ResetPasswordAsync(
        string id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetUserRoleIdsAsync(
        string id,
        CancellationToken cancellationToken);

    Task AssignUserRolesAsync(
        string id,
        AssignIdsRequest request,
        CancellationToken cancellationToken);

    Task DeleteUserAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken);

    Task<RoleDto> CreateRoleAsync(RoleUpsertRequest request, CancellationToken cancellationToken);

    Task<RoleDto> UpdateRoleAsync(
        string id,
        RoleUpsertRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetRoleMenuIdsAsync(
        string id,
        CancellationToken cancellationToken);

    Task AssignRoleMenusAsync(
        string id,
        AssignIdsRequest request,
        CancellationToken cancellationToken);

    Task DeleteRoleAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MenuDto>> GetMenusAsync(CancellationToken cancellationToken);

    Task<MenuDto> CreateMenuAsync(MenuUpsertRequest request, CancellationToken cancellationToken);

    Task<MenuDto> UpdateMenuAsync(
        string id,
        MenuUpsertRequest request,
        CancellationToken cancellationToken);

    Task DeleteMenuAsync(string id, CancellationToken cancellationToken);
}
