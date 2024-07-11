using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Iridium.Application.Dtos;
using Iridium.Application.Models;
using Iridium.Domain.Cache;
using Iridium.Domain.Common;
using Iridium.Domain.Constants;
using Iridium.Domain.Constants.Validations;
using Iridium.Domain.Entities;
using Iridium.Domain.Enums;
using Iridium.Domain.Extensions;
using Iridium.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Iridium.Application.Services.AuthSrv;

public class AuthService : BaseService, IAuthService
{
    public AuthService(IMemoryCache memoryCache, ApplicationDbContext dbContext, IOptions<AppSettings> appSettings) :
        base(memoryCache, dbContext, appSettings)
    {
    }

    public async Task<ServiceResult<bool>> RegisterUser(UserRegisterDto registerRequest)
    {
        // TODO: Phone Validation
        //if (!registerRequest.PhoneNumber.IsValidPhoneNumber())
        //    return new ServiceResult<bool>("The phone number is invalid.");
        // TODO: Mail Client and Commercial Mail System

        var password = registerRequest.Password;
        var confirmPassword = registerRequest.PasswordConfirm;
        var mailAddress = registerRequest.MailAddress;

        if (!mailAddress.IsValidMailAddress())
            return GetFailedResult(ValidationConstants.MailAddressIsInvalid);

        if (!await ValidateUserExists(mailAddress))
            return GetFailedResult(ValidationConstants.UserAlreadyExits);

        if (!ValidateIsPasswordMin8CharactersLong(password))
            return GetFailedResult(ValidationConstants.PasswordShouldBeAtLeast8CharactersLong);

        if (!ValidateIsPasswordMax16CharactersLong(password))
            return GetFailedResult(ValidationConstants.PasswordShouldBeMax16CharactersLong);

        if (!ValidatePasswordAndConfirmPasswordMatch(password, confirmPassword))
            return GetFailedResult(ValidationConstants.PasswordAndConfirmPasswordDoNotMatch);

        if (!ValidateIsPasswordContainsAtLeastOneUpperCaseLetter(password))
            return GetFailedResult(ValidationConstants.PasswordShouldContainAtLeastOneUpperCaseLetter);

        if (!ValidateIsPasswordContainsAtLeastOneLowerCaseLetter(password))
            return GetFailedResult(ValidationConstants.PasswordShouldContainAtLeastOneLowerCaseLetter);

        if (!ValidateIsPasswordContainsAtLeastOneDigit(password))
            return GetFailedResult(ValidationConstants.PasswordShouldContainAtLeastOneDigit);

        if (!ValidateIsPasswordContainsAtLeastOneSpecialCharacter(password))
            return GetFailedResult(ValidationConstants.PasswordShouldContainAtLeastOneSpecialCharacter);

        var hashedPassword = password.ToSHA256Hash();
        var validationKey = Guid.NewGuid().ToString(); // for mail validation

        // for adding user
        var user = new User
        {
            MailAddress = registerRequest.MailAddress,
            Password = hashedPassword,
            PhoneNumber = registerRequest.PhoneNumber,
            ValidationKey = validationKey,
            ValidationExpire = DateTime.UtcNow,
            UserState = (short)UserState.Completed, // Mail Link Registering -to do
            IsPremium = false,
            CreatedBy = 1,
            CreatedDate = DateTime.UtcNow
        };

        await DbContext.User.AddAsync(user);
        await DbContext.SaveChangesAsync();

        long todoFullRoleId = 0;
        RoleCache.TryGetValue(RoleParamCode.TodoFull, out todoFullRoleId);

        var roleIds = new List<long> { todoFullRoleId };

        var userId = user.Id;
        var userRoles = new List<UserRole>();

        foreach (var roleId in roleIds)
            userRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId
            });

        await DbContext.UserRole.AddRangeAsync(userRoles);
        await DbContext.SaveChangesAsync();

        return GetSucceededResult();
    }

    public async Task<ServiceResult<User>> GetAuthenticatedUser(UserLoginDto loginRequest)
    {
        var hashedPassword = loginRequest.Password.ToSHA256Hash();

        var user = await DbContext.User.Where(w => w.MailAddress == loginRequest.MailAddress)
            .Where(w => w.Password == hashedPassword)
            .FirstOrDefaultAsync();

        return user == null
            ? new ServiceResult<User>(ValidationConstants.MailAddressOrPasswordIsWrong)
            : new ServiceResult<User>(user);
    }

    public async Task<ServiceResult<UserTokenDto>> LoginAndGetUserToken(UserLoginDto loginRequest)
    {
        var hashedPassword = loginRequest.Password.ToSHA256Hash();

        var user = await DbContext.User.Where(w => w.MailAddress == loginRequest.MailAddress)
            .Where(w => w.Password == hashedPassword)
            .FirstOrDefaultAsync();

        if (user == null)
            return new ServiceResult<UserTokenDto>(ValidationConstants.MailAddressOrPasswordIsWrong);

        return await LoginUserAndGenerateJwtToken(user);

        #region Mail Link

        // TODO : Mail Link For Validation

        //user.ValidationKey = Guid.NewGuid().ToString();
        //user.ValidationExpire = DateTime.UtcNow;

        //_dbContext.User.Update(user);

        //await _dbContext.SaveChangesAsync();

        //return new ServiceResult<UserLoginResponse>("To continue, verify your e-mail address by clicking on the new link sent to your e-mail.");

        #endregion
    }

    #region Private Helper Methods

    private async Task<ServiceResult<UserTokenDto>> LoginUserAndGenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var symmetricKey = AppSettings.JwtConfig.SecretKey;
        var key = Encoding.ASCII.GetBytes(symmetricKey);

        var userRoleParamCodes = DbContext.UserRole.Include(ur => ur.Role)
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.Role.ParamCode)
            .ToList();

        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, user.MailAddress),
            new("UserId", user.Id.ToString())
        };

        var userRolesWithChild = await GetRoleHierarchyAsync(userRoleParamCodes);
        var allUserRoleParamCodesWithChild = userRolesWithChild.Select(s => s.ParamCode).ToList();

        foreach (var userRoleParamCode in allUserRoleParamCodesWithChild)
            claims.Add(new Claim(ClaimTypes.Role, userRoleParamCode));

        var tokenExpireDate = DateTime.UtcNow.AddDays(ConfigurationConstants.TokenExpireAsDay);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = tokenExpireDate,
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        var loginResponse = new UserTokenDto
        {
            AccessToken = accessToken,
            ExpiresIn = tokenExpireDate
        };

        return new ServiceResult<UserTokenDto>(loginResponse);
    }

    private bool ValidateJwtToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var symmetricKey = AppSettings.JwtConfig.SecretKey;
        var key = Encoding.ASCII.GetBytes(symmetricKey);

        tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);

        return validatedToken != null;
    }

    #endregion

    #region Private Validation Methods

    private async Task<bool> ValidateUserExists(string mailAddress)
    {
        return !await DbContext.User.AnyAsync(w => w.Deleted != true && w.MailAddress == mailAddress);
    }

    private bool ValidateIsPasswordMin8CharactersLong(string password)
    {
        return password.Length >= 8;
    }

    private bool ValidateIsPasswordMax16CharactersLong(string password)
    {
        return password.Length <= 16;
    }

    private bool ValidatePasswordAndConfirmPasswordMatch(string password, string confirmPassword)
    {
        return password == confirmPassword;
    }

    private bool ValidateIsPasswordContainsAtLeastOneUpperCaseLetter(string password)
    {
        return password.Any(char.IsUpper);
    }

    private bool ValidateIsPasswordContainsAtLeastOneLowerCaseLetter(string password)
    {
        return password.Any(char.IsLower);
    }

    private bool ValidateIsPasswordContainsAtLeastOneDigit(string password)
    {
        return password.Any(char.IsDigit);
    }

    private bool ValidateIsPasswordContainsAtLeastOneSpecialCharacter(string password)
    {
        return !password.All(char.IsLetterOrDigit);
    }

    #endregion
}