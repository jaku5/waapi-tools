using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using JPAudio.WaapiTools.ClientJson;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core.Models;
using System;

namespace JPAudio.WaapiTools.Tool.ActormixerSanitizer.Core.Tests
{
  public class ActormixerSanitizerServiceTests
  {
    private Mock<IJsonClient>? _clientMock;
    private Mock<ILogger<ActormixerSanitizerService>>? _loggerMock;
    private ActormixerSanitizerService? _service;

    private void SetupTest()
    {
      _clientMock = new Mock<IJsonClient>();
      _loggerMock = new Mock<ILogger<ActormixerSanitizerService>>();
      _service = new ActormixerSanitizerService(_clientMock.Object, _loggerMock.Object);
    }

    private void SetupDefaultMocks()
    {
      if (_clientMock == null) return;

      var undoGroupResult = new JObject();
      var undoEndResult = new JObject();
      var undoLastResult = new JObject();

      _clientMock.Setup(c => c.Call(ak.wwise.core.undo.beginGroup, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
        .ReturnsAsync(undoGroupResult);
      _clientMock.Setup(c => c.Call(ak.wwise.core.undo.endGroup, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
    .ReturnsAsync(undoEndResult);
      _clientMock.Setup(c => c.Call(ak.wwise.core.undo.undoLast, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
        .ReturnsAsync(undoLastResult);
    }

    private void SetupWaapiCallsForTest(JObject actorMixersResult, JObject ancestorResult, JObject diffResult, JObject rtpcResult, JObject referencesResult, JObject stateGroupResult)
    {
      if (_clientMock == null) return;

      _clientMock.Reset();

      _clientMock
.Setup(c => c.Call(ak.wwise.core.@object.get, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
   .Returns<string, JObject, JObject, int>((uri, args, options, timeout) =>
 {
   var waql = args?["waql"]?.Value<string>() ?? "";

   if (waql.Contains("from type actormixer") || waql.Contains("from type PropertyContainer"))
     return Task.FromResult(actorMixersResult);
   else if (waql.Contains("select ancestors.first"))
     return Task.FromResult(ancestorResult);
   else if (waql.Contains("rtpc.any"))
     return Task.FromResult(rtpcResult);
   else if (waql.Contains("select referencesTo"))
     return Task.FromResult(referencesResult);
   else if (waql.Contains("from type stateGroup"))
     return Task.FromResult(stateGroupResult);

   return Task.FromResult(new JObject(new JProperty("return", new JArray())));
 });

      _clientMock
   .Setup(c => c.Call(ak.wwise.core.@object.diff, It.IsAny<JObject>(), It.IsAny<JObject>(), It.IsAny<int>()))
    .ReturnsAsync(diffResult);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenWaapiReturnsOneSanitizableMixer_ShouldReturnOneMixer()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      var actorMixerId = "{A5A7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var actorMixerName = "MyTestActorMixer";
      var actorMixerPath = "\\Actor-Mixer Hierarchy\\Default Work Unit\\MyTestActorMixer";
      var parentId = "{C5C7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var ancestorId = "{D5D7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var ancestorName = "ParentActorMixer";

      var actorMixersResult = new JObject(
    new JProperty("return", new JArray(
new JObject(
  new JProperty("id", actorMixerId),
new JProperty("name", actorMixerName),
new JProperty("path", actorMixerPath),
new JProperty("parent.id", parentId),
new JProperty("notes", "Some notes"),
    new JProperty("Volume", 0),
       new JProperty("Pitch", 0),
new JProperty("Lowpass", 0),
 new JProperty("Highpass", 0),
      new JProperty("MakeUpGain", 0)
              )
   ))
);
      var ancestorResult = new JObject(
         new JProperty("return", new JArray(
    new JObject(
        new JProperty("id", ancestorId),
              new JProperty("name", ancestorName)
            )
           ))
              );
      var diffResult = new JObject(new JProperty("properties", new JArray()));
      var rtpcResult = new JObject(new JProperty("return", new JArray()));
      var referencesResult = new JObject(new JProperty("return", new JArray()));
      var stateGroupResult = new JObject(new JProperty("return", new JArray()));

      SetupWaapiCallsForTest(actorMixersResult, ancestorResult, diffResult, rtpcResult, referencesResult, stateGroupResult);

      // Act
      var result = await _service.GetSanitizableMixersAsync();

      // Assert
      Assert.Single(result);
      var mixerInfo = result.First();
      Assert.Equal(actorMixerId, mixerInfo.Id);
      Assert.Equal(actorMixerName, mixerInfo.Name);
      Assert.Equal(actorMixerPath, mixerInfo.Path);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenMixerHasActiveRtpc_ShouldReturnEmptyList()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      var actorMixerId = "{A5A7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var actorMixerName = "MyTestActorMixer";
      var actorMixerPath = "\\Actor-Mixer Hierarchy\\Default Work Unit\\MyTestActorMixer";
      var parentId = "{C5C7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var ancestorId = "{D5D7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var ancestorName = "ParentActorMixer";

      var actorMixersResult = new JObject(
     new JProperty("return", new JArray(
new JObject(
new JProperty("id", actorMixerId),
new JProperty("name", actorMixerName),
new JProperty("path", actorMixerPath),
new JProperty("parent.id", parentId),
new JProperty("notes", "Some notes"),
    new JProperty("Volume", 0),
new JProperty("Pitch", 0),
new JProperty("Lowpass", 0),
new JProperty("Highpass", 0),
new JProperty("MakeUpGain", 0)
)
))
);
      var ancestorResult = new JObject(
         new JProperty("return", new JArray(
              new JObject(
              new JProperty("id", ancestorId),
       new JProperty("name", ancestorName)
            )
               ))
          );
      var diffResult = new JObject(new JProperty("properties", new JArray()));
      var rtpcResult = new JObject(new JProperty("return", new JArray(new JObject(new JProperty("id", actorMixerId)))));
      var referencesResult = new JObject(new JProperty("return", new JArray()));
      var stateGroupResult = new JObject(new JProperty("return", new JArray()));

      SetupWaapiCallsForTest(actorMixersResult, ancestorResult, diffResult, rtpcResult, referencesResult, stateGroupResult);

      // Act
      var result = await _service.GetSanitizableMixersAsync();

      // Assert
      Assert.Empty(result);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenMixerIsReferencedByEvent_ShouldReturnEmptyList()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      var actorMixerId = "{A5A7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var actorMixerName = "MyTestActorMixer";
      var actorMixerPath = "\\Actor-Mixer Hierarchy\\Default Work Unit\\MyTestActorMixer";
      var parentId = "{C5C7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var ancestorId = "{D5D7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var ancestorName = "ParentActorMixer";

      var actorMixersResult = new JObject(
         new JProperty("return", new JArray(
           new JObject(
       new JProperty("id", actorMixerId),
       new JProperty("name", actorMixerName),
                   new JProperty("path", actorMixerPath),
    new JProperty("parent.id", parentId),
           new JProperty("notes", "Some notes"),
        new JProperty("Volume", 0),
    new JProperty("Pitch", 0),
      new JProperty("Lowpass", 0),
          new JProperty("Highpass", 0),
      new JProperty("MakeUpGain", 0)
       )
     ))
              );
      var ancestorResult = new JObject(
      new JProperty("return", new JArray(
    new JObject(
new JProperty("id", ancestorId),
       new JProperty("name", ancestorName)
      )
 ))
);
      var diffResult = new JObject(new JProperty("properties", new JArray()));
      var rtpcResult = new JObject(new JProperty("return", new JArray()));
      var referencesResult = new JObject(new JProperty("return", new JArray(new JObject(new JProperty("id", "{E5E7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}")))));
      var stateGroupResult = new JObject(new JProperty("return", new JArray()));

      SetupWaapiCallsForTest(actorMixersResult, ancestorResult, diffResult, rtpcResult, referencesResult, stateGroupResult);

      // Act
      var result = await _service.GetSanitizableMixersAsync();

      // Assert
      Assert.Empty(result);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenNoActorsFound_ReturnsNull()
    {
      // Arrange: when no actor-mixers/property containers exist at all, service returns null (edge case 1)
      SetupTest();
      SetupDefaultMocks();

      var emptyResult = new JObject(new JProperty("return", new JArray()));
      var ancestorResult = new JObject(new JProperty("return", new JArray()));
      var stateGroupResult = new JObject(new JProperty("return", new JArray()));

      SetupWaapiCallsForTest(emptyResult, ancestorResult, new JObject(new JProperty("properties", new JArray())),
                     new JObject(new JProperty("return", new JArray())),
         new JObject(new JProperty("return", new JArray())),
           stateGroupResult);

      // Act
      var result = await _service!.GetSanitizableMixersAsync();

      // Assert: null signals "no objects of this type exist in the project at all"
      Assert.Null(result);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenAncestorLookupFails_SkipsActor()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      var actorMixerId = "{A5A7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}";
      var actorMixersResult = new JObject(
        new JProperty("return", new JArray(
          new JObject(
            new JProperty("id", actorMixerId),
            new JProperty("name", "TestMixer"),
            new JProperty("path", "\\Test\\TestMixer"),
            new JProperty("parent.id", "{C5C7B1B1-1B1B-1B1B-1B1B-1B1B1B1B1B1B}"),
            new JProperty("notes", ""),
            new JProperty("Volume", 0),
            new JProperty("Pitch", 0),
            new JProperty("Lowpass", 0),
            new JProperty("Highpass", 0),
            new JProperty("MakeUpGain", 0)))));

      // Ancestor returns empty - simulates lookup failure
      var emptyAncestorResult = new JObject(new JProperty("return", new JArray()));
      var stateGroupResult = new JObject(new JProperty("return", new JArray()));

      SetupWaapiCallsForTest(actorMixersResult, emptyAncestorResult,
      new JObject(new JProperty("properties", new JArray())),
      new JObject(new JProperty("return", new JArray())),
      new JObject(new JProperty("return", new JArray())),
      stateGroupResult);

      // Act
      var result = await _service!.GetSanitizableMixersAsync();

      // Assert - actor should be skipped due to no ancestor
      Assert.Empty(result);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenMultipleMixersWithMixedCriteria_FiltersProperly()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      var sanitizableId = "{A1A1A1A1-A1A1-A1A1-A1A1-A1A1A1A1A1A1}";
      var rtpcId = "{B2B2B2B2-B2B2-B2B2-B2B2-B2B2B2B2B2B2}";
      var referenceId = "{C3C3C3C3-C3C3-C3C3-C3C3-C3C3C3C3C3C3}";

      var actorMixersResult = new JObject(
        new JProperty("return", new JArray(
          // Sanitizable mixer
          new JObject(
            new JProperty("id", sanitizableId),
            new JProperty("name", "SanitizableMixer"),
            new JProperty("path", "\\Test\\Sanitizable"),
            new JProperty("parent.id", "{Parent1}"),
            new JProperty("notes", ""),
            new JProperty("Volume", 0),
            new JProperty("Pitch", 0),
            new JProperty("Lowpass", 0),
            new JProperty("Highpass", 0),
            new JProperty("MakeUpGain", 0)),
          // Mixer with RTPC
          new JObject(
            new JProperty("id", rtpcId),
            new JProperty("name", "RtpcMixer"),
            new JProperty("path", "\\Test\\Rtpc"),
            new JProperty("parent.id", "{Parent2}"),
            new JProperty("notes", ""),
            new JProperty("Volume", 0),
            new JProperty("Pitch", 0),
            new JProperty("Lowpass", 0),
            new JProperty("Highpass", 0),
            new JProperty("MakeUpGain", 0)
             ),
          // Mixer with references
          new JObject(
            new JProperty("id", referenceId),
            new JProperty("name", "ReferencedMixer"),
            new JProperty("path", "\\Test\\Referenced"),
            new JProperty("parent.id", "{Parent3}"),
            new JProperty("notes", ""),
            new JProperty("Volume", 0),
            new JProperty("Pitch", 0),
            new JProperty("Lowpass", 0),
            new JProperty("Highpass", 0),
            new JProperty("MakeUpGain", 0)))));

      var ancestorResult = new JObject(
        new JProperty("return", new JArray(
          new JObject(new JProperty("id", "{Ancestor1}"), new JProperty("name", "Ancestor")))));

      var diffResult = new JObject(new JProperty("properties", new JArray()));
      var rtpcResult = new JObject(new JProperty("return", new JArray(
         new JObject(new JProperty("id", rtpcId)))));

      var referencesResult = new JObject(new JProperty("return", new JArray(
        new JObject(new JProperty("id", "{EventAction1}")))));

      var stateGroupResult = new JObject(new JProperty("return", new JArray()));

      SetupWaapiCallsForTest(actorMixersResult, ancestorResult, diffResult, rtpcResult, referencesResult, stateGroupResult);

      // Act
      var result = await _service!.GetSanitizableMixersAsync();

      // Assert - all three should be filtered due to criteria issues
      // After analysis of the filter logic, actors with RTPC and references should be excluded
      Assert.Empty(result);
    }

    [Fact]
    public async Task ConnectAsync_WhenCalled_ResetsIsScanned()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      // Act
      await _service!.ConnectAsync();

      // Assert
      Assert.False(_service.IsScanned);
    }

    [Fact]
    public void Disconnect_WhenCalled_SetsConnectionLost()
    {
      // Arrange
      SetupTest();

      // Act
      _service!.Disconnect();

      // Assert
      Assert.True(_service.IsConnectionLost);
    }

    [Fact]
    public async Task ConvertToFoldersAsync_WhenCalled_PerformsConversionSteps()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      var childId = "{CHILD-1}";
      SetupConversionMocks(new[] { childId });

      var actors = new List<ActorMixerInfo>
      {
        new ActorMixerInfo
        {
            Id = "{AM-1}",
            Name = "Mixer1",
            Path = "\\Actor-Mixer Hierarchy\\Default Work Unit\\Mixer1",
            Notes = "Note1"
        }
      };

      // Act
      await _service!.ConvertToFoldersAsync(actors);

      // Assert
      // 1. Begin Group - Target Overload 1 (object)
      _clientMock!.Verify(c => c.Call(ak.wwise.core.undo.beginGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);

      // 2. Create Folder - Check properties on JObject cast
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.create,
          It.Is<object>(o => o is JObject && ((JObject)o)["type"]!.ToString() == "Folder" && ((JObject)o)["name"]!.ToString() == "Mixer1Temp"),
          It.IsAny<object>(), It.IsAny<int>()), Times.Once);

      // 3. Move Children
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.move,
          It.Is<object>(o => o is JObject && ((JObject)o)["object"]!.ToString() == childId),
          It.IsAny<object>(), It.IsAny<int>()), Times.Once);

      // 4. Delete Original
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.delete,
          It.Is<object>(o => o is JObject && ((JObject)o)["object"]!.ToString() == "{AM-1}"),
          It.IsAny<object>(), It.IsAny<int>()), Times.Once);

      // 5. Rename Folder
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.setName,
          It.Is<object>(o => o is JObject && ((JObject)o)["value"]!.ToString() == "Mixer1"),
          It.IsAny<object>(), It.IsAny<int>()), Times.Once);

      // 6. End Group
      _clientMock.Verify(c => c.Call(ak.wwise.core.undo.endGroup,
          It.Is<object>(o => o is JObject && ((JObject)o)["displayName"] != null),
          It.IsAny<object>(), It.IsAny<int>()), Times.Once);

      // 7. State
      Assert.True(_service.IsConverted);
      Assert.False(_service.IsConverting);
    }

    [Fact]
    public async Task ConvertToFoldersAsync_WhenActorHasNoChildren_SkipsMove()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();
      SetupConversionMocks(null); // No children

      var actors = new List<ActorMixerInfo>
      {
        new ActorMixerInfo { Id = "{AM-1}", Name = "Mixer1", Path = "\\Mixer1" }
      };

      // Act
      await _service!.ConvertToFoldersAsync(actors);

      // Assert
      // Move should never be called
      _clientMock!.Verify(c => c.Call(ak.wwise.core.@object.move, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Never);

      // Other steps should still happen
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.delete,
          It.Is<object>(o => o is JObject && ((JObject)o)["object"]!.ToString() == "{AM-1}"),
          It.IsAny<object>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ConvertToFoldersAsync_WhenWaapiFails_Throws()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      if (_clientMock != null)
      {
        // Setup beginGroups to succeed - Target Overload 1
        _clientMock.Setup(c => c.Call(ak.wwise.core.undo.beginGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
           .ReturnsAsync(new JObject());

        // Make Create throw
        _clientMock.Setup(c => c.Call(ak.wwise.core.@object.create, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
            .ThrowsAsync(new System.Exception("WAAPI Error"));
      }

      var actors = new List<ActorMixerInfo>
      {
        new ActorMixerInfo { Id = "{AM-1}", Name = "Mixer1", Path = "\\Mixer1" }
      };

      // Act & Assert
      await Assert.ThrowsAsync<System.Exception>(() => _service!.ConvertToFoldersAsync(actors));
    }

    private void SetupConversionMocks(string[]? childIds = null)
    {
      if (_clientMock == null) return;

      // Create returns a mock ID - Target Overload 1
      _clientMock.Setup(c => c.Call(ak.wwise.core.@object.create, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
         .ReturnsAsync(new JObject(new JProperty("id", "{NEW-FOLDER-ID}")));

      // Get Children - Target Overload 2 (Call(string, JObject, JObject, int)) used by QueryWaapiAsync
      _clientMock.Setup(c => c.Call(ak.wwise.core.@object.get,
          It.IsAny<JObject>(),
          It.IsAny<JObject>(),
          It.IsAny<int>()))
         .Returns<string, JObject, JObject, int>((uri, args, options, timeout) =>
         {
           // Check if this is the "children" query
           if (args != null && args["waql"] != null && args["waql"]!.ToString().Contains("select children"))
           {
             var children = new JArray();
             if (childIds != null)
             {
               foreach (var id in childIds)
               {
                 children.Add(new JObject(new JProperty("id", id), new JProperty("name", "Child")));
               }
             }
             return Task.FromResult(new JObject(new JProperty("return", children)));
           }
           return Task.FromResult(new JObject(new JProperty("return", new JArray())));
         });

      // Target Overload 1 (Call(string, object, object, int)) - Safety fallback or if implementation changes
      _clientMock.Setup(c => c.Call(ak.wwise.core.@object.get,
          It.IsAny<object>(),
          It.IsAny<object>(),
          It.IsAny<int>()))
         .Returns<string, object, object, int>((uri, args, options, timeout) =>
         {
           // Similar logic for fallback
           var jArgs = args as JObject;
           if (jArgs != null && jArgs["waql"] != null && jArgs["waql"]!.ToString().Contains("select children"))
           {
             var children = new JArray();
             if (childIds != null)
             {
               foreach (var id in childIds)
               {
                 children.Add(new JObject(new JProperty("id", id), new JProperty("name", "Child")));
               }
             }
             return Task.FromResult(new JObject(new JProperty("return", children)));
           }
           return Task.FromResult(new JObject(new JProperty("return", new JArray())));
         });

      // Other modification calls matches simple object (Overload 1)
      var success = new JObject();
      _clientMock.Setup(c => c.Call(ak.wwise.core.@object.move, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>())).ReturnsAsync(success);
      _clientMock.Setup(c => c.Call(ak.wwise.core.@object.delete, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>())).ReturnsAsync(success);
      _clientMock.Setup(c => c.Call(ak.wwise.core.@object.setName, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>())).ReturnsAsync(success);
    }

    [Fact]
    public async Task ConvertToFoldersAsync_WhenCancelled_RollsBackChanges()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      var cts = new System.Threading.CancellationTokenSource();
      cts.Cancel(); // Cancel immediately

      var actors = new List<ActorMixerInfo>
      {
        new ActorMixerInfo
        {
            Id = "{AM-1}",
            Name = "Mixer1",
            Path = "\\Actor-Mixer Hierarchy\\Default Work Unit\\Mixer1",
            Notes = "Note1"
        }
      };

      // Act & Assert
      await Assert.ThrowsAsync<OperationCanceledException>(() => _service!.ConvertToFoldersAsync(actors, null, cts.Token));

      // Verify rollback occurred: endGroup then undoLast
      _clientMock!.Verify(c => c.Call(ak.wwise.core.undo.endGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);
      _clientMock!.Verify(c => c.Call(ak.wwise.core.undo.undoLast, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ConnectAsync_WhenWwise2025_DetectsYearAndNomenclature()
    {
      // Arrange
      SetupTest();
      
      var projectInfo = new JObject(new JProperty("name", "TestProject"));
      var wwiseInfo = new JObject(
          new JProperty("version", new JObject(
              new JProperty("displayName", "v2025.1.0"),
              new JProperty("year", 2025),
              new JProperty("major", 1)
          ))
      );

      _clientMock!.Setup(c => c.Call(ak.wwise.core.getProjectInfo, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
          .ReturnsAsync(projectInfo);
      _clientMock!.Setup(c => c.Call(ak.wwise.core.getInfo, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
          .ReturnsAsync(wwiseInfo);

      // Act
      await _service!.ConnectAsync();

      // Assert
      Assert.Equal("Property Container", _service.ActorMixerName);
      Assert.Equal("Property Containers", _service.ActorMixerNamePlural);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenWwise2025_UsesPropertyContainerWAQL()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      // Connect as 2025
      var wwiseInfo = new JObject(new JProperty("version", new JObject(new JProperty("year", 2025))));
      _clientMock!.Setup(c => c.Call(ak.wwise.core.getInfo, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
          .ReturnsAsync(wwiseInfo);
      await _service!.ConnectAsync();

      var actorMixersResult = new JObject(new JProperty("return", new JArray(
          new JObject(new JProperty("id", "{ID}"), new JProperty("name", "PC"), new JProperty("path", "\\PC"),
                      new JProperty("Volume", 0), new JProperty("Pitch", 0), new JProperty("Lowpass", 0), new JProperty("Highpass", 0), new JProperty("MakeUpGain", 0))
      )));
      var ancestorResult = new JObject(new JProperty("return", new JArray(new JObject(new JProperty("id", "{ANC}"), new JProperty("name", "ANC")))));
      
      SetupWaapiCallsForTest(actorMixersResult, ancestorResult, new JObject(new JProperty("properties", new JArray())), 
                            new JObject(new JProperty("return", new JArray())), new JObject(new JProperty("return", new JArray())), new JObject(new JProperty("return", new JArray())));

      // Act
      await _service.GetSanitizableMixersAsync();

      // Assert
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.get, 
          It.Is<JObject>(o => o["waql"]!.ToString().Contains("from type PropertyContainer") && o["waql"]!.ToString().Contains("and !customStates.any()")), 
          It.IsAny<JObject>(), It.IsAny<int>()), Times.AtLeastOnce);
          
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.get, 
          It.Is<JObject>(o => o["waql"]!.ToString().Contains("select ancestors.first(type = \"PropertyContainer\")")), 
          It.IsAny<JObject>(), It.IsAny<int>()), Times.AtLeastOnce);

      _clientMock.Verify(c => c.Call(ak.wwise.core.undo.beginGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetSanitizableMixersAsync_WhenLegacyWwise_UsesActorMixerWAQL()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      // Connect as 2024 (Legacy)
      var wwiseInfo = new JObject(new JProperty("version", new JObject(new JProperty("year", 2024))));
      _clientMock!.Setup(c => c.Call(ak.wwise.core.getInfo, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
          .ReturnsAsync(wwiseInfo);
      await _service!.ConnectAsync();

      var actorMixersResult = new JObject(new JProperty("return", new JArray(
          new JObject(new JProperty("id", "{ID}"), new JProperty("name", "AM"), new JProperty("path", "\\AM"),
                      new JProperty("Volume", 0), new JProperty("Pitch", 0), new JProperty("Lowpass", 0), new JProperty("Highpass", 0), new JProperty("MakeUpGain", 0))
      )));
      var ancestorResult = new JObject(new JProperty("return", new JArray(new JObject(new JProperty("id", "{ANC}"), new JProperty("name", "ANC")))));
      
      SetupWaapiCallsForTest(actorMixersResult, ancestorResult, new JObject(new JProperty("properties", new JArray())), 
                            new JObject(new JProperty("return", new JArray())), new JObject(new JProperty("return", new JArray())), new JObject(new JProperty("return", new JArray())));

      // Act
      await _service.GetSanitizableMixersAsync();

      // Assert
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.get, 
          It.Is<JObject>(o => o["waql"]!.ToString().Contains("from type actormixer")), 
          It.IsAny<JObject>(), It.IsAny<int>()), Times.AtLeastOnce);
          
      _clientMock.Verify(c => c.Call(ak.wwise.core.@object.get, 
          It.Is<JObject>(o => o["waql"]!.ToString().Contains("select ancestors.first(type = \"actormixer\")")), 
          It.IsAny<JObject>(), It.IsAny<int>()), Times.AtLeastOnce);

      _clientMock.Verify(c => c.Call(ak.wwise.core.undo.beginGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.AtLeastOnce);
    }
    [Fact]
    public async Task GetSanitizableMixersAsync_WhenCancelled_CallsCancelGroup()
    {
      // Arrange
      SetupTest();
      SetupDefaultMocks();

      // Connect as Legacy (to trigger undo group)
      var wwiseInfo = new JObject(new JProperty("version", new JObject(new JProperty("year", 2024))));
      _clientMock!.Setup(c => c.Call(ak.wwise.core.getInfo, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
          .ReturnsAsync(wwiseInfo);
      await _service!.ConnectAsync();

      _clientMock.Setup(c => c.Call(ak.wwise.core.undo.cancelGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()))
          .ReturnsAsync(new JObject());

      var cts = new System.Threading.CancellationTokenSource();
      
      // Setup actors to throw cancellation during processing
      var actorMixersResult = new JObject(new JProperty("return", new JArray(
          new JObject(new JProperty("id", "{ID}"), new JProperty("name", "AM"), new JProperty("path", "\\AM"),
                      new JProperty("Volume", 0), new JProperty("Pitch", 0), new JProperty("Lowpass", 0), new JProperty("Highpass", 0), new JProperty("MakeUpGain", 0))
      )));
      var ancestorResult = new JObject(new JProperty("return", new JArray(new JObject(new JProperty("id", "{ANC}"), new JProperty("name", "ANC")))));
      
      SetupWaapiCallsForTest(actorMixersResult, ancestorResult, new JObject(new JProperty("properties", new JArray())), 
                            new JObject(new JProperty("return", new JArray())), new JObject(new JProperty("return", new JArray())), new JObject(new JProperty("return", new JArray())));

      // Cancel before Act
      cts.Cancel();

      // Act & Assert
      await Assert.ThrowsAsync<OperationCanceledException>(() => _service.GetSanitizableMixersAsync(null, cts.Token));

      // Verify cancelGroup was called
      _clientMock.Verify(c => c.Call(ak.wwise.core.undo.cancelGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Once);
      
      // Verify endGroup and undoLast were NOT called
      _clientMock.Verify(c => c.Call(ak.wwise.core.undo.endGroup, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Never);
      _clientMock.Verify(c => c.Call(ak.wwise.core.undo.undoLast, It.IsAny<object>(), It.IsAny<object>(), It.IsAny<int>()), Times.Never);

      // Verify IsScanned is false
      Assert.False(_service.IsScanned);
    }
  }
}