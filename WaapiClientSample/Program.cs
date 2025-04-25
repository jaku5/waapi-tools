/******************************************************************************

The content of this file includes portions of the AUDIOKINETIC Wwise Technology
released in source code form as part of the SDK installer package.

Commercial License Usage

Licensees holding valid commercial licenses to the AUDIOKINETIC Wwise Technology
may use this file in accordance with the end user license agreement provided 
with the software or, alternatively, in accordance with the terms contained in a
written agreement between you and Audiokinetic Inc.

Apache License Usage

Alternatively, this file may be used under the Apache License, Version 2.0 (the 
"Apache License"); you may not use this file except in compliance with the 
Apache License. You may obtain a copy of the Apache License at 
http://www.apache.org/licenses/LICENSE-2.0.

Unless required by applicable law or agreed to in writing, software distributed
under the Apache License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES
OR CONDITIONS OF ANY KIND, either express or implied. See the Apache License for
the specific language governing permissions and limitations under the License.

  Copyright (c) 2020 Audiokinetic Inc.

*******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AK.Wwise.Waapi
{
    class Program
    {
        static void Main(string[] args)
        {
            _Main().Wait();
        }

        static async Task _Main()
        {
            try
            {
                AK.Wwise.Waapi.Client client = new AK.Wwise.Waapi.Client();

                // Try to connect to running instance of Wwise on localhost, default port
                await client.Connect();

                // Register for connection lost event
                client.Disconnected += () =>
                {
                    System.Console.WriteLine("We lost connection!");
                };

                // Simple RPC call
                string info = await client.Call(ak.wwise.core.getInfo, null, null);
                System.Console.WriteLine(info);
                // JObject profilerStart = await client.Call(ak.wwise.core.profiler.startCapture);

                // Define all properties we want to check and potentially reset
                var propertiesToReset = new Dictionary<string, object>
            {
                { "OverridePositioning", false },
                { "OverrideEffect", false }
                // Add more properties as needed
            };

                // Build WAQL query dynamically
                var conditions = string.Join(" or ", propertiesToReset.Keys.Select(p => $"{p} = true"));

                // Properly format the query as a JSON string
                string query = $"{{ \"waql\": \"$ from type actormixer where {conditions}\" }}";

                // Properly format the options as a JSON string
                var options = $"{{ \"return\": [\"name\", \"id\"] }}";

                string result = await client.Call(ak.wwise.core.@object.get, query, options);
                System.Console.WriteLine(result);

                /*if (result["return"] is JArray resultsArray && resultsArray.Any())
                {
                    string userInput;

                    Console.WriteLine("Would you like to reset overrides? (y/n)");
                    userInput = Console.ReadLine();
                    if (userInput.ToLower() == "y")
                    {

                        foreach (var actor in result["return"])
                        {
                            Console.WriteLine($"\nProcessing: {actor["name"]} (ID: {actor["id"]})");

                            foreach (var property in propertiesToReset)
                            {
                                var propertyName = property.Key;
                                var propertyValue = property.Value;

                                await client.Call(ak.wwise.core.@object.setProperty,
                                    new JObject(
                                        new JProperty("property", propertyName),
                                        new JProperty("object", actor["id"]),
                                        new JProperty("value", propertyValue)),
                                    null);
                            }
                        }

                    }
                    else
                    {
                        Console.WriteLine("User cancelled.");
                    }
                }
                else
                {
                    Console.WriteLine("No actor mixers to sanitize found. Good job!");
                }*/

                System.Console.WriteLine("Done.");
            }
            catch (Exception e)
            {
                System.Console.Error.WriteLine(e.Message);
            }
        }
    }
}
