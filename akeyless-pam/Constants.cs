namespace Keyfactor.Extensions.Pam.Akeyless;

public static class AkeylessConstants
{
    // Compile-time constant (gets inlined into referencing assemblies)
    public const string DefaultAuthMethod = "access_key";

    public const string DefaultAkeylessApiUrl = "https://api.akeyless.io";

    // Recommended for libraries: avoids inlining so you can change value without recompiling dependents
    public static readonly string DefaultAuthMethodReadOnly = "access_key";
}