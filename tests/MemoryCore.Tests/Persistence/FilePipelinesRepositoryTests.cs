using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mnemosyne.Core.Models.Pipelines;
using Mnemosyne.Core.Persistence;
using Moq;
using Shouldly;

namespace Mnemosyne.Core.Tests.Persistence
{
    public class FilePipelinesRepositoryTests
    {
        private readonly Mock<IOptions<PipelineStorageOptions>> _mockOptions;
        private readonly MockFileSystem _mockFileSystem;
        private readonly Mock<ILogger<FilePipelinesRepository>> _mockLogger;
        private readonly FilePipelinesRepository _repository;
        private readonly string _storagePath = "TestManifests";
        private readonly JsonSerializerOptions _jsonOptions;

        public FilePipelinesRepositoryTests()
        {
            _mockOptions = new Mock<IOptions<PipelineStorageOptions>>();
            _mockOptions.Setup(o => o.Value).Returns(new PipelineStorageOptions { StoragePath = _storagePath });
            
            _mockFileSystem = new MockFileSystem();
            _mockFileSystem.Directory.CreateDirectory(_storagePath); // Ensure base path exists

            _mockLogger = new Mock<ILogger<FilePipelinesRepository>>();

            _jsonOptions = new JsonSerializerOptions // Initialize _jsonOptions first
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };
            _jsonOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

            _repository = new FilePipelinesRepository(_jsonOptions, _mockFileSystem, _mockOptions.Object, _mockLogger.Object);
        }

        private string GetManifestPath(Guid id) => _mockFileSystem.Path.Combine(_storagePath, $"{id}.json");

        private async Task CreateMockManifestFile(PipelineManifest manifest)
        {
            var filePath = GetManifestPath(manifest.Id);
            var jsonContent = JsonSerializer.Serialize(manifest, _jsonOptions);
            await _mockFileSystem.File.WriteAllTextAsync(filePath, jsonContent);
        }

        [Fact]
        public async Task CreatePipelineAsync_ShouldCreateFile_AndReturnManifest()
        {
            // Arrange
            var manifest = new PipelineManifest { Name = "Test Create", Description = "Desc" };

            // Act
            var result = await _repository.CreatePipelineAsync(manifest);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Id.ShouldNotBe(Guid.Empty);
            result.Value.Name.ShouldBe("Test Create");
            _mockFileSystem.FileExists(GetManifestPath(result.Value.Id)).ShouldBeTrue();
            
            var fileContent = await _mockFileSystem.File.ReadAllTextAsync(GetManifestPath(result.Value.Id));
            var deserializedManifest = JsonSerializer.Deserialize<PipelineManifest>(fileContent, _jsonOptions);
            deserializedManifest.ShouldNotBeNull();
            deserializedManifest.Name.ShouldBe("Test Create");
        }

        [Fact]
        public async Task CreatePipelineAsync_WhenManifestIsNull_ShouldReturnFail()
        {
            // Act
            var result = await _repository.CreatePipelineAsync(null!);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe("Pipeline manifest cannot be null.");
        }
        
        [Fact]
        public async Task CreatePipelineAsync_WhenFileAlreadyExists_ShouldReturnFail()
        {
            // Arrange
            var manifest = new PipelineManifest { Id = Guid.NewGuid(), Name = "Test Existing" };
            await CreateMockManifestFile(manifest); // Pre-create the file

            // Act
            var result = await _repository.CreatePipelineAsync(manifest);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe($"Pipeline manifest with ID {manifest.Id} already exists.");
        }


