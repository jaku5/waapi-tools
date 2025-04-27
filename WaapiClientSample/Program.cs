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

using Newtonsoft.Json.Linq;
using System;
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
                AK.Wwise.Waapi.JsonClient client = new AK.Wwise.Waapi.JsonClient();

                // Try to connect to running instance of Wwise on localhost, default port
                await client.Connect();

                // Register for connection lost event
                client.Disconnected += () =>
                {
                    Console.WriteLine("We lost connection!");
                };

                // Gat all actor-mixers canditates in the project
                var query = new JObject(
                        new JProperty("waql", "$ from type actormixer where parent.type:\"actormixer\""));

                var options = new JObject(
                    new JProperty("return", new string[] { "name", "id", "path", "parent.name", "parent.id" }));

                JObject result = await client.Call(ak.wwise.core.@object.get, query, options);

                // Check for actor mixers diff against their parent
                if (result["return"] is JArray resultsArray && resultsArray.Any())
                {
                    var actorsToConvert = new JArray();

                    foreach (var actor in result["return"])
                    {
                        Console.WriteLine($"\nProcessing: {actor["name"]} (ID: {actor["id"]})");
                        JObject diff = await client.Call(ak.wwise.core.@object.diff,
                                                        new JObject(
                                                            new JProperty("source", actor["id"]),
                                                            new JProperty("target", actor["parent.id"])),
                                                        null);

                        if (diff["properties"] is JArray diffArray && !diffArray.Any())
                        {
                            actorsToConvert.Add(new JObject(
                                new JProperty("id", actor["id"]),
                                new JProperty("name", actor["name"]),
                                new JProperty("path", actor["path"]),
                                new JProperty("parent.id", actor["parent.id"]),
                                new JProperty("parent.name", actor["parent.name"])));
                        }
                    }
                    Console.WriteLine($"Actors to convert: {actorsToConvert}");
                    Console.WriteLine("Would you like to convert these actors to virtual folders? (y/n)");

                    string userInput;

                    userInput = Console.ReadLine();

                    if (userInput.ToLower() == "y")
                    {
                        // Create a folder for each actor
                        foreach (var actor in actorsToConvert)
                        {
                            var tempName = $"{actor["name"]}Temp";
                            Console.WriteLine($"Converting: {actor}");
                            await client.Call(ak.wwise.core.@object.create, new JObject(
                                new JProperty("parent", actor["parent.id"]),
                                new JProperty("type", "Folder"),
                                new JProperty("name", tempName )));
                        }

                        // Get children of the actor
                        var childrenToMove = new JArray();

                        foreach (var actor in actorsToConvert)
                        {
                            var actorPath = $"\"{actor["path"].ToString().Replace("\\\\", "\\")}\"";
                            var folderPath = $"{actor["path"].ToString().Replace("\\\\", "\\")}Temp";

                            Console.WriteLine($"Moving children of: {actor}");

                            var queryChildren = new JObject(
                                new JProperty("waql", $"$ {actorPath} select children"));

                            JObject resultChildren = await client.Call(ak.wwise.core.@object.get, queryChildren);

                            Console.WriteLine($"Child: {resultChildren}");

                            // Copy the children to the new folder
                            if (resultChildren["return"] is JArray resultsArrayChildren && resultsArrayChildren.Any())
                            {
                                foreach (var child in resultChildren["return"])
                                {
                                    Console.WriteLine($"\nMoving: {child["name"]} (ID: {child["id"]})");

                                    await client.Call(ak.wwise.core.@object.move, new JObject(
                                        new JProperty("object", child["id"]),
                                        new JProperty("parent", folderPath)));
                                }
                            }
                            // Delete the actor
                            Console.WriteLine($"Deleting: {actor}");
                            await client.Call(ak.wwise.core.@object.delete, new JObject(
                                 new JProperty("object", actor["id"])));
                            
                            // Rename the folder
                            await client.Call(ak.wwise.core.@object.setName, new JObject(
                                new JProperty("object", folderPath),
                                new JProperty("value", actor["name"])));
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
                }

                Console.WriteLine("Done. Press any key to exit.");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }
    }
}
