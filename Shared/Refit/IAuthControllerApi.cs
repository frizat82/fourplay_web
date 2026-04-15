using FourPlayWebApp.Shared.Models.Account;
using FourPlayWebApp.Shared.Models.Account.Dto;
using Refit;

namespace FourPlayWebApp.Shared.Refit
{
    public interface IAuthControllerApi
    {
        [Post("/api/auth/login")]
        Task<ApiResponse<SignInResultDto>> Login([Body] LoginRequest request);

        [Post("/api/auth/logout")]
        Task<IApiResponse> Logout();

        [Get("/api/auth/me")]
        Task<ApiResponse<UserInfo>> Me();

        [Post("/api/auth/confirm-email")]
        Task<ApiResponse<string>> ConfirmEmail([Body] ConfirmEmailRequest confirmRequest);
        // Assign a role to a user
        [Post("/api/auth/assign-user-role")]
        Task<ApiResponse<string>> AssignUserRoleAsync([Body] AssignRoleRequest request);

        [Post("/api/auth/delete-user/{userId}")]
        Task<ApiResponse<string>> DeleteUser(string userId);

        [Get("/api/auth/admin-confirm-email/{userId}")]
        public Task<ApiResponse<string>> ConfirmEmailAdmin(string userId);

        [Post("/api/auth/change-password")]
        Task<ApiResponse<string>> ChangePassword([Body] ChangePassword model);
        [Post("/api/auth/reset-password")]
        Task<ApiResponse<string>> ResetPassword([Body] ResetPasswordRequest model);

        [Post("/api/auth/request-email-confirmation")]
        Task<ApiResponse<string>> RequestEmailConfirmation([Body] RequestEmailConfirmation request);

        [Post("/api/auth/forgot-password")]
        Task<ApiResponse<string>> ForgotPassword([Body] ForgotPasswordRequest model);

        //[Post("/api/auth/is-email-confirmation")]
        //public Task<ApiResponse<string>> IsEmailConfirmed(string email);
        [Post("/api/auth/create-user")]
        Task<ApiResponse<CreateUserResponse>> CreateUser([Body] CreateUserRequest user);

        [Post("/api/auth/refresh")]
        Task<IApiResponse> Refresh();

        [Post("/api/auth/does-user-exist/{email}")]
        Task<ApiResponse<bool>> DoesUserExist(string email);

    }
}
