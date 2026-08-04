namespace Tianci.OA.Application.Modules.Organization;

public interface IOrganizationService
{
    Task<IReadOnlyList<DepartmentDto>> DepartmentsAsync(CancellationToken cancellationToken);

    Task<DepartmentDto> CreateDepartmentAsync(
        DepartmentRequest request,
        CancellationToken cancellationToken);

    Task<DepartmentDto> UpdateDepartmentAsync(
        string id,
        DepartmentRequest request,
        CancellationToken cancellationToken);

    Task DeleteDepartmentAsync(string id, CancellationToken cancellationToken);

    Task<IReadOnlyList<PositionDto>> PositionsAsync(
        string? departmentId,
        CancellationToken cancellationToken);

    Task<PositionDto> CreatePositionAsync(
        PositionRequest request,
        CancellationToken cancellationToken);

    Task<PositionDto> UpdatePositionAsync(
        string id,
        PositionRequest request,
        CancellationToken cancellationToken);

    Task DeletePositionAsync(string id, CancellationToken cancellationToken);
}
