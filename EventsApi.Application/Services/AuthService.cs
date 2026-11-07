using EventsApi.Application.Abstractions;
using EventsApi.Application.Dto;
using EventsApi.Application.Repositories;
using EventsApi.Domain.Entities;
using EventsApi.Domain.Exceptions;

namespace EventsApi.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly string _dummyHash;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _dummyHash = passwordHasher.Hash("dummy-password-for-constant-work");
    }

    public async Task<UserResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Login))
            throw new DomainValidationException("Login is required");

        if (string.IsNullOrEmpty(request.Password))
            throw new DomainValidationException("Password is required");

        if (request.Role is { } requestedRole && !Enum.IsDefined(requestedRole))
            throw new DomainValidationException("Unknown role");

        var login = request.Login.Trim();

        if (await _userRepository.ExistsByLoginAsync(login, cancellationToken))
            throw new UserAlreadyExistsException(login);

        var user = User.Create(login, _passwordHasher.Hash(request.Password), request.Role ?? Role.User);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new UserResponse
        {
            Id = user.Id,
            Login = user.Login,
            Role = user.Role
        };
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var login = request.Login?.Trim() ?? string.Empty;

        var user = await _userRepository.GetByLoginAsync(login, cancellationToken);

        var passwordValid = _passwordHasher.Verify(
            request.Password ?? string.Empty,
            user?.PasswordHash ?? _dummyHash);

        if (user is null || !passwordValid)
            throw new InvalidCredentialsException();

        var token = _jwtTokenService.GenerateToken(user.Id, user.Login, user.Role.ToString());

        return new LoginResponse { Token = token };
    }
}
