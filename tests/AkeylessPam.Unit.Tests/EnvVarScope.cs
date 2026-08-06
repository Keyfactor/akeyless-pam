/// <summary>
/// Sets an environment variable for the duration of a test and restores the prior value (or clears it,
/// if it was previously unset) on dispose. Ensures env var overrides used to test one PAM instance
/// don't leak into other tests or other test runs.
/// </summary>
internal sealed class EnvVarScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previousValue;

    public EnvVarScope(string name, string? value)
    {
        _name = name;
        _previousValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, _previousValue);
    }
}
