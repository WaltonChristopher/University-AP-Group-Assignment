namespace Yodaphone.Web.Services;

/// <summary>
/// Supplies the server-trusted ID of the user whose conversations are being handled.
/// </summary>
public interface ICurrentUser
{
    int UserId { get; }
}
