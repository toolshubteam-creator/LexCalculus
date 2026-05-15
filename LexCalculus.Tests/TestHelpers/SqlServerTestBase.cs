namespace LexCalculus.Tests.TestHelpers;

/// <summary>
/// Adım 5.8 P2 (charter Karar 10) — SQL Server LocalDB kullanan servis/birim
/// test sınıfları için ortak taban.
///
/// xUnit her <c>[Fact]</c> için test sınıfının yeni bir instance'ını
/// oluşturur; <see cref="IAsyncLifetime.InitializeAsync"/> her testten önce
/// taze bir <see cref="SqlServerTestDb"/> (unique LocalDB veritabanı +
/// migration) kurar, <see cref="IAsyncLifetime.DisposeAsync"/> testten sonra
/// düşürür. Bu, InMemory <c>TestDbContextFactory</c>'nin "her çağrı taze DB"
/// davranışıyla birebir simetriktir.
///
/// Türetilen sınıf <c>_db.Create(...)</c> ile context açar — imza
/// <c>TestDbContextFactory.Create(...)</c> ile aynıdır.
///
/// Not: Boilerplate'i her sınıfa kopyalamak yerine taban sınıfa toplandı —
/// bkz. tech-debt madde 34. Kendi <see cref="IAsyncLifetime"/> davranışına
/// ihtiyaç duyan sınıf (örn. ek temp-dir cleanup) <see cref="IDisposable"/>
/// ekleyebilir; xUnit her ikisini de çağırır.
/// </summary>
public abstract class SqlServerTestBase : IAsyncLifetime
{
    protected SqlServerTestDb _db = null!;

    public virtual async Task InitializeAsync()
        => _db = await SqlServerTestDb.CreateAsync();

    public virtual async Task DisposeAsync()
        => await _db.DisposeAsync();
}
