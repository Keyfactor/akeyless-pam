// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

namespace Keyfactor.Tests.Integration;

/// <summary>
///     Loads variables from a .env file into the process environment, without overwriting
///     variables that are already set. Walks up the directory tree from the executing assembly
///     to find the repo-root .env file.
/// </summary>
internal static class DotEnvLoader
{
    internal static void Load()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                foreach (var line in File.ReadAllLines(candidate))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;

                    // Strip optional leading "export "
                    if (trimmed.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
                        trimmed = trimmed["export ".Length..].TrimStart();

                    var eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;

                    var key = trimmed[..eq].Trim();
                    var value = trimmed[(eq + 1)..].Trim().Trim('"');

                    // Only set if not already present in the environment
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
                        Environment.SetEnvironmentVariable(key, value);
                }
                return;
            }
            dir = dir.Parent;
        }
    }
}
