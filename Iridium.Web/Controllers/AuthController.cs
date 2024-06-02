﻿using Iridium.Domain.Common;
using Iridium.Domain.Models.RequestModels;
using Iridium.Domain.Models.ResponseModels;
using Iridium.Infrastructure.Services;
using Iridium.Web.Controllers.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Iridium.Web.Controllers;

public class AuthController : ApiBaseController
{
    public readonly IAuthService AuthService;

    public AuthController(IAuthService authService)
        => AuthService = authService;

    [AllowAnonymous]
    [HttpPost("Login")]
    public async Task<ServiceResult<UserLoginResponse>> Login([FromBody] UserLoginRequest loginRequest)
        => await AuthService.LoginAndGetUserToken(loginRequest);

    [AllowAnonymous]
    [HttpPost("Register")]
    public async Task<ServiceResult<bool>> Register([FromBody] UserRegisterRequest userRegisterDto)
        => await AuthService.RegisterUser(userRegisterDto);
    
    

}
