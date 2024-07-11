using Iridium.Domain.Attributes.Base;
using Iridium.Domain.Common;
using Iridium.Domain.Constants;
using Iridium.Domain.Enums;

namespace Iridium.Application.Dtos;

[EndpointSettings("Todos/Template", "Todos/PaginatedList", "Todos/GetList", "Todos/Get", "Todos/Insert",
    "Todos/Update", "Todos/Delete", "Todos/Delete", "Todos/GetDropdown")]
public class TodoBriefDto : BaseDto
{
    [FormComponent("Content", true, FormInputType.InputText, true, true, true, 12, AttributeConfigurations.NoMask,
        AttributeConfigurations.NoCascade)]
    public string Content { get; set; }

    [FormComponent("IsCompleted", true, FormInputType.BoolSwitch, false, true, true, 12, AttributeConfigurations.NoMask,
        AttributeConfigurations.NoCascade)]
    public bool IsCompleted { get; set; }
}
