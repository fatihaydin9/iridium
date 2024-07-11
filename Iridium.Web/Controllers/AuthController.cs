using Iridium.Application.Dtos;
using Iridium.Application.Services.AuthSrv;
using Iridium.Domain.Common;
using Iridium.Web.Controllers.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iridium.Web.Controllers;

public class AuthController : ApiBaseController
{
    private readonly IAuthService AuthService;

    public AuthController(IAuthService authService)
    {
        AuthService = authService;
    }

    [AllowAnonymous]
    [HttpPost("Login")]
    public async Task<ServiceResult<UserTokenDto>> Login([FromBody] UserLoginDto loginRequest)
    {
        return await AuthService.LoginAndGetUserToken(loginRequest);
    }

    [AllowAnonymous]
    [HttpPost("Register")]
    public async Task<ServiceResult<bool>> Register([FromBody] UserRegisterDto userRegisterDto)
    {
        return await AuthService.RegisterUser(userRegisterDto);
    }
}