// Copyright 2025 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using akeyless.Api;
using akeyless.Client;
using akeyless.Model;

namespace Keyfactor.Extensions.Pam.Akeyless;

internal class AkeylessApiClient : IAkeylessApiClient
{
    private readonly V2Api _api;

    internal AkeylessApiClient(string basePath)
    {
        var config = new Configuration { BasePath = basePath };
        _api = new V2Api(config);
    }

    public string Authenticate(string accessId, string accessKey)
    {
        var authResp = _api.Auth(new Auth(accessId, accessKey));
        if (string.IsNullOrEmpty(authResp.Token) && string.IsNullOrEmpty(authResp.Creds?.Token))
            return string.Empty;
        return string.IsNullOrEmpty(authResp.Token) ? authResp.Creds.Token : authResp.Token;
    }

    public async Task<Dictionary<string, string>> GetSecretValuesAsync(IEnumerable<string> names, string token)
    {
        var nameList = names.ToList();
        var req = new GetSecretValue(names: nameList, token: token);
        var result = await _api.GetSecretValueAsync(req);
        return result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);
    }
}
