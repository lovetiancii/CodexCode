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
            .ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()))
            .ForMember(d => d.EmployeeId, o => o.MapFrom(s => s.EmployeeId.HasValue ? s.EmployeeId.Value.ToString() : null))
            .ForMember(d => d.DepartmentId, o => o.MapFrom(s => s.DepartmentId.HasValue ? s.DepartmentId.Value.ToString() : null));
        CreateMap<SysRole, RoleDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString()));
        CreateMap<SysMenu, MenuDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString())).ForMember(d => d.ParentId, o => o.MapFrom(s => s.ParentId.HasValue ? s.ParentId.Value.ToString() : null));
        CreateMap<Department, DepartmentDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString())).ForMember(d => d.ParentId, o => o.MapFrom(s => s.ParentId.HasValue ? s.ParentId.Value.ToString() : null));
        CreateMap<Position, PositionDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString())).ForMember(d => d.DepartmentId, o => o.MapFrom(s => s.DepartmentId.ToString()));
        CreateMap<Employee, EmployeeDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString())).ForMember(d => d.DepartmentId, o => o.MapFrom(s => s.DepartmentId.ToString())).ForMember(d => d.PositionId, o => o.MapFrom(s => s.PositionId.ToString())).ForMember(d => d.SourceResumeId, o => o.MapFrom(s => s.SourceResumeId.HasValue ? s.SourceResumeId.Value.ToString() : null));
        CreateMap<Resume, ResumeDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString())).ForMember(d => d.AppliedPositionId, o => o.MapFrom(s => s.AppliedPositionId.ToString())).ForMember(d => d.AttachmentFileId, o => o.MapFrom(s => s.AttachmentFileId.HasValue ? s.AttachmentFileId.Value.ToString() : null));
        CreateMap<InterviewRecord, InterviewDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString())).ForMember(d => d.ResumeId, o => o.MapFrom(s => s.ResumeId.ToString())).ForMember(d => d.InterviewerUserId, o => o.MapFrom(s => s.InterviewerUserId.ToString()));
        CreateMap<EmployeeContract, ContractDto>().ForMember(d => d.Id, o => o.MapFrom(s => s.Id.ToString())).ForMember(d => d.EmployeeId, o => o.MapFrom(s => s.EmployeeId.ToString())).ForMember(d => d.AttachmentFileId, o => o.MapFrom(s => s.AttachmentFileId.HasValue ? s.AttachmentFileId.Value.ToString() : null));
    }
}
