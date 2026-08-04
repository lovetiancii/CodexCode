using AutoMapper;
using Tianci.OA.Application.Modules.Contracts;
using Tianci.OA.Application.Modules.Employees;
using Tianci.OA.Application.Modules.Identity;
using Tianci.OA.Application.Modules.Organization;
using Tianci.OA.Application.Modules.Recruitment;
using Tianci.OA.Domain.Contracts;
using Tianci.OA.Domain.Employees;
using Tianci.OA.Domain.Identity;
using Tianci.OA.Domain.Organization;
using Tianci.OA.Domain.Recruitment;

namespace Tianci.OA.Application.Common;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<SysUser, UserDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.EmployeeId,
                options => options.MapFrom(source => source.EmployeeId.HasValue
                    ? source.EmployeeId.Value.ToString()
                    : null))
            .ForMember(destination => destination.DepartmentId,
                options => options.MapFrom(source => source.DepartmentId.HasValue
                    ? source.DepartmentId.Value.ToString()
                    : null));

        CreateMap<SysRole, RoleDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()));

        CreateMap<SysMenu, MenuDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.ParentId,
                options => options.MapFrom(source => source.ParentId.HasValue
                    ? source.ParentId.Value.ToString()
                    : null));

        CreateMap<Department, DepartmentDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.ParentId,
                options => options.MapFrom(source => source.ParentId.HasValue
                    ? source.ParentId.Value.ToString()
                    : null));

        CreateMap<Position, PositionDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.DepartmentId,
                options => options.MapFrom(source => source.DepartmentId.ToString()));

        CreateMap<Employee, EmployeeDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.DepartmentId,
                options => options.MapFrom(source => source.DepartmentId.ToString()))
            .ForMember(destination => destination.PositionId,
                options => options.MapFrom(source => source.PositionId.ToString()))
            .ForMember(destination => destination.SourceResumeId,
                options => options.MapFrom(source => source.SourceResumeId.HasValue
                    ? source.SourceResumeId.Value.ToString()
                    : null));

        CreateMap<Resume, ResumeDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.AppliedPositionId,
                options => options.MapFrom(source => source.AppliedPositionId.ToString()))
            .ForMember(destination => destination.AttachmentFileId,
                options => options.MapFrom(source => source.AttachmentFileId.HasValue
                    ? source.AttachmentFileId.Value.ToString()
                    : null));

        CreateMap<InterviewRecord, InterviewDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.ResumeId,
                options => options.MapFrom(source => source.ResumeId.ToString()))
            .ForMember(destination => destination.InterviewerUserId,
                options => options.MapFrom(source => source.InterviewerUserId.ToString()));

        CreateMap<EmployeeContract, ContractDto>()
            .ForMember(destination => destination.Id,
                options => options.MapFrom(source => source.Id.ToString()))
            .ForMember(destination => destination.EmployeeId,
                options => options.MapFrom(source => source.EmployeeId.ToString()))
            .ForMember(destination => destination.AttachmentFileId,
                options => options.MapFrom(source => source.AttachmentFileId.HasValue
                    ? source.AttachmentFileId.Value.ToString()
                    : null));
    }
}