        [Fact]
        public async Task GetPipelineAsync_WhenFileExists_ShouldReturnManifest()
        {
            // Arrange
            var manifestId = Guid.NewGuid();
            var manifest = new PipelineManifest { Id = manifestId, Name = "Test Get" };
            await CreateMockManifestFile(manifest);

            // Act
            var result = await _repository.GetPipelineAsync(manifestId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Id.ShouldBe(manifestId);
            result.Value.Name.ShouldBe("Test Get");
        }

        [Fact]
        public async Task GetPipelineAsync_WhenFileDoesNotExist_ShouldReturnFail()
        {
            // Arrange
            var manifestId = Guid.NewGuid();

            // Act
            var result = await _repository.GetPipelineAsync(manifestId);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe($"Pipeline manifest not found: {manifestId}");
        }

        [Fact]
        public async Task GetPipelineAsync_WhenFileIsInvalidJson_ShouldReturnFail()
        {
            // Arrange
            var manifestId = Guid.NewGuid();
            var filePath = GetManifestPath(manifestId);
            await _mockFileSystem.File.WriteAllTextAsync(filePath, "invalid json content");

            // Act
            var result = await _repository.GetPipelineAsync(manifestId);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldContain("Message deserializing pipeline manifest:");
        }
        
        [Fact]
        public async Task GetAllPipelinesAsync_ShouldReturnAllManifests()
        {
            // Arrange
            var manifest1 = new PipelineManifest { Id = Guid.NewGuid(), Name = "Test All 1" };
            var manifest2 = new PipelineManifest { Id = Guid.NewGuid(), Name = "Test All 2" };
            await CreateMockManifestFile(manifest1);
            await CreateMockManifestFile(manifest2);
             _mockFileSystem.AddFile("someotherfile.txt", new MockFileData("test"));


            // Act
            var result = await _repository.GetAllPipelinesAsync();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Count().ShouldBe(2);
            result.Value.ShouldContain(m => m.Id == manifest1.Id);
            result.Value.ShouldContain(m => m.Id == manifest2.Id);
        }

        [Fact]
        public async Task GetAllPipelinesAsync_WhenNoManifests_ShouldReturnEmptyList()
        {
            // Act
            var result = await _repository.GetAllPipelinesAsync();

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.ShouldBeEmpty();
        }
        
        [Fact]
        public async Task UpdatePipelineAsync_WhenFileExists_ShouldUpdateFileAndReturnManifest()
        {
            // Arrange
            var manifestId = Guid.NewGuid();
            var originalManifest = new PipelineManifest { Id = manifestId, Name = "Original Name" };
            await CreateMockManifestFile(originalManifest);

            var updatedManifest = new PipelineManifest { Id = manifestId, Name = "Updated Name", Description = "New Desc" };

            // Act
            var result = await _repository.UpdatePipelineAsync(manifestId, updatedManifest);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            result.Value.ShouldNotBeNull();
            result.Value.Name.ShouldBe("Updated Name");
            result.Value.Description.ShouldBe("New Desc");

            var fileContent = await _mockFileSystem.File.ReadAllTextAsync(GetManifestPath(manifestId));
            var deserializedManifest = JsonSerializer.Deserialize<PipelineManifest>(fileContent, _jsonOptions);
            deserializedManifest.ShouldNotBeNull();
            deserializedManifest.Name.ShouldBe("Updated Name");
        }

        [Fact]
        public async Task UpdatePipelineAsync_WhenManifestIsNull_ShouldReturnFail()
        {
            // Act
            var result = await _repository.UpdatePipelineAsync(Guid.NewGuid(), null!);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe("Pipeline manifest cannot be null for update.");
        }
        
        [Fact]
        public async Task UpdatePipelineAsync_WhenFileDoesNotExist_ShouldReturnFail()
        {
            // Arrange
            var manifestId = Guid.NewGuid();
            var manifestToUpdate = new PipelineManifest { Id = manifestId, Name = "Non Existent" };

            // Act
            var result = await _repository.UpdatePipelineAsync(manifestId, manifestToUpdate);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe($"Pipeline manifest not found for update: {manifestId}");
        }
        
        [Fact]
        public async Task DeletePipelineAsync_WhenFileExists_ShouldDeleteFileAndReturnOk()
        {
            // Arrange
            var manifestId = Guid.NewGuid();
            var manifest = new PipelineManifest { Id = manifestId, Name = "To Delete" };
            await CreateMockManifestFile(manifest);
            _mockFileSystem.FileExists(GetManifestPath(manifestId)).ShouldBeTrue();

            // Act
            var result = await _repository.DeletePipelineAsync(manifestId);

            // Assert
            result.IsSuccess.ShouldBeTrue();
            _mockFileSystem.FileExists(GetManifestPath(manifestId)).ShouldBeFalse();
        }

        [Fact]
        public async Task DeletePipelineAsync_WhenFileDoesNotExist_ShouldReturnFail()
        {
            // Arrange
            var manifestId = Guid.NewGuid();

            // Act
            var result = await _repository.DeletePipelineAsync(manifestId);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe($"Pipeline manifest not found for deletion: {manifestId}");
        }
         [Fact]
        public async Task DeletePipelineAsync_WhenIdIsEmpty_ShouldReturnFail()
        {
            // Act
            var result = await _repository.DeletePipelineAsync(Guid.Empty);

            // Assert
            result.IsFailed.ShouldBeTrue();
            result.Errors.First().Message.ShouldBe("Pipeline ID cannot be empty for delete.");
        }
    }
}