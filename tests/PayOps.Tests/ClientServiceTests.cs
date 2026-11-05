using Microsoft.EntityFrameworkCore;
using PayOps.Client.Data;
using PayOps.Client.Data.Services;
using PayOps.Database.Entities;
using PayOps.Database.Lookups;

namespace PayOps.Tests;

[Collection(SqlServerCollection.Name)]
public class ClientServiceTests(SqlServerFixture fixture)
{
    private PrimaryClientService Primaries(ClientDbContext db) => new(db, TimeProvider.System);
    private SecondaryClientService Secondaries(ClientDbContext db) => new(db, TimeProvider.System);

    private async Task<PrimaryClientDto> CreatePrimaryAsync(string? name = null)
    {
        await using var db = fixture.CreateClientContext();
        var result = await Primaries(db).CreateAsync(new CreatePrimaryClientRequest(TestData.Unique("P"), name ?? "Primary " + TestData.Unique("")));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    private async Task<SecondaryClientDto> CreateSecondaryAsync(int primaryId)
    {
        await using var db = fixture.CreateClientContext();
        var result = await Secondaries(db).CreateAsync(primaryId, new CreateSecondaryClientRequest(TestData.Unique("S"), "Secondary " + TestData.Unique("")));
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    [Fact]
    public async Task Primary_client_is_created_with_defaults_and_row_version()
    {
        var primary = await CreatePrimaryAsync();
        Assert.True(primary.IsActive);
        Assert.False(primary.AutoProcess);
        Assert.Equal(0, primary.SecondaryClientCount);
        Assert.NotEmpty(Convert.FromBase64String(primary.RowVersion));
    }

    [Fact]
    public async Task Lists_are_searched_and_paged()
    {
        var tag = TestData.Unique("T");
        for (var i = 0; i < 3; i++)
            await CreatePrimaryAsync($"{tag} {i}");

        await using var db = fixture.CreateClientContext();
        var page = (await Primaries(db).ListAsync(new ClientListQuery(tag, Page: 2, PageSize: 2))).Value!;
        Assert.Equal(3, page.TotalCount);
        Assert.Equal($"{tag} 2", Assert.Single(page.Items).Name);

        var invalid = await Primaries(db).ListAsync(new ClientListQuery(PageSize: ClientListQuery.MaxPageSize + 1));
        Assert.Equal(ClientErrorKind.Validation, invalid.Error!.Kind);
    }

    [Fact]
    public async Task Unique_client_id_is_unique_across_primary_and_secondary_clients()
    {
        var primary = await CreatePrimaryAsync();
        await using var db = fixture.CreateClientContext();

        var asSecondary = await Secondaries(db).CreateAsync(primary.Id, new CreateSecondaryClientRequest(primary.UniqueClientId, "Clash"));
        Assert.Equal(ClientErrorKind.Conflict, asSecondary.Error!.Kind);

        var secondary = await CreateSecondaryAsync(primary.Id);
        var asPrimary = await Primaries(db).CreateAsync(new CreatePrimaryClientRequest(secondary.UniqueClientId, "Clash"));
        Assert.Equal(ClientErrorKind.Conflict, asPrimary.Error!.Kind);
    }

    [Fact]
    public async Task Invalid_input_returns_field_errors()
    {
        await using var db = fixture.CreateClientContext();
        var result = await Primaries(db).CreateAsync(new CreatePrimaryClientRequest("HAS SPACE", new string('x', 101)));
        Assert.Equal(ClientErrorKind.Validation, result.Error!.Kind);
        Assert.Contains("uniqueClientId", result.Error.Errors!.Keys);
        Assert.Contains("name", result.Error.Errors.Keys);
    }

    [Fact]
    public async Task Stale_row_version_is_a_conflict()
    {
        var primary = await CreatePrimaryAsync();
        await using (var db = fixture.CreateClientContext())
        {
            var updated = await Primaries(db).UpdateAsync(primary.Id, new UpdatePrimaryClientRequest("Renamed", true, null, primary.RowVersion));
            Assert.True(updated.IsSuccess, updated.Error?.Message);
            Assert.Equal("Renamed", updated.Value!.Name);
            Assert.NotNull(updated.Value.ModifiedDate);
            Assert.NotEqual(primary.RowVersion, updated.Value.RowVersion);
        }

        await using (var db = fixture.CreateClientContext())
        {
            var stale = await Primaries(db).UpdateAsync(primary.Id, new UpdatePrimaryClientRequest("Lost update", false, null, primary.RowVersion));
            Assert.Equal(ClientErrorKind.Conflict, stale.Error!.Kind);
        }
    }

    [Fact]
    public async Task Primary_with_active_secondaries_cannot_be_deactivated()
    {
        var primary = await CreatePrimaryAsync();
        var secondary = await CreateSecondaryAsync(primary.Id);
        await using var db = fixture.CreateClientContext();

        Assert.Equal(ClientErrorKind.Conflict, (await Primaries(db).DeactivateAsync(primary.Id)).Error!.Kind);

        Assert.False((await Secondaries(db).DeactivateAsync(secondary.Id)).Value!.IsActive);
        Assert.False((await Primaries(db).DeactivateAsync(primary.Id)).Value!.IsActive);

        var create = await Secondaries(db).CreateAsync(primary.Id, new CreateSecondaryClientRequest(TestData.Unique("S"), "Late"));
        Assert.Equal(ClientErrorKind.Conflict, create.Error!.Kind);
        Assert.Equal(ClientErrorKind.Conflict, (await Secondaries(db).ActivateAsync(secondary.Id)).Error!.Kind);
    }

    [Fact]
    public async Task Only_review_secondaries_can_be_reassigned()
    {
        var placeholder = await CreatePrimaryAsync();
        var target = await CreatePrimaryAsync();
        var secondary = await CreateSecondaryAsync(placeholder.Id);
        await using var db = fixture.CreateClientContext();

        var notFlagged = await Secondaries(db).ReassignAsync(secondary.Id, new ReassignSecondaryClientRequest(target.Id, secondary.RowVersion));
        Assert.Equal(ClientErrorKind.Conflict, notFlagged.Error!.Kind);

        await db.SecondaryClients.Where(s => s.Id == secondary.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.RequiresReview, true));
        db.ChangeTracker.Clear();
        var review = (await Secondaries(db).ListAsync(new SecondaryClientListQuery(RequiresReview: true, PrimaryClientId: placeholder.Id))).Value!;
        var flagged = Assert.Single(review.Items);

        var moved = await Secondaries(db).ReassignAsync(secondary.Id, new ReassignSecondaryClientRequest(target.Id, flagged.RowVersion));
        Assert.True(moved.IsSuccess, moved.Error?.Message);
        Assert.Equal(target.Id, moved.Value!.PrimaryClientId);
        Assert.False(moved.Value.RequiresReview);
    }

    [Fact]
    public async Task Secondary_is_found_by_unique_client_id()
    {
        var primary = await CreatePrimaryAsync();
        var secondary = await CreateSecondaryAsync(primary.Id);
        await using var db = fixture.CreateClientContext();

        var found = (await Secondaries(db).GetByUniqueClientIdAsync(secondary.UniqueClientId)).Value!;
        Assert.Equal(secondary.Id, found.Id);
        Assert.Equal(primary.UniqueClientId, found.PrimaryClientUniqueId);
        Assert.Equal(ClientErrorKind.NotFound, (await Secondaries(db).GetByUniqueClientIdAsync("NOPE")).Error!.Kind);
    }

    [Fact]
    public async Task Primary_has_at_most_one_active_bank_account_per_type()
    {
        var primary = await CreatePrimaryAsync();
        await using var db = fixture.CreateClientContext();
        var service = new ClientBankAccountService(db, TimeProvider.System);
        var request = new CreateBankAccountRequest((byte)AccountTypeCode.Nominated, "Main", "250655", "62000000001");

        var first = (await service.CreateAsync(primary.Id, request)).Value!;
        Assert.Equal("Nominated Account", first.AccountType);
        Assert.Equal(ClientErrorKind.Conflict, (await service.CreateAsync(primary.Id, request)).Error!.Kind);
        Assert.True((await service.CreateAsync(primary.Id, request with { AccountTypeId = (byte)AccountTypeCode.Charges })).IsSuccess);

        await service.DeactivateAsync(first.Id);
        Assert.True((await service.CreateAsync(primary.Id, request)).IsSuccess);
        Assert.Equal(3, (await service.ListAsync(primary.Id)).Value!.Count);

        var invalid = await service.CreateAsync(primary.Id, request with { AccountTypeId = 9, BranchCode = "25-06" });
        Assert.Equal(["accountTypeId", "branchCode"], invalid.Error!.Errors!.Keys.Order());
    }

    [Fact]
    public async Task Active_bank_account_type_is_unique_in_the_database()
    {
        var primary = await CreatePrimaryAsync();
        await using var db = fixture.CreateClientContext();
        for (var i = 0; i < 2; i++)
            db.ClientBankAccounts.Add(new ClientBankAccount
            {
                PrimaryClientId = primary.Id, AccountTypeId = (byte)AccountTypeCode.Charges, AccountName = "Fees", BranchCode = "1", AccountNumber = "1"
            });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Api_key_is_shown_once_and_only_its_hash_is_stored()
    {
        var primary = await CreatePrimaryAsync();
        var secondary = await CreateSecondaryAsync(primary.Id);
        await using var db = fixture.CreateClientContext();
        var service = new ClientApiKeyService(db, TimeProvider.System);

        var created = (await service.CreateAsync(ClientKind.Secondary, secondary.Id, new CreateApiKeyRequest("Upload"))).Value!;
        Assert.StartsWith(ApiKeyHasher.KeyMarker, created.Key);
        Assert.Equal(created.Key[..ApiKeyHasher.PrefixLength], created.ApiKey.KeyPrefix);
        Assert.Equal(secondary.Id, created.ApiKey.SecondaryClientId);

        var stored = await db.ClientApiKeys.AsNoTracking().SingleAsync(k => k.Id == created.ApiKey.Id);
        Assert.Equal(ApiKeyHasher.Hash(created.Key), stored.KeyHash);
        Assert.Equal(32, stored.KeyHash.Length);

        var owner = await service.ValidateApiKeyAsync(created.Key);
        Assert.Equal(new ApiKeyOwner(ClientKind.Secondary, secondary.Id, secondary.UniqueClientId, created.ApiKey.Id), owner);
        Assert.Null(await service.ValidateApiKeyAsync(created.Key + "x"));
    }

    [Fact]
    public async Task Revoked_expired_and_inactive_client_keys_are_rejected()
    {
        var primary = await CreatePrimaryAsync();
        var secondary = await CreateSecondaryAsync(primary.Id);
        await using var db = fixture.CreateClientContext();
        var service = new ClientApiKeyService(db, TimeProvider.System);

        var revoked = (await service.CreateAsync(ClientKind.Primary, primary.Id, new CreateApiKeyRequest("Old"))).Value!;
        Assert.NotNull((await service.ValidateApiKeyAsync(revoked.Key))?.ClientId);
        Assert.NotNull((await service.RevokeAsync(revoked.ApiKey.Id)).Value!.RevokedDate);
        Assert.Null(await service.ValidateApiKeyAsync(revoked.Key));

        var expired = (await service.CreateAsync(ClientKind.Primary, primary.Id, new CreateApiKeyRequest("Short", DateTime.UtcNow.AddMinutes(5)))).Value!;
        await db.ClientApiKeys.Where(k => k.Id == expired.ApiKey.Id).ExecuteUpdateAsync(k => k.SetProperty(x => x.ExpiryDate, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Null(await service.ValidateApiKeyAsync(expired.Key));

        var ofSecondary = (await service.CreateAsync(ClientKind.Secondary, secondary.Id, new CreateApiKeyRequest("Upload"))).Value!;
        await new SecondaryClientService(db, TimeProvider.System).DeactivateAsync(secondary.Id);
        Assert.Null(await service.ValidateApiKeyAsync(ofSecondary.Key));
    }

    [Fact]
    public async Task Client_has_at_most_two_active_api_keys()
    {
        var primary = await CreatePrimaryAsync();
        await using var db = fixture.CreateClientContext();
        var service = new ClientApiKeyService(db, TimeProvider.System);

        var first = (await service.CreateAsync(ClientKind.Primary, primary.Id, new CreateApiKeyRequest("One"))).Value!;
        await service.CreateAsync(ClientKind.Primary, primary.Id, new CreateApiKeyRequest("Two"));
        Assert.Equal(ClientErrorKind.Conflict, (await service.CreateAsync(ClientKind.Primary, primary.Id, new CreateApiKeyRequest("Three"))).Error!.Kind);

        await service.RevokeAsync(first.ApiKey.Id);
        Assert.True((await service.CreateAsync(ClientKind.Primary, primary.Id, new CreateApiKeyRequest("Three"))).IsSuccess);
        Assert.Equal(3, (await service.ListAsync(ClientKind.Primary, primary.Id)).Value!.Count);

        var past = await service.CreateAsync(ClientKind.Primary, primary.Id, new CreateApiKeyRequest("Past", DateTime.UtcNow.AddDays(-1)));
        Assert.Equal(ClientErrorKind.Validation, past.Error!.Kind);
    }

    [Fact]
    public async Task Api_key_must_have_exactly_one_owner()
    {
        var primary = await CreatePrimaryAsync();
        var secondary = await CreateSecondaryAsync(primary.Id);
        await using var db = fixture.CreateClientContext();
        db.ClientApiKeys.Add(new ClientApiKey
        {
            PrimaryClientId = primary.Id, SecondaryClientId = secondary.Id, Name = "Both", KeyPrefix = "x", KeyHash = ApiKeyHasher.Hash(Guid.NewGuid().ToString())
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("CK_ClientApiKey_Owner", ex.InnerException!.Message);
    }
}
