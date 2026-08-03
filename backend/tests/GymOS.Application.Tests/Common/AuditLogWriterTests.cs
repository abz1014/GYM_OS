using GymOS.Application.Common.Auditing;
using Shouldly;

namespace GymOS.Application.Tests.Common;

/// <summary>
/// Redaction is deny-by-default on a keyword substring match, with a narrow allowlist for
/// business identifiers that merely contain one of those keywords. Both directions matter:
/// leaking a secret is a security bug, and over-redacting a business field silently destroys
/// audit history that can never be recovered (Principle #7).
/// </summary>
public class AuditLogWriterTests
{
    private record SecretCarrying(string Password, string CurrentPassword, string NewPassword, string MfaSecret, string RefreshToken, string MfaCode);

    private record BusinessCarrying(string CouponCode, string MemberCode, string TemplateCode);

    private record MixedCommand(string Email, string Password, string? CouponCode, decimal Amount);

    [Fact]
    public void Secrets_are_redacted()
    {
        var json = AuditLogWriter.SerializeRedacted(
            new SecretCarrying("pw1", "pw2", "pw3", "S3CR3T", "rt-abc", "123456"));

        json.ShouldNotContain("pw1");
        json.ShouldNotContain("pw2");
        json.ShouldNotContain("pw3");
        json.ShouldNotContain("S3CR3T");
        json.ShouldNotContain("rt-abc");
        json.ShouldNotContain("123456");
    }

    [Fact]
    public void Business_identifiers_containing_a_keyword_are_preserved()
    {
        var json = AuditLogWriter.SerializeRedacted(new BusinessCarrying("SUMMER20", "MBR-00027", "low-stock"));

        json.ShouldContain("SUMMER20");
        json.ShouldContain("MBR-00027");
        json.ShouldContain("low-stock");
        json.ShouldNotContain("REDACTED");
    }

    [Fact]
    public void A_single_command_can_redact_one_field_while_keeping_another()
    {
        var json = AuditLogWriter.SerializeRedacted(new MixedCommand("a@b.com", "hunter2", "SUMMER20", 120m));

        json.ShouldContain("a@b.com");
        json.ShouldContain("SUMMER20");
        json.ShouldContain("120");
        json.ShouldNotContain("hunter2");
        json.ShouldContain("REDACTED");
    }

    [Fact]
    public void A_null_business_field_stays_null_rather_than_becoming_a_redaction_marker()
    {
        var json = AuditLogWriter.SerializeRedacted(new MixedCommand("a@b.com", "hunter2", null, 0m));

        json.ShouldContain("\"CouponCode\":null");
    }
}
