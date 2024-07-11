using Iridium.Application.Dtos;
using Iridium.Domain.Common;
using Iridium.Domain.Entities;

namespace Iridium.Application.Services.AuthSrv;

public interface IAuthService
{
    Task<ServiceResult<bool>> RegisterUser(UserRegisterDto registerRequest);

    Task<ServiceResult<User>> GetAuthenticatedUser(UserLoginDto loginRequest);

    Task<ServiceResult<UserTokenDto>> LoginAndGetUserToken(UserLoginDto loginRequest);
}