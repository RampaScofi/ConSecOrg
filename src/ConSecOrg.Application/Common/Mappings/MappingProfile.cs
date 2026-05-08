using AutoMapper;
using ConSecOrg.Domain.Entities;
using ConSecOrg.Domain.Enumerations;
using ConSecOrg.Shared.DTOs.Audit;
using ConSecOrg.Shared.DTOs.Users;

namespace ConSecOrg.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<UserSettings, UserSettingsDto>()
            .ForMember(d => d.LayoutSettings, o => o.MapFrom(s => s.LayoutSettingsJson));

        CreateMap<Session, SessionDto>();

        CreateMap<AuditLog, AuditLogDto>()
            .ForMember(d => d.Action, o => o.MapFrom(s => s.Action.ToString()))
            .ForMember(d => d.Details, o => o.MapFrom(s => s.DetailsJson));
    }
}
