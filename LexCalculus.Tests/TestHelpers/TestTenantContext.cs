using LexCalculus.Core.Services;

namespace LexCalculus.Tests.TestHelpers;

/// <summary>
/// Test-only ITenantContext: explicit user/tenant perspektifi kurmaya yarar.
/// Default ctor (her iki property null) NoOp davranışı — bireysel anonim
/// kullanıcı senaryosu (filter "kendi null-tenant kayıtları" ister, ama
/// CurrentUserId de null olduğundan hiçbir kayıt görünmez).
///
/// Mevcut CalculationHistory testleri seed user perspektifini explicit
/// kurmalı: <c>new TestTenantContext { CurrentUserId = user.Id }</c>.
/// </summary>
internal sealed class TestTenantContext : ITenantContext
{
    public int? CurrentTenantId { get; init; }
    public int? CurrentUserId { get; init; }
    public bool IsTenantMember => CurrentTenantId.HasValue;
}
