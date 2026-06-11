using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordService _passwordService;
    private readonly IMapper _mapper;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ITokenService tokenService,
        IPasswordService passwordService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _passwordService = passwordService;
        _mapper = mapper;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.FindAsync(x => x.Email.ToLower() == request.Email.ToLower().Trim());

        if (user == null) return Result<LoginResponse>.Failure(401, "Invalid email or password");

        var isPasswordTrue = _passwordService.VerifyPassword(request.Password, user.PasswordHash);

        if (!isPasswordTrue) return Result<LoginResponse>.Failure(401, "Invalid email or password");


        var token = _tokenService.CreateTempToken(user.Id.ToString());

        var res = new LoginResponse
        {
            TempToken = token,
            IsEmailVerified = user.IsEmailVerified,
            IsProfileCompleted = user.IsProfileCompleted,
            Businesses = _mapper.Map<List<BusinessLoginResponse>>(user.Businesses)
        };

        return Result<LoginResponse>.Success(res, 200, "Login successful");
    }
}
