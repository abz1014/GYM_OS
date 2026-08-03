namespace GymOS.Application.Common.Interfaces;

public interface ITotpService
{
    string GenerateSecret();

    string GenerateQrCodeUri(string secret, string accountEmail, string issuer);

    bool ValidateCode(string secret, string code);
}
