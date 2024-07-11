using AutoMapper;
using Iridium.Application.Dtos;
using Iridium.Domain.Entities;

namespace Iridium.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Todo, TodoBriefDto>().ReverseMap();
    }
}