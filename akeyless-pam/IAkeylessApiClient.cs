// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Keyfactor.Extensions.Pam.Akeyless;

public interface IAkeylessApiClient
{
    /// <summary>
    ///     Authenticates to Akeyless using the provided access key credentials.
    /// </summary>
    /// <returns>The auth token, or empty string if authentication failed.</returns>
    string Authenticate(string accessId, string accessKey);

    /// <summary>
    ///     Retrieves secret values by name from Akeyless.
    /// </summary>
    Task<Dictionary<string, string>> GetSecretValuesAsync(IEnumerable<string> names, string token);
}
