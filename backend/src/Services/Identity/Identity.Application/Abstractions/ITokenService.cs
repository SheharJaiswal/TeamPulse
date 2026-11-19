using Identity.Domain.Entities;
namespace Identity.Application.Abstractions;
public interface ITokenService { string CreateAccessToken(Employee employee); }