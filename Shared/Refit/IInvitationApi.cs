using FourPlayWebApp.Shared.Models.Data.Dtos;
using FourPlayWebApp.Shared.Models.Email;
using Refit;

namespace FourPlayWebApp.Shared.Refit;

public interface IInvitationApi
{
    [Get("/api/invitations/all")]
    Task<List<InvitationDto>> GetAll();

    [Get("/api/invitations/user/{userId}")]
    Task<List<InvitationDto>> GetByUser(string userId);

    [Post("/api/invitations")]
    Task<InvitationDto> Create([Query] string email, [Query] string invitedByUserId);

    [Delete("/api/invitations/{id}")]
    Task Delete(int id);

    [Get("/api/invitations/validate/{code}")]
    Task<InvitationDto?> Validate(string code);

    [Post("/api/invitations/use")]
    Task<bool> MarkAsUsed([Query] string code, [Query] string registeredUserId);

    // -----------------------------
    // Email Endpoints
    // -----------------------------

    [Post("/api/invitations/send")]
    Task<ApiResponse<string>> SendEmailAsync([Body] EmailRequest request);

    [Post("/api/invitations/send-confirmation")]
    Task<ApiResponse<string>> SendConfirmationAsync([Body] ConfirmationRequest request);

    [Post("/api/invitations/send-reset-link")]
    Task<ApiResponse<string>> SendPasswordResetLinkAsync([Body] PasswordResetLinkRequest request);

    [Post("/api/invitations/send-reset-code")]
    Task<ApiResponse<string>> SendPasswordResetCodeAsync([Body] PasswordResetCodeRequest request);
}
