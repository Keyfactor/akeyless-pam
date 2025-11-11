// Copyright 2023 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the specific language governing permissions
// and limitations under the License.

using Keyfactor.Extensions.Pam.Akeyless;

namespace TestConsole;

internal class Program
{
    private static void Main(string[] args)
    {
        var pam = new AkeylessPam();
        var initInfo = new Dictionary<string, string>();

        var instanceParams = new Dictionary<string, string>();

        //Read Url from environment variable
        initInfo.Add("Url",
            Environment.GetEnvironmentVariable("AKEYLESS_API_URL") ?? "https://api.akeyless.io");
        //Read Username from environment variable
        initInfo.Add("AuthType", Environment.GetEnvironmentVariable("AKEYLESS_AUTH_TYPE") ?? "access_key");

        switch (initInfo["AuthType"])
        {
            case "access_key":
                initInfo.Add("AccessId", Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_ID") ?? "changemeId!");
                initInfo.Add("AccessKey", Environment.GetEnvironmentVariable("AKEYLESS_ACCESS_KEY") ?? "changemeKey!");
                break;
            default:
                Console.WriteLine("Using implicit authentication.");
                break;
        }

        Console.WriteLine("Test secret type `static_text`:");
        instanceParams.Add("SecretType", "static_text");
        instanceParams.Add("SecretName", "pam/test/pamStaticTextUsername");
        var username = pam.GetPassword(instanceParams, initInfo);
        instanceParams["SecretName"] = "pam/test/pamStaticTextPassword";
        var password = pam.GetPassword(instanceParams, initInfo);
        Console.WriteLine($"ServerUsername: {username}");
        Console.WriteLine($"ServerPassword: {password}");
        Console.WriteLine();

        Console.WriteLine("Test secret type `static_kv`:");
        instanceParams["SecretType"] = "static_kv";
        instanceParams["SecretName"] = "pam/test/pamStaticKV";
        instanceParams["StaticSecretFieldName"] = "username";
        var kvUsername = pam.GetPassword(instanceParams, initInfo);
        instanceParams["StaticSecretFieldName"] = "password";
        var kvPassword = pam.GetPassword(instanceParams, initInfo);

        Console.WriteLine($"ServerUsername: {kvUsername}");
        Console.WriteLine($"ServerPassword: {kvPassword}");

        Console.WriteLine();
        Console.WriteLine("Test secret type `static_json`:");
        instanceParams["SecretType"] = "static_json";
        instanceParams["SecretName"] = "pam/test/pamStaticJSON";
        instanceParams["StaticSecretFieldName"] = "username";
        var jsonUsername = pam.GetPassword(instanceParams, initInfo);
        instanceParams["StaticSecretFieldName"] = "password";
        var jsonPassword = pam.GetPassword(instanceParams, initInfo);
        Console.WriteLine($"ServerUsername: {jsonUsername}");
        Console.WriteLine($"ServerPassword: {jsonPassword}");

        Console.WriteLine();
        Console.WriteLine("Test secret type `static_json` raw:");
        instanceParams["SecretType"] = "static_json";
        instanceParams["SecretName"] = "pam/test/k8s-orchestrator";
        instanceParams.Remove("StaticSecretFieldName");
        var jsonRaw = pam.GetPassword(instanceParams, initInfo);
        Console.WriteLine($"ServerSecret: {jsonRaw}");

        Console.WriteLine("Test completed.");
    }
}