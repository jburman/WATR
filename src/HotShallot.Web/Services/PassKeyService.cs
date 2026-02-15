using Fido2NetLib;
using Fido2NetLib.Objects;
using HotShallot.Shared.Models;
using HotShallot.Web.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HotShallot.Web.Services;

public class PassKeyService
{
    private readonly IFido2 _fido2;
    private readonly HotShallotDbContext _db;

    public PassKeyService(IFido2 fido2, HotShallotDbContext db)
    {
        _fido2 = fido2;
        _db = db;
    }

    public CredentialCreateOptions MakeCredentialOptions(User user, List<PublicKeyCredentialDescriptor> existingKeys)
    {
        var authenticatorSelection = new AuthenticatorSelection
        {
            ResidentKey = ResidentKeyRequirement.Required,
            UserVerification = UserVerificationRequirement.Preferred
        };

        var fidoUser = new Fido2User
        {
            DisplayName = user.DisplayName,
            Name = user.Email,
            Id = Encoding.UTF8.GetBytes(user.Email)
        };

        return _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = existingKeys,
            AuthenticatorSelection = authenticatorSelection,
            AttestationPreference = AttestationConveyancePreference.None
        });
    }

    public async Task<UserPassKey> CreatePassKeyAsync(User user, AuthenticatorAttestationRawResponse response, CredentialCreateOptions options)
    {
        var success = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = response,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = async (args, cancellationToken) => true
        });

        if (success == null)
        {
            throw new Exception("Failed to verify credential");
        }

        var passKey = new UserPassKey
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CredentialId = success.Id,
            PublicKey = success.PublicKey,
            SignatureCounter = success.SignCount,
            AaGuid = success.AaGuid
        };

        _db.UserPassKeys.Add(passKey);
        await _db.SaveChangesAsync();

        return passKey;
    }

    public async Task<AssertionOptions> MakeAssertionOptionsAsync(string email)
    {
        var user = await _db.Users
            .Include(u => u.PassKeys)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            throw new Exception("User not found");
        }

        var existingCredentials = user.PassKeys
            .Select(pk => new PublicKeyCredentialDescriptor(pk.CredentialId))
            .ToList();

        return _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = existingCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });
    }

    public async Task<User> VerifyAssertionAsync(AuthenticatorAssertionRawResponse response, AssertionOptions options)
    {
        byte[] credentialId = response.RawId;
        
        var passKey = await _db.UserPassKeys
            .Include(pk => pk.User)
            .FirstOrDefaultAsync(pk => pk.CredentialId == credentialId);

        if (passKey == null || passKey.User == null)
        {
            throw new Exception("Credential not found");
        }

        var success = await _fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = response,
            OriginalOptions = options,
            StoredPublicKey = passKey.PublicKey,
            StoredSignatureCounter = passKey.SignatureCounter,
            IsUserHandleOwnerOfCredentialIdCallback = async (args, cancellationToken) => true
        });

        if (success == null)
        {
            throw new Exception("Failed to verify assertion");
        }

        // Update counter
        passKey.SignatureCounter = success.SignCount;
        await _db.SaveChangesAsync();

        return passKey.User;
    }
}
